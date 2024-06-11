using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PluginInterface;
using System.Reflection;
using System.Text.Json;
using WalkingTec.Mvvm.Core;
using IoTGateway.DataAccess;
using IoTGateway.Model;
using Microsoft.Extensions.Logging;
using static NPOI.HSSF.UserModel.HeaderFooter;

namespace Plugin
{
    public class DriverService
    {
        private readonly ILogger<DriverService> _logger;
        readonly string _driverPath = Path
            .Combine(AppDomain.CurrentDomain.BaseDirectory, @"drivers/net6.0");
        readonly string[] _driverFiles;
        public List<DriverInfo> DriverInfos = new();

        public DriverService(IConfiguration configRoot, ILogger<DriverService> logger)
        {
            _logger = logger;
            try
            {
                _logger.LogInformation("加载驱动文件开始");
                _driverFiles = Directory
                    .GetFiles(_driverPath)
                    .Where(x => Path.GetExtension(x) == ".dll")
                    .ToArray();

                _logger.LogInformation("加载驱动文件结束，共{cnt}", _driverFiles.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载驱动文件错误");
            }

            LoadAllDrivers();
        }

        public List<ComboSelectListItem> GetAllDrivers()
        {
            var driverFilesComboSelect = new List<ComboSelectListItem>();
            using var dc =
                new DataContext(IoTBackgroundService.connnectSetting, IoTBackgroundService.DbType);
            var drivers = dc.Set<Driver>()
                .AsNoTracking()
                .ToList();

            foreach (var file in _driverFiles)
            {
                var dll = Assembly.LoadFrom(file);
                if (!dll.GetTypes().Any(x => typeof(IDriver).IsAssignableFrom(x) && x.IsClass)) continue;

                var fileName = Path.GetFileName(file);
                var item = new ComboSelectListItem
                {
                    Text = fileName,
                    Value = fileName,
                    Disabled = false,
                };

                if (drivers.Any(x => x.FileName == Path.GetFileName(file)))
                    item.Disabled = true;
                driverFilesComboSelect.Add(item);

            }

            return driverFilesComboSelect;
        }

        public string GetAssembleNameByFileName(string fileName)
        {
            var file = _driverFiles.SingleOrDefault(f => Path.GetFileName(f) == fileName);
            var dll = Assembly.LoadFrom(file);
            var type = dll.GetTypes().FirstOrDefault(x => typeof(IDriver).IsAssignableFrom(x) && x.IsClass);
            return type?.FullName;
        }

        public void AddConfigs(Guid? dapId, Guid? driverId)
        {
            using var dc =
                new DataContext(IoTBackgroundService.connnectSetting, IoTBackgroundService.DbType);
            var device = dc
                .Set<Device>()
                .Where(x => x.ID == dapId)
                .AsNoTracking()
                .SingleOrDefault();
            var driver = dc
                .Set<Driver>()
                .Where(x => x.ID == driverId)
                .AsNoTracking()
                .SingleOrDefault();
            var type = DriverInfos
                .SingleOrDefault(x => x.Type?.FullName == driver?.AssembleName);

            Type[] argTypes = { typeof(string), typeof(ILogger) };
            object[] param = { device?.DeviceName!, _logger };

            if (type?.Type is null)
                return;

            var constructor = type.Type.GetConstructor(argTypes);
            var iObj = constructor?.Invoke(param) as IDriver;

            // 遍历驱动中的属性
            foreach (var property in type.Type.GetProperties())
            {
                // 获取驱动中的配置用字段
                var config = property
                    .GetCustomAttribute(typeof(ConfigParameterAttribute));

                if (config is null) continue;

                // 实例化数据库中存储用的模型类
                var isEnum = property.PropertyType.BaseType == typeof(Enum);

                dc.Set<DeviceConfig>().Add(new()
                {
                    ID = Guid.NewGuid(),
                    DeviceId = dapId,
                    DeviceConfigName = property.Name,
                    DataSide = DataSide.AnySide,
                    Description = ((ConfigParameterAttribute)config).Description,
                    Value = property.GetValue(iObj)?.ToString(),
                    EnumInfo = isEnum ? JsonSerializer.Serialize(property.PropertyType
                        .GetFields(BindingFlags.Static | BindingFlags.Public)
                        .ToDictionary(f => f.Name, f => (int)(f.GetValue(null) ?? 0))) : string.Empty
                });
            }

            dc.SaveChanges();
        }

        public void LoadAllDrivers()
        {
            _logger.LogInformation("开始加载全部驱动");
            Parallel.ForEach(_driverFiles, file =>
            {
                try
                {
                    var dll = Assembly.LoadFrom(file);

                    // 筛选出 IDriver 类
                    foreach (var type in dll.GetTypes()
                                 .Where(x => typeof(IDriver).IsAssignableFrom(x) && x.IsClass))
                    {
                        // 创建 DriverInfo 并添加
                        var driverInfo = new DriverInfo
                        {
                            FileName = Path.GetFileName(file),
                            Type = type
                        };
                        DriverInfos.Add(driverInfo);

                        _logger.LogInformation("加载`{f}`的全部驱动完成", driverInfo.FileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "加载`{f}`的全部驱动出错", file);
                }
            });

            _logger.LogInformation("加载全部驱动完成，总共{cnt}", DriverInfos.Count);
        }

        public void LoadRegestedDeviers()
        {
            using var dc = new DataContext(IoTBackgroundService.connnectSetting, IoTBackgroundService.DbType);

            var path = Path
                .Combine(AppDomain.CurrentDomain.BaseDirectory, @"drivers/net6.0");
            var files = Directory
                .GetFiles(path)
                .Where(x => Path.GetExtension(x) == ".dll");

            foreach (var file in files)
            {
                var dll = Assembly.LoadFrom(file);
                foreach (var type in dll.GetTypes().Where(x => typeof(IDriver).IsAssignableFrom(x) && x.IsClass))
                {
                    var driverInfo = new DriverInfo
                    {
                        FileName = Path.GetFileName(file),
                        Type = type
                    };
                    DriverInfos.Add(driverInfo);
                }
            }
        }
    }
}