using MQTTnet;
using MQTTnet.Client;
using Newtonsoft.Json;
using PluginInterface;
using IoTGateway.Model;
using MQTTnet.Protocol;
using MQTTnet.Formatter;
using IoTGateway.DataAccess;
using PluginInterface.IoTSharp;
using PluginInterface.HuaWeiRoma;
using Microsoft.Extensions.Logging;
using RpcRequest = PluginInterface.ThingsBoard.TBRpcRequest;

namespace Plugin
{
    public class MyMqttClient
    {
        private readonly ILogger<MyMqttClient> _logger;
        //private readonly ReferenceNodeManager? _uaNodeManager;

        private SystemConfig _systemConfig;
        private MqttClientOptions _options;
        public bool IsConnected => (Client?.IsConnected ?? false);
        private IMqttClient? Client { get; set; }
        public event EventHandler<PluginInterface.RpcRequest> OnExcRpc;
        public event EventHandler<ISAttributeResponse> OnReceiveAttributes;
        private readonly string _tbRpcTopic = "v1/gateway/rpc";

        //UAService uaService, 
        public MyMqttClient(ILogger<MyMqttClient> logger)
        {
            _logger = logger;
            //_uaNodeManager = uaService.server.m_server.nodeManagers[0] as ReferenceNodeManager;

            StartClientAsync().Wait();
        }

        public async Task StartClientAsync()
        {
            try
            {
                Client?.Dispose();
                Client = new MqttFactory().CreateMqttClient();
                await using var dc = new DataContext(IoTBackgroundService.connnectSetting, IoTBackgroundService.DbType);
                _systemConfig = dc.Set<SystemConfig>().First();

                #region ClientOptions
                _options = new MqttClientOptionsBuilder()
                    .WithClientId(string.IsNullOrEmpty(_systemConfig.ClientId)
                        ? Guid.NewGuid().ToString()
                        : _systemConfig.ClientId)
                    .WithTcpServer(_systemConfig.MqttIp, _systemConfig.MqttPort)
                    .WithCredentials(_systemConfig.MqttUName, _systemConfig.MqttUPwd)
                    .WithTimeout(TimeSpan.FromSeconds(30))
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                    .WithProtocolVersion(MqttProtocolVersion.V311)
                    .WithCleanSession(true)
                    .Build();
                #endregion
                Client.ConnectedAsync += Client_ConnectedAsync;
                Client.DisconnectedAsync += Client_DisconnectedAsync;
                Client.ApplicationMessageReceivedAsync += Client_ApplicationMessageReceivedAsync;

                if (Client.ConnectAsync(_options).IsCompletedSuccessfully) ;
                {
                    _logger.LogInformation("MQTT客户端等待消息");
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"开启MQTT客户端失败");
            }
        }

        private async Task Client_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            _logger.LogInformation("MQTT客户端连接成功");
            #region Topics
            try
            {
                switch (_systemConfig.IoTPlatformType)
                {
                    case IoTPlatformType.ThingsBoard:
                        //{"device": "Device A", "data": {"id": $request_id, "method": "toggle_gpio", "params": {"pin":1}}}
                        await Client.SubscribeAsync(_tbRpcTopic, MqttQualityOfServiceLevel.ExactlyOnce);
                        //Message: {"id": $request_id, "device": "Device A", "value": "value1"}
                        await Client.SubscribeAsync("v1/gateway/attributes/response", MqttQualityOfServiceLevel.ExactlyOnce);
                        //Message: {"device": "Device A", "data": {"attribute1": "value1", "attribute2": 42}}
                        await Client.SubscribeAsync("v1/gateway/attributes", MqttQualityOfServiceLevel.ExactlyOnce);
                        break;
                    case IoTPlatformType.IoTSharp:
                    case IoTPlatformType.IoTGateway:
                        await Client.SubscribeAsync("devices/+/rpc/request/+/+", MqttQualityOfServiceLevel.ExactlyOnce);
                        await Client.SubscribeAsync("devices/+/attributes/update", MqttQualityOfServiceLevel.ExactlyOnce);
                        //Message: {"device": "Device A", "data": {"attribute1": "value1", "attribute2": 42}}
                        await Client.SubscribeAsync("devices/+/attributes/response/+", MqttQualityOfServiceLevel.ExactlyOnce);
                        break;
                    case IoTPlatformType.ThingsCloud:
                        await Client.SubscribeAsync("gateway/attributes/response", MqttQualityOfServiceLevel.ExactlyOnce);
                        await Client.SubscribeAsync("gateway/attributes/get/response", MqttQualityOfServiceLevel.ExactlyOnce);
                        await Client.SubscribeAsync("gateway/attributes/push", MqttQualityOfServiceLevel.ExactlyOnce);
                        await Client.SubscribeAsync("gateway/event/response", MqttQualityOfServiceLevel.ExactlyOnce);
                        await Client.SubscribeAsync("gateway/command/send", MqttQualityOfServiceLevel.ExactlyOnce);
                        break;
                    case IoTPlatformType.AliCloudIoT:
                        break;
                    case IoTPlatformType.TencentIoTHub:
                        break;
                    case IoTPlatformType.BaiduIoTCore:
                        break;
                    case IoTPlatformType.OneNET:
                        break;
                    case IoTPlatformType.HuaWei:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT订阅失败");
            }
            #endregion
        }

