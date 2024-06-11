using IoTGateway.DataAccess;
using IoTGateway.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet.Server;
using PluginInterface;
using System.Reflection;
using WalkingTec.Mvvm.Core;

namespace Plugin;

public class DeviceService : IDisposable
{
    public DriverService DriverService { get; }

    // ReSharper disable once NotAccessedField.Local
    private readonly IConfiguration _configRoot;
    private readonly MyMqttClient _mqttClient;
    private readonly MqttServer _mqttServer;
    private readonly ILogger<DeviceService> _logger;

    private static readonly Dictionary<Type, Func<Type, string, object>> ParserTable
        = new(){
                { typeof(bool), (_, val) => val != "0" },
                { typeof(string), (_,val) => val },
                { typeof(Enum), Enum.Parse },
        };

    public List<DeviceThread> DeviceThreads = new();

    private readonly string _connectSetting = IoTBackgroundService.connnectSetting;
    private readonly DBTypeEnum _dbType = IoTBackgroundService.DbType;

    public DeviceService(IConfiguration configRoot,
        DriverService driverService,
        MyMqttClient mqttClient,
        MqttServer mqttServer,
        ILogger<DeviceService> logger)
    {
        ArgumentNullException.ThrowIfNull(mqttServer);

        (_configRoot, DriverService, _mqttClient, _mqttServer, _logger)
            = (configRoot, driverService, mqttClient, mqttServer, logger);

        try
        {
            using var dataContext = new DataContext(_connectSetting, _dbType);
            // 从数据库中筛选出设备
            var devices = dataContext.Set<Device>()
                .Where(x => x.DeviceTypeEnum == DeviceTypeEnum.Device)
                .Include(x => x.Parent)
                .Include(x => x.Driver)
                .Include(x => x.DeviceConfigs)
                .Include(x => x.DeviceVariables)
                .AsNoTracking()
                .ToList();
            _logger.LogInformation("Loaded Devices Count: {count}", devices.Count);

            // 为所有设备创建线程
            Parallel.ForEach(devices, CreateDeviceThread);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadDevicesError.");
        }
    }

    /// <summary>
    /// 更新设备
    /// </summary>
    /// <param name="device"></param>
    public void UpdateDevice(Device device)
    {
        try
        {
            _logger.LogInformation("开始更新设备{device}", device.DeviceName);
            RemoveDeviceThread(device);
            CreateDeviceThread(device);
            _logger.LogInformation("完成更新设备{device}", device.DeviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新设备{device}失败", device.DeviceName);
        }
    }

    /// <summary>
    /// 更新多个设备
    /// </summary>
    /// <param name="devices"></param>
    public void UpdateDevices(IList<Device> devices)
    {
        foreach (var device in devices)
            UpdateDevice(device);
    }

    /// <summary>
    /// 创建设备线程
    /// </summary>
    /// <param name="device"></param>
    public void CreateDeviceThread(Device device)
    {
        try
        {
            _logger.LogInformation("开始创建设备 {device} 线程", device.DeviceName);

            using (var dataContext = new DataContext(_connectSetting, _dbType))
            {
                var systemManage = dataContext
                    .Set<SystemConfig>()
                    .FirstOrDefault();
                var driver = DriverService.DriverInfos
                    .SingleOrDefault(x => x.Type?.FullName == device.Driver.AssembleName);
                if (driver is null)
                    _logger.LogError("找不到设备:[{device}]的驱动:[{driver}]", device.DeviceName, device.Driver.AssembleName);
                else
                {
                    var settingList = dataContext.Set<DeviceConfig>()
                        .Where(x => x.DeviceId == device.ID)
                        .AsNoTracking()
                        .ToList();

                    var types = new[] { typeof(string), typeof(ILogger) };
                    var param = new object[] { device.DeviceName, _logger };

                    // 获取构造函数 (string deviceName, ILogger logger)
                    var constructor = driver.Type?.GetConstructor(types);

                    // 能够正常创建对象再赋值
                    if (constructor?.Invoke(param) is IDriver driverObj && systemManage != null)
                    {
                        // 将设置中的值赋值到实例的字段中
                        var propList = driver.Type?.GetProperties();

                        if (propList is not null)
                        {
                            foreach (var prop in propList
                                         .Where(p => p.GetCustomAttribute(typeof(ConfigParameterAttribute)) is not null))
                            {
                                var setting = settingList
                                    .FirstOrDefault(x => x.DeviceConfigName == prop.Name);

                                if (setting == null)
                                    continue;

                                // 通过反射调用解析
                                var type = prop.PropertyType;
                                var settingVal = setting.Value;
                                object val = settingVal;

                                var tableType = type;
                                if (type.BaseType == typeof(Enum))
                                {
                                    tableType = typeof(Enum);
                                }

                                if (ParserTable.TryGetValue(tableType, out var parser))
                                {
                                    val = parser(type, settingVal);
                                }
                                else
                                {
                                    var parseMethod = type.GetMethod("Parse", new[] { typeof(string) });
                                    if (parseMethod != null)
                                    {
                                        val = parseMethod.Invoke(null, new object[] { settingVal }) ?? settingVal;
                                    }
                                }

                                // 设置字段的值
                                prop.SetValue(driverObj, val);
                            }
                        }

                        // 将线程添加到线程池
                        DeviceThreads.Add(new(
                            device,
                            driverObj,
                            systemManage.GatewayName,
                            _mqttClient,
                            _mqttServer, _logger));
                    }
                }
            }

            _logger.LogInformation("完成创建设备 {device} 线程", device.DeviceName);
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "创建设备 {device} 线程错误", device.DeviceName);
        }
    }

    /// <summary>
    /// 创建多个设备线程
    /// </summary>
    /// <param name="devices"></param>
    public void CreateDeviceThreads(List<Device> devices)
    {
        foreach (var device in devices)
            CreateDeviceThread(device);
    }

    /// <summary>
    /// 删除设备线程
    /// </summary>
    /// <param name="devices"></param>
    public void RemoveDeviceThread(Device devices)
    {
        // 查找设备线程
        var deviceThread = DeviceThreads
            .FirstOrDefault(x => x.Device.ID == devices.ID);
        if (deviceThread == null) return;

        // 停止并销毁设备线程
        deviceThread.StopThread();
        deviceThread.Dispose();
        DeviceThreads.Remove(deviceThread);
    }

    /// <summary>
    /// 删除多个设备线程
    /// </summary>
    /// <param name="devices"></param>
    public void RemoveDeviceThreads(List<Device> devices)
    {
        foreach (var device in devices)
            RemoveDeviceThread(device);
    }

    /// <summary>
    /// 获取设备驱动（选择框）
    /// </summary>
    /// <param name="deviceId"></param>
    /// <returns></returns>
    public List<ComboSelectListItem> GetDriverMethods(Guid? deviceId)
    {
        List<ComboSelectListItem>? driverFilesComboSelect = null;

        try
        {
            _logger.LogInformation("开始获取驱动{deviceId}方法", deviceId);

            driverFilesComboSelect = DeviceThreads
                .FirstOrDefault(x => x.Device.ID == deviceId)
                ?.Methods
                ?.Select(m => new ComboSelectListItem()
                {
                    Text = m.Name,
                    Value = m.Name
                })
                .ToList();

            _logger.LogInformation("结束获取驱动{deviceId}方法，总计{cnt}", deviceId, driverFilesComboSelect?.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取驱动{deviceId}方法错误", deviceId);
        }

        return driverFilesComboSelect ?? new List<ComboSelectListItem>();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _logger.LogInformation("Dispose");
    }

    public static Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public static Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
