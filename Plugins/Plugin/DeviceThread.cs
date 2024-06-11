using DynamicExpresso;
using IoTGateway.DataAccess;
using IoTGateway.Model;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;
using Newtonsoft.Json;
using PluginInterface;
using System.Reflection;
using System.Text;

namespace Plugin;
public class DeviceThread : IDisposable
{
    public Device Device { get; }
    public IDriver Driver { get; }
    private readonly string _projectId;

    private readonly MyMqttClient _myMqttClient;
    private readonly MqttServer _mqttServer;

    private readonly ILogger _logger;

    private Interpreter _interpreter = new();
    internal List<MethodInfo>? Methods { get; set; }
    private Task? _task;
    private readonly DateTime _tsStartDt = new(1970, 1, 1);
    private readonly CancellationTokenSource _tokenSource = new();
    private readonly ManualResetEvent _resetEvent = new(true);

    public DeviceThread(Device device, IDriver driver, string projectId, MyMqttClient myMqttClient,
        MqttServer mqttServer, ILogger logger)
    {
        (Device, Driver, _projectId, _myMqttClient, _mqttServer, _logger)
            = (device, driver, projectId, myMqttClient, mqttServer, logger);

        _myMqttClient.OnExcRpc += MyMqttClient_OnExcRpc;

        Methods = Driver.GetType().GetMethods()
            .Where(x => x.GetCustomAttribute(typeof(MethodAttribute)) != null)
            .ToList();

        // 自动运行
        if (!Device.AutoStart) return;

        if (Device.DeviceVariables != null)
        {
            // 重置变量状态
            foreach (var vars in Device.DeviceVariables)
            {
                vars.StatusType = VaribaleStatusTypeEnum.Bad;
                if (string.IsNullOrWhiteSpace(vars.Alias))
                    vars.Alias = string.Empty;
            }
        }

        CreateThread().Wait();
        _logger.LogInformation("自启动设备{device}已启动", Device.DeviceName);
    }