        private async Task Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            try
            {
                _logger.LogError($"MQTT断开连接");
                _ = await Client?.ConnectAsync(_options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT连接失败");
            }
        }

        private Task Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            _logger.LogDebug(
                $"ApplicationMessageReceived Topic {e.ApplicationMessage.Topic}  QualityOfServiceLevel:{e.ApplicationMessage.QualityOfServiceLevel} Retain:{e.ApplicationMessage.Retain} ");
            try
            {
                if (e.ApplicationMessage.Topic == _tbRpcTopic)
                {
                    // ThinkBoard RPC
                }
                else if (e.ApplicationMessage.Topic.StartsWith($"devices/") &&
                         e.ApplicationMessage.Topic.Contains("/response/"))
                {
                    ReceiveAttributes(e.ApplicationMessage);
                }
                else if (e.ApplicationMessage.Topic.StartsWith($"devices/") &&
                         e.ApplicationMessage.Topic.Contains("/rpc/request/"))
                {
                    // ReceiveIsRpc(e);
                }
                else if (e.ApplicationMessage.Topic == "gateway/command/send")
                {
                    // ThingsCloud RPC
                    ReceiveRpc(e.ApplicationMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, $"ClientId:{e.ClientId} Topic:{e.ApplicationMessage.Topic},Payload:{e.ApplicationMessage.ConvertPayloadToString()}");
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// thingscloud rpc
        /// </summary>
        /// <param name="msg"></param>
        private void ReceiveRpc(MqttApplicationMessage msg)
        {
            try
            {
                var tCRpcRequest =
                    JsonConvert.DeserializeObject<RpcRequest>(msg.ConvertPayloadToString());
                if (tCRpcRequest != null)
                    OnExcRpc.Invoke(Client, new()
                    {
                        Method = tCRpcRequest.RequestData.Method,
                        DeviceName = tCRpcRequest.DeviceName,
                        RequestId = tCRpcRequest.RequestData.RequestId,
                        Params = tCRpcRequest.RequestData.Params
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ThingCloud RPC:Topic:{topic},Payload:{payload}", msg.Topic, msg.ConvertPayloadToString());
            }
        }

        private async Task ResponseTcRpcAsync(RpcRequest tCRpcResponse)
        {
            var topic = $"command/reply/{tCRpcResponse.RequestData.RequestId}";
            await Client.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(JsonConvert.SerializeObject(tCRpcResponse))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
        }

        private void ReceiveAttributes(MqttApplicationMessage msg)
        {
            var tps = msg.Topic.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var rpcMethodName = tps[2];
            var rpcDeviceName = tps[1];
            var rpcRequestId = tps[4];
            _logger.LogInformation("rpcMethodName={method}", rpcMethodName);
            _logger.LogInformation("rpcDeviceName={dev}", rpcDeviceName);
            _logger.LogInformation("rpcRequestId={req}", rpcRequestId);

            if (string.IsNullOrEmpty(rpcMethodName) || string.IsNullOrEmpty(rpcDeviceName) ||
                string.IsNullOrEmpty(rpcRequestId)) return;

            if (msg.Topic.Contains("/attributes/"))
            {
                OnReceiveAttributes.Invoke(Client, new()
                {
                    KeyName = rpcMethodName,
                    DeviceName = rpcDeviceName,
                    Id = rpcRequestId,
                    Data = msg.ConvertPayloadToString()
                });
            }
        }

        public Task UploadAttributeAsync(string deviceName, object obj)
        {
            //Topic: v1/gateway/attributes
            //Message: {"Device A":{"attribute1":"value1", "attribute2": 42}, "Device B":{"attribute1":"value1", "attribute2": 42}
            try
            {
                return Client.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic($"devices/{deviceName}/attributes").WithPayload(JsonConvert.SerializeObject(obj))
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .Build());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device:{dev} 上传属性失败", deviceName);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 上传遥测数据
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public async Task UploadTelemetryDataAsync(string deviceName, object obj)
        {
            var toSend = new Dictionary<string, object> { { deviceName, obj } };
            await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"gateway/attributes")
                .WithPayload(JsonConvert.SerializeObject(toSend)).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
        }

        public async Task ResponseRpcAsync(RpcResponse rpcResponse)
        {
            try
            {
                switch (_systemConfig.IoTPlatformType)
                {
                    case IoTPlatformType.ThingsBoard:
                        //var tRpcResponse = new TBRpcResponse
                        //{
                        //    DeviceName = rpcResponse.DeviceName,
                        //    RequestId = rpcResponse.RequestId,
                        //    ResponseData = new Dictionary<string, object>
                        //        { { "success", rpcResponse.IsSuccess }, { "description", rpcResponse.Description } }
                        //};
                        //await ResponseTbRpcAsync(tRpcResponse);
                        break;
                    case IoTPlatformType.IoTSharp:
                    case IoTPlatformType.IoTGateway:
                        //await ResponseIsRpcAsync(new ISRpcResponse
                        //{
                        //    DeviceId = rpcResponse.DeviceName,
                        //    Method = rpcResponse.Method,
                        //    ResponseId = rpcResponse.RequestId,
                        //    Data = JsonConvert.SerializeObject(new Dictionary<string, object>
                        //    {
                        //        { "success", rpcResponse.IsSuccess }, { "description", rpcResponse.Description }
                        //    })
                        //});
                        break;
                    case IoTPlatformType.ThingsCloud:
                        //官网API不需要回复的                        
                        break;
                    case IoTPlatformType.AliCloudIoT:
                        break;
                    case IoTPlatformType.TencentIoTHub:
                        break;
                    case IoTPlatformType.BaiduIoTCore:
                        break;
                    case IoTPlatformType.OneNET:
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ResponseRpc Error,{rpcResponse}");
            }
        }

        public async Task RequestAttributes(string deviceName, bool anySide, params string[] args)
        {
            try
            {
                var id = Guid.NewGuid().ToString();
                switch (_systemConfig.IoTPlatformType)
                {
                    case IoTPlatformType.ThingsBoard:
                        //{"id": $request_id, "device": "Device A", "client": true, "key": "attribute1"}
                        //var tbRequestData = new Dictionary<string, object>
                        //{
                        //    { "id", id },
                        //    { "device", deviceName },
                        //    { "client", true },
                        //    { "key", args[0] }
                        //};
                        //await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"v1/gateway/attributes/request")
                        //    .WithPayload(JsonConvert.SerializeObject(tbRequestData)).WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
                        break;
                    case IoTPlatformType.IoTSharp:
                    case IoTPlatformType.IoTGateway:
                        //var topic = $"devices/{deviceName}/attributes/request/{id}";
                        //Dictionary<string, string> keys = new Dictionary<string, string>();
                        //keys.Add(anySide ? "anySide" : "server", string.Join(",", args));
                        //await Client.SubscribeAsync($"devices/{deviceName}/attributes/response/{id}",
                        //    MqttQualityOfServiceLevel.ExactlyOnce);
                        //await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topic)
                        //    .WithPayload(JsonConvert.SerializeObject(keys))
                        //    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
                        //break;
                    case IoTPlatformType.AliCloudIoT:
                        break;
                    case IoTPlatformType.TencentIoTHub:
                        break;
                    case IoTPlatformType.BaiduIoTCore:
                        break;
                    case IoTPlatformType.OneNET:
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"RequestAttributes:{deviceName}");
            }
        }

        private Dictionary<string, List<PayLoad>> _lastTelemetrys = new(0);

        /// <summary>
        /// 判断是否推送遥测数据
        /// </summary>
        /// <param name="deviceName">设备</param>
        /// <param name="device">设备</param>
        /// <returns></returns>
        private bool CanPubTelemetry(string deviceName, Device device, Dictionary<string, List<PayLoad>> sendModel)
        {
            var canPub = false;
            try
            {//第一次上传
                if (!_lastTelemetrys.ContainsKey(deviceName))
                    canPub = true;
                else
                {
                    //变化上传
                    if (device.CgUpload)
                    {
                        //是否超过归档周期
                        if (sendModel[deviceName][0].TS - _lastTelemetrys[deviceName][0].TS >
                            device.EnforcePeriod)
                            canPub = true;
                        //是否变化 这里不好先用
                        else
                        {
                            if (JsonConvert.SerializeObject(sendModel[deviceName][0].Values) !=
                                JsonConvert.SerializeObject(_lastTelemetrys[deviceName][0].Values))
                                canPub = true;
                        }
                    }
                    //非变化上传
                    else
                        canPub = true;
                }
            }
            catch (Exception e)
            {
                canPub = true;
                Console.WriteLine(e);
            }

            if (canPub)
                _lastTelemetrys[deviceName] = sendModel[deviceName];
            return canPub;
        }

        public async Task PublishTelemetryAsync(string deviceName, Device device, Dictionary<string, List<PayLoad>> sendModel)
        {
            try
            {
                if (CanPubTelemetry(deviceName, device, sendModel))
                {
                    switch (_systemConfig.IoTPlatformType)
                    {
                        case IoTPlatformType.ThingsBoard:
                            await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("v1/gateway/telemetry")
                                .WithPayload(JsonConvert.SerializeObject(sendModel))
                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce).Build());
                            break;
                        case IoTPlatformType.IoTSharp:
                        case IoTPlatformType.IoTGateway:
                            //foreach (var payload in sendModel[deviceName])
                            //{
                            //    if (payload.Values != null)
                            //    {
                            //        if (_systemConfig.IoTPlatformType == IoTPlatformType.IoTGateway)
                            //            payload.Values["_ts_"] = (long)(DateTime.UtcNow - _tsStartDt).TotalMilliseconds;
                            //        await UploadIsTelemetryDataAsync(deviceName, payload.Values);
                            //    }
                            //}

                            break;
                        case IoTPlatformType.ThingsCloud:
                            foreach (var payload in sendModel[deviceName])
                            {
                                if (payload.Values != null)
                                    await UploadTelemetryDataAsync(deviceName, payload.Values);
                            }

                            break;
                        case IoTPlatformType.HuaWei:
                            //foreach (var payload in sendModel[deviceName])
                            //{
                            //    if (payload.Values != null)
                            //        await UploadHwTelemetryDataAsync(device, payload.Values);
                            //}
                            break;

                        case IoTPlatformType.AliCloudIoT:
                        case IoTPlatformType.TencentIoTHub:
                        case IoTPlatformType.BaiduIoTCore:
                        case IoTPlatformType.OneNET:
                        default:
                            break;
                    }
                }

                //foreach (var payload in sendModel[deviceName])
                //{
                //    if (payload.Values != null)
                //        foreach (var kv in payload.Values)
                //        {
                //            //更新到UAService
                //            _uaNodeManager?.UpdateNode($"{device.Parent.DeviceName}.{deviceName}.{kv.Key}",
                //                kv.Value);
                //        }
                //}
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"PublishTelemetryAsync Error");
            }
        }

        private readonly DateTime _tsStartDt = new(1970, 1, 1);

        public async Task DeviceConnected(string DeviceName, Device device)
        {
            try
            {
                switch (_systemConfig.IoTPlatformType)
                {
                    case IoTPlatformType.ThingsBoard:
                    case IoTPlatformType.IoTSharp:
                    case IoTPlatformType.IoTGateway:
                        await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("v1/gateway/connect")
                            .WithPayload(JsonConvert.SerializeObject(new Dictionary<string, string>
                                { { "device", DeviceName } }))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce).Build());
                        break;
                    case IoTPlatformType.AliCloudIoT:
                        break;
                    case IoTPlatformType.TencentIoTHub:
                        break;
                    case IoTPlatformType.BaiduIoTCore:
                        break;
                    case IoTPlatformType.OneNET:
                        break;
                    case IoTPlatformType.ThingsCloud:
                        await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic("gateway/connect")
                            .WithPayload(JsonConvert.SerializeObject(new Dictionary<string, string>
                                { { "device", DeviceName } }))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
                        break;
                    case IoTPlatformType.HuaWei:
                        var deviceOnLine = new HwDeviceOnOffLine()
                        {
                            MId = new Random().Next(0, 1024), //命令ID
                            DeviceStatuses = new()
                            {
                                new()
                                {
                                    DeviceId = device.DeviceConfigs
                                        .FirstOrDefault(x => x.DeviceConfigName == "DeviceId")
                                        ?.Value,
                                    Status = "ONLINE"
                                }
                            }
                        };
                        await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"/v1/devices/{_systemConfig.GatewayName}/topo/update")
                            .WithPayload(JsonConvert.SerializeObject(deviceOnLine))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DeviceConnected:{DeviceName}");
            }
        }

