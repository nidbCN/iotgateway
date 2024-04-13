using Microsoft.Extensions.Logging;
using PluginInterface;
using SocketCANSharp;
using SocketCANSharp.Network;
using System.Text;

namespace Other.CAN;

[DriverSupported("CAN")]
[DriverInfo("CAN", "V1.0.0", "Copyleft gaein.cn")]
public class DeviceCAN : IDriver
{
    public bool IsConnected => _socket is not null;

    public ILogger _logger { get; set; }

    #region 配置字段

    [ConfigParameter("设备Id")]
    public string DeviceId { get; set; }

    [ConfigParameter("超时时间ms")]
    public int Timeout { get; set; } = 300;

    [ConfigParameter("最小通讯周期ms")]
    public uint MinPeriod { get; set; } = 3000;

    [ConfigParameter("总线接口")]
    public string Interface { get; set; } = "can0";

    #endregion

    #region 私有字段

    private RawCanSocket? _socket;

    #endregion

    public bool Connect()
    {
        try
        {
            _socket = new RawCanSocket();
            var canInterface = CanNetworkInterface
                .GetAllInterfaces(true)
                .First(i => i.Name == Interface);
            _socket.Bind(canInterface);
        }
        catch (Exception ex)
        {
            _socket = null;
            _logger.LogError(ex, "Device:[{dev}],Connect()", DeviceId);
        }

        return IsConnected;
    }

    public bool Close()
    {
        _socket?.Close();
        return true;
    }

    public void Dispose()
    {
        _socket?.Dispose();
        GC.SuppressFinalize(this);
    }

    public DriverReturnValueModel Read(DriverAddressIoArgModel ioArg)
    {
        if (!IsConnected)
            return new DriverReturnValueModel
            {
                StatusType = VaribaleStatusTypeEnum.Bad
            };

        var ret = new DriverReturnValueModel()
        {
            StatusType = VaribaleStatusTypeEnum.Good
        };

        _socket!.Read(out CanFrame frame);

        switch (ioArg.ValueType)
        {
            case DataTypeEnum.Bool:
                ret.Value = frame.Data[0] == 1;
                break;
            case DataTypeEnum.Byte:
                ret.Value = frame.Data[0];
                break;
            case DataTypeEnum.Int16:
                ret.Value = BitConverter.ToInt16(frame.Data);
                break;
            case DataTypeEnum.Int32:
                ret.Value = BitConverter.ToInt32(frame.Data);
                break;
            case DataTypeEnum.Int64:
                ret.Value = BitConverter.ToInt64(frame.Data);
                break;
            case DataTypeEnum.AsciiString:
                ret.Value = Encoding.ASCII.GetString(frame.Data);
                break;
        }

        return ret;
    }

    public async Task<RpcResponse> WriteAsync(string RequestId, string Method, DriverAddressIoArgModel ioArg)
    {
        var resp = new RpcResponse();
        try
        {
            await Task.Run(() =>
            {
                _socket?.Write(new CanFrame()
                {
                    Data = null
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "write device {d} error.", DeviceId);
        }

        return resp;
    }

    public DeviceCAN(string device, ILogger logger)
    {
        _logger = logger;
    }
}