    public async Task CreateThread()
    {
        _task = await Task.Factory.StartNew(async () =>
        {
            await Task.Delay(5000);

            // 上传客户端属性
            foreach (var deviceVariables in Device.DeviceVariables
                !.GroupBy(x => x.Alias))
            {
                var deviceName = string.IsNullOrWhiteSpace(deviceVariables.Key)
                        ? Device.DeviceName
                        : deviceVariables.Key;

                await _myMqttClient.UploadAttributeAsync(deviceName,
                    Device.DeviceConfigs
                    .Where(x => x.DataSide is DataSide.ClientSide or DataSide.AnySide)
                    .ToDictionary(x => x.DeviceConfigName, x => x.Value));
            }

            while (true)
            {
                if (_tokenSource.IsCancellationRequested)
                {
                    _logger.LogInformation("停止线程:{deviceName}", Device.DeviceName);
                    return;
                }

                _resetEvent.WaitOne();
                try
                {
                    if (Driver.IsConnected)
                    {
                        foreach (var deviceVariables in Device.DeviceVariables
                                                            .Where(x => x.ProtectType != ProtectTypeEnum.WriteOnly)
                                                            .GroupBy(x => x.Alias))
                        {
                            var deviceName = string.IsNullOrWhiteSpace(deviceVariables.Key)
                                ? Device.DeviceName
                                : deviceVariables.Key;

                            var sendModel = new Dictionary<string, List<PayLoad>>
                            {
                                { deviceName, new() }
                            };

                            var payLoad = new PayLoad
                            {
                                Values = new()
                            };

                            if (!deviceVariables.Any()) continue;
                            foreach (var deviceVar in deviceVariables.OrderBy(x => x.Index))
                            {
                                deviceVar.Value = null;
                                deviceVar.CookedValue = null;
                                deviceVar.StatusType = VaribaleStatusTypeEnum.Bad;

                                await Task.Delay((int)Device.CmdPeriod);

                                // 构建返回值
                                var ret = new DriverReturnValueModel();
                                var ioArg = new DriverAddressIoArgModel
                                {
                                    ID = deviceVar.ID,
                                    Address = deviceVar.DeviceAddress,
                                    ValueType = deviceVar.DataType,
                                    EndianType = deviceVar.EndianType
                                };
                                var method = Methods
                                    ?.FirstOrDefault(x => x.Name == deviceVar.Method);

                                if (method is null)
                                {
                                    ret.StatusType = VaribaleStatusTypeEnum.MethodError;
                                }
                                else
                                {
                                    var arg = new object[] { ioArg };
                                    ret = (DriverReturnValueModel)method.Invoke(Driver,
                                        arg)!;
                                }

                                deviceVar.EnqueueVariable(ret.Value);

                                if (ret.StatusType == VaribaleStatusTypeEnum.Good &&
                                    !string.IsNullOrWhiteSpace(deviceVar.Expressions?.Trim()))
                                {
                                    var expressionText = DealMysqlStr(deviceVar.Expressions)
                                        .Replace("raw",
                                            deviceVar.Values[0] is bool
                                                ? $"Convert.ToBoolean(\"{deviceVar.Values[0]}\")"
                                                : deviceVar.Values[0]?.ToString())
                                        .Replace("$ppv",
                                            deviceVar.Values[2] is bool
                                                ? $"Convert.ToBoolean(\"{deviceVar.Values[2]}\")"
                                                : deviceVar.Values[2]?.ToString())
                                        .Replace("$pv",
                                            deviceVar.Values[1] is bool
                                                ? $"Convert.ToBoolean(\"{deviceVar.Values[1]}\")"
                                                : deviceVar.Values[1]?.ToString());

                                    try
                                    {
                                        ret.CookedValue = _interpreter.Eval(expressionText);
                                    }
                                    catch (Exception)
                                    {
                                        ret.StatusType = VaribaleStatusTypeEnum.ExpressionError;
                                    }
                                }
                                else
                                {
                                    ret.CookedValue = ret.Value;
                                }

                                if (deviceVar.IsUpload)
                                {
                                    payLoad.Values[deviceVar.Name] = ret.CookedValue;
                                }

                                ret.VarId = deviceVar.ID;

                                // 变化了才推送到mqttserver，用于前端展示
                                // 组态用的MQTT server
                                if ((deviceVar.Values[1] == null && deviceVar.Values[0] != null) ||
                                    (deviceVar.Values[1] != null && deviceVar.Values[0] != null && JsonConvert.SerializeObject(deviceVar.Values[1]) != JsonConvert.SerializeObject(deviceVar.Values[0])))
                                {
                                    //这是设备变量列表要用的
                                    var msgInternal = new InjectedMqttApplicationMessage(
                                        new()
                                        {
                                            Topic = $"internal/v1/gateway/telemetry/{deviceName}/{deviceVar.Name}",
                                            PayloadSegment = Encoding.UTF8.GetBytes(JsonUtility.SerializeToJson(ret))
                                        });

                                    await _mqttServer.InjectApplicationMessage(msgInternal);

                                    //这是在线组态要用的
                                    var msgConfigure = new InjectedMqttApplicationMessage(
                                        new()
                                        {
                                            Topic = $"v1/gateway/telemetry/{deviceName}/{deviceVar.Name}",
                                            PayloadSegment = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(ret.CookedValue))
                                        });

                                    await _mqttServer.InjectApplicationMessage(msgConfigure);
                                }

                                (deviceVar.Value, deviceVar.CookedValue, deviceVar.Timestamp, deviceVar.StatusType)
                                    = (ret.Value, ret.CookedValue, ret.Timestamp, ret.StatusType);
                            }

                            payLoad.TS = (long)(DateTime.UtcNow - _tsStartDt).TotalMilliseconds;

                            if (deviceVariables.Where(x => x.IsUpload && x.ProtectType != ProtectTypeEnum.WriteOnly).All(x => x.StatusType == VaribaleStatusTypeEnum.Good))
                            {
                                payLoad.DeviceStatus = DeviceStatusTypeEnum.Good;
                                sendModel[deviceName] = new() { payLoad };
                                _myMqttClient
                                    .PublishTelemetryAsync(deviceName,
                                        Device, sendModel).Wait();
                            }
                            else if (deviceVariables.Any(x => x.StatusType == VaribaleStatusTypeEnum.Bad))
                                _myMqttClient?.DeviceDisconnected(deviceName, Device);

                        }

                        // 只要有读取异常且连接正常就断开
                        if (Device.DeviceVariables
                            .Where(x => x.IsUpload && x.ProtectType != ProtectTypeEnum.WriteOnly)
                            .Any(x => x.StatusType != VaribaleStatusTypeEnum.Good) && Driver.IsConnected)
                        {
                            Driver.Close();
                            Driver.Dispose();
                        }
                    }
                    else
                    {
                        foreach (var deviceVariables in Device.DeviceVariables!.GroupBy(x => x.Alias))
                        {
                            var deviceName = string.IsNullOrWhiteSpace(deviceVariables.Key)
                                ? Device.DeviceName
                                : deviceVariables.Key;

                            _myMqttClient?.DeviceDisconnected(deviceName, Device);
                        }

                        if (Driver.Connect())
                        {
                            foreach (var deviceVariables in Device.DeviceVariables!.GroupBy(x => x.Alias))
                            {
                                var deviceName = string.IsNullOrWhiteSpace(deviceVariables.Key)
                                    ? Device.DeviceName
                                    : deviceVariables.Key;

                                _myMqttClient?.DeviceConnected(deviceName, Device);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "线程循环异常,{deviceName}", Device.DeviceName);
                }

                await Task.Delay(Device.DeviceVariables!.Any() ? (int)Driver.MinPeriod : 10000);
            }
        }, TaskCreationOptions.LongRunning);
    }

    public void MyMqttClient_OnExcRpc(object? sender, RpcRequest e)
    {
        //设备名或者设备别名
        if (e.DeviceName != Device.DeviceName &&
            !Device.DeviceVariables.Select(x => x.Alias).Contains(e.DeviceName)) return;
        {
            var rpcLog = new RpcLog
            {
                DeviceId = Device.ID,
                StartTime = DateTime.Now,
                Method = e.Method,
                RpcSide = RpcSide.ServerSide,
                Params = JsonConvert.SerializeObject(e.Params)
            };

            _logger.LogInformation($"{e.DeviceName}收到RPC,{e}");
            RpcResponse rpcResponse = new()
            { DeviceName = e.DeviceName, RequestId = e.RequestId, IsSuccess = false, Method = e.Method };
            //执行写入变量RPC
            if (e.Method.ToLower() == "write")
            {
                _resetEvent.Reset();

                var rpcConnected = false;
                //没连接就连接
                if (!Driver.IsConnected)
                    if (Driver.Connect())
                        rpcConnected = true;

                //连接成功就尝试一个一个的写入，注意:目前写入地址和读取地址是相同的，对于PLC来说没问题，其他的要自己改........
                if (Driver.IsConnected)
                {
                    foreach (var para in e.Params)
                    {
                        //先查配置项，要用到配置的地址、数据类型、方法(方法最主要是用于区分写入数据的辅助判断，比如modbus不同的功能码)
                        //先找别名中的变量名，找不到就用设备名
                        DeviceVariable? deviceVariable;
                        if (e.DeviceName == Device.DeviceName)
                            deviceVariable = Device.DeviceVariables.FirstOrDefault(x =>
                                x.Name == para.Key && string.IsNullOrWhiteSpace(x.Alias));
                        else
                            deviceVariable = Device.DeviceVariables.FirstOrDefault(x =>
                                x.Name == para.Key && x.Alias == e.DeviceName);

                        if (deviceVariable != null && deviceVariable.ProtectType != ProtectTypeEnum.ReadOnly)
                        {
                            DriverAddressIoArgModel ioArgModel = new()
                            {
                                Address = deviceVariable.DeviceAddress,
                                Value = para.Value,
                                ValueType = deviceVariable.DataType,
                                EndianType = deviceVariable.EndianType
                            };
                            var writeResponse = Driver
                                .WriteAsync(e.RequestId, deviceVariable.Method, ioArgModel).Result;
                            rpcResponse.IsSuccess = writeResponse.IsSuccess;
                            if (!writeResponse.IsSuccess)
                            {
                                rpcResponse.Description += writeResponse.Description;
                            }
                        }
                        else
                        {
                            rpcResponse.IsSuccess = false;
                            rpcResponse.Description += $"未能找到支持写入的变量:{para.Key},";
                        }
                    }

                    if (rpcConnected)
                        Driver.Close();
                }
                else //连接失败
                {
                    rpcResponse.IsSuccess = false;
                    rpcResponse.Description = $"{e.DeviceName} 连接失败";
                }
                _resetEvent.Set();
            }
            //其他RPC TODO
            else
            {
                rpcResponse.IsSuccess = false;
                rpcResponse.Description = $"方法:{e.Method}暂未实现";
            }

            //反馈RPC
            _myMqttClient.ResponseRpcAsync(rpcResponse).Wait();
            //纪录入库
            rpcLog.IsSuccess = rpcResponse.IsSuccess;
            rpcLog.Description = rpcResponse.Description;
            rpcLog.EndTime = DateTime.Now;


            using var dc = new DataContext(IoTBackgroundService.connnectSetting, IoTBackgroundService.DbType);
            dc.Set<RpcLog>().Add(rpcLog);
            dc.SaveChanges();
        }
    }

    public void StopThread()
    {
        _logger.LogInformation("{dev}线程停止", Device.DeviceName);
        if (Device.DeviceVariables != null && Device.DeviceVariables.Any())
        {
            foreach (var deviceVariables in Device.DeviceVariables.GroupBy(x => x.Alias))
            {
                var deviceName = string.IsNullOrWhiteSpace(deviceVariables.Key)
                    ? Device.DeviceName
                    : deviceVariables.Key;

                // 断开连接
                _myMqttClient?.DeviceDisconnected(deviceName, Device);
            }
        }

        if (_task == null) return;
        if (_myMqttClient != null) _myMqttClient.OnExcRpc -= MyMqttClient_OnExcRpc;
        _tokenSource.Cancel();
        Driver.Close();
    }

    public void Dispose()
    {
        Driver.Dispose();
        _interpreter = null;
        Methods = null;
        _logger.LogInformation("{dev}线程释放", Device.DeviceName);
    }

    //mysql会把一些符号转义，没找到原因，先临时处理下
    private string DealMysqlStr(string expression)
    {
        return expression.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Replace("&quot;", "\"");
    }
}