        public async Task DeviceDisconnected(string DeviceName, Device device)
        {
            try
            {
                switch (_systemConfig.IoTPlatformType)
                {
                    case IoTPlatformType.ThingsBoard:
                    case IoTPlatformType.IoTSharp:
                    case IoTPlatformType.IoTGateway:
                        await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"v1/gateway/disconnect")
                            .WithPayload(JsonConvert.SerializeObject(new Dictionary<string, string>
                                { { "device", DeviceName } }))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce).Build());
                        break;
                    case IoTPlatformType.AliCloudIoT:
                        break;
                    case IoTPlatformType.TencentIoTHub:
                        break;
                    case IoTPlatformType.BaiduIoTCore:
                        break;
                    case IoTPlatformType.OneNET:
                        break;
                    case IoTPlatformType.ThingsCloud:
                        await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"gateway/disconnect")
                            .WithPayload(JsonConvert.SerializeObject(new Dictionary<string, string>
                                { { "device", DeviceName } }))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
                        break;
                    case IoTPlatformType.HuaWei:
                        var deviceOnLine = new HwDeviceOnOffLine()
                        {
                            MId = new Random().Next(0, 1024), //命令ID
                            DeviceStatuses = new()
                            {
                                new()
                                {
                                    DeviceId = device.DeviceConfigs
                                        .FirstOrDefault(x => x.DeviceConfigName == "DeviceId")
                                        ?.Value,
                                    Status = "OFFLINE"
                                }
                            }
                        };
                        await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"/v1/devices/{_systemConfig.GatewayName}/topo/update")
                            .WithPayload(JsonConvert.SerializeObject(deviceOnLine))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DeviceDisconnected:{DeviceName}");
            }
        }

        public async Task DeviceAdded(Device device)
        {
            try
            {
                switch (_systemConfig.IoTPlatformType)
                {
                    case IoTPlatformType.HuaWei:
                        var topic = $"/v1/devices/{_systemConfig.GatewayName}/topo/add";

                        var addDeviceDto = new HwAddDeviceDto
                        {
                            MId = new Random().Next(0, 1024), //命令ID
                        };
                        addDeviceDto.DeviceInfos.Add(
                            new()
                            {
                                NodeId = device.DeviceName,
                                Name = device.DeviceName,
                                Description = device.Description,
                                ManufacturerId = "Test_n",
                                ProductType = "A_n"
                            }
                        );
                        await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topic)
                            .WithPayload(JsonConvert.SerializeObject(addDeviceDto))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DeviceAdded:{device.DeviceName}");
            }
        }

        public async Task DeviceDeleted(Device device)
        {
            try
            {
                switch (_systemConfig.IoTPlatformType)
                {
                    case IoTPlatformType.HuaWei:
                        var topic = $"/v1/devices/{_systemConfig.GatewayName}/topo/delete";

                        await using (var dc = new DataContext(IoTBackgroundService.connnectSetting, IoTBackgroundService.DbType))
                        {
                            var deviceId = dc.Set<DeviceConfig>().FirstOrDefault(x =>
                                x.DeviceId == device.ID && x.DeviceConfigName == "DeviceId")?.Value;

                            var deleteDeviceDto = new HwDeleteDeviceDto
                            {
                                Id = new Random().Next(0, 1024), //命令ID
                                DeviceId = deviceId,
                                RequestTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds,
                                Request = new()
                                {
                                    ManufacturerId = "Test_n",
                                    ManufacturerName = "Test_n",
                                    ProductType = "A_n"
                                }
                            };
                            await Client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topic)
                                .WithPayload(JsonConvert.SerializeObject(deleteDeviceDto))
                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce).Build());
                        }
                        break;
                    case IoTPlatformType.ThingsBoard:
                        break;
                    case IoTPlatformType.IoTSharp:
                        break;
                    case IoTPlatformType.AliCloudIoT:
                        break;
                    case IoTPlatformType.TencentIoTHub:
                        break;
                    case IoTPlatformType.BaiduIoTCore:
                        break;
                    case IoTPlatformType.OneNET:
                        break;
                    case IoTPlatformType.ThingsCloud:
                        break;
                    case IoTPlatformType.IoTGateway:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除设备错误{dev}", device.DeviceName);
            }
        }
    }
}