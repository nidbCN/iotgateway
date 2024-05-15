using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PluginInterface;
using System.Reflection;
using WalkingTec.Mvvm.Core;
using IoTGateway.DataAccess;
using IoTGateway.Model;
using MQTTnet.Server;
using Microsoft.Extensions.Logging;

namespace Plugin
{
    public class DeviceService : IDisposable
    {
        private readonly ILogger<DeviceService> _logger;
        public DriverService DrvierManager;
        private static readonly Dictionary<Type, Func<Type, string, object>> ParserTable
            = new(){
                { typeof(bool), (t, val) => val != "0" },
                { typeof(string), (t,val) => val },
                { typeof(Enum), (t, val) => Enum.Parse(t, val) },
            };

        public List<DeviceThread> DeviceThreads = new();
        private readonly MyMqttClient _myMqttClient;
        private readonly MqttServer _mqttServer;
        private readonly string _connnectSetting = IoTBackgroundService.connnectSetting;
        private readonly DBTypeEnum _dbType = IoTBackgroundService.DbType;

        public DeviceService(IConfiguration configRoot, DriverService drvierManager, MyMqttClient myMqttClient,
            MqttServer mqttServer, ILogger<DeviceService> logger)
        {
            _logger = logger;
            DrvierManager = drvierManager;
            _myMqttClient = myMqttClient;
            _mqttServer = mqttServer ?? throw new ArgumentNullException(nameof(mqttServer));
            
            try
            {
                using var dataContext = new DataContext(_connnectSetting, _dbType);
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
                _logger.LogInformation("UpdateDevice Start:{device}", device.DeviceName);
                RemoveDeviceThread(device);
                CreateDeviceThread(device);
                _logger.LogInformation("UpdateDevice End:{device}", device.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateDevice Error:{device}", device.DeviceName);
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
                _logger.LogInformation("CreateDeviceThread Start:{device}", device.DeviceName);
                using (var dataContext = new DataContext(_connnectSetting, _dbType))
                {
                    var systemManage = dataContext.Set<SystemConfig>().FirstOrDefault();
                    var driver = DrvierManager.DriverInfos
                        .SingleOrDefault(x => x.Type?.FullName == device.Driver.AssembleName);
                    if (driver is null)
                        _logger.LogError("找不到设备:[{device}]的驱动:[{driver}]", device.DeviceName, device.Driver.AssembleName);
                    else
                    {
                        var settingList = dataContext.Set<DeviceConfig>()
                            .Where(x => x.DeviceId == device.ID)
                            .AsNoTracking()
                            .ToList();

                        var types = new Type[] { typeof(string), typeof(ILogger) };
                        var param = new object[] { device.DeviceName, _logger };

                        var constructor = driver.Type?.GetConstructor(types);
                        var deviceObj = constructor?.Invoke(param) as IDriver;

                        // 将设置中的值赋值到实例的字段中
                        var propList = driver.Type?.GetProperties();

                        if (propList is not null)
                        {
                            foreach (var prop in propList)
                            {
                                var config = prop.GetCustomAttribute(typeof(ConfigParameterAttribute));
                                var setting = settingList
                                    .FirstOrDefault(x => x.DeviceConfigName == prop.Name);

                                if (config == null || setting == null)
                                    continue;

                                // 通过反射调用解析
                                var type = prop.PropertyType;
                                var settingVal = setting.Value;
                                object val = settingVal;

                                if (ParserTable.TryGetValue(type, out var parser))
                                {
                                    val = parser(type, settingVal);
                                }

                                var parseMethod = type.GetMethod("Parse", new[] { typeof(string) });
                                if (parseMethod != null)
                                {
                                    val = parseMethod.Invoke(null, new object[] { settingVal }) ?? settingVal;
                                }

                                prop.SetValue(deviceObj, val);
                            }
                        }

                        if (deviceObj != null && systemManage != null)
                        {
                            var deviceThread = new DeviceThread(device, deviceObj, systemManage.GatewayName,
                                _myMqttClient,
                                _mqttServer, _logger);
                            DeviceThreads.Add(deviceThread);
                        }
                    }
                }

                _logger.LogInformation("CreateDeviceThread End:{device}", device.DeviceName);
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "CreateDeviceThread Error:{device}", device.DeviceName);
            }
        }

        /// <summary>
        /// 创建多个设备线程
        /// </summary>
        /// <param name="devices"></param>
        public void CreateDeviceThreads(List<Device> devices)
        {
            foreach (Device device in devices)
                CreateDeviceThread(device);
        }

        /// <summary>
        /// 删除设备线程
        /// </summary>
        /// <param name="devices"></param>
        public void RemoveDeviceThread(Device devices)
        {
            var deviceThread = DeviceThreads
                .FirstOrDefault(x => x.Device.ID == devices.ID);
            if (deviceThread != null)
            {
                deviceThread.StopThread();
                deviceThread.Dispose();
                DeviceThreads.Remove(deviceThread);
            }
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
            var driverFilesComboSelect = new List<ComboSelectListItem>();
            try
            {
                _logger.LogInformation("GetDriverMethods Start:{deviceId}", deviceId);
                var methodInfos = DeviceThreads
                    .FirstOrDefault(x => x.Device.ID == deviceId)
                    ?.Methods
                    ?.Select(m => new ComboSelectListItem()
                    {
                        Text = m.Name,
                        Value = m.Name
                    })
                    .ToList();

                _logger.LogInformation("GetDriverMethods End:{deviceId}, Count:{cnt}", deviceId, methodInfos?.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDriverMethods Error:{deviceId}", deviceId);
            }

            return driverFilesComboSelect;
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
}