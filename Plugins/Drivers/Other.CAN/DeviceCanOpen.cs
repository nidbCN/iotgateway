using Microsoft.Extensions.Logging;
using PluginInterface;
using SocketCANSharp;
using SocketCANSharp.Network;
using System.Text;

namespace Other.CAN;

[DriverSupported("CanOpen")]
[DriverInfo("CanOpen", "V1.0.0", "Copyleft gaein.cn")]
public class DeviceCanOpen : IDriver
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
            case DataTypeEnum.Any:
                ret.Value = frame.Data;
                break;
        }

        return ret;
    }

    public async Task<RpcResponse> WriteAsync(string RequestId, string Method, DriverAddressIoArgModel ioArg)
    {
        var resp = new RpcResponse();
        try
        {
            byte[]? data = null;

            switch (ioArg.ValueType)
            {
                case DataTypeEnum.Bit:
                    break;
                case DataTypeEnum.Bool:
                    break;
                case DataTypeEnum.UByte:
                    break;
                case DataTypeEnum.Byte:
                    data = new byte[] { (byte)ioArg.Value };
                    break;
                case DataTypeEnum.Uint16:
                    data = BitConverter.GetBytes((ushort)ioArg.Value);
                    break;
                case DataTypeEnum.Int16:
                    data = BitConverter.GetBytes((short)ioArg.Value);
                    break;
                case DataTypeEnum.Bcd16:
                    break;
                case DataTypeEnum.Uint32:
                    data = BitConverter.GetBytes((uint)ioArg.Value);
                    break;
                case DataTypeEnum.Int32:
                    data = BitConverter.GetBytes((int)ioArg.Value);
                    break;
                case DataTypeEnum.Float:
                    data = BitConverter.GetBytes((float)ioArg.Value);
                    break;
                case DataTypeEnum.Bcd32:
                    break;
                case DataTypeEnum.Uint64:
                    data = BitConverter.GetBytes((ulong)ioArg.Value);
                    break;
                case DataTypeEnum.Int64:
                    data = BitConverter.GetBytes((long)ioArg.Value);
                    break;
                case DataTypeEnum.Double:
                    data = BitConverter.GetBytes((double)ioArg.Value);
                    break;
                case DataTypeEnum.AsciiString:
                    data = Encoding.ASCII.GetBytes((string)ioArg.Value);
                    break;
                case DataTypeEnum.Utf8String:
                    data = Encoding.UTF8.GetBytes((string)ioArg.Value);
                    break;
                case DataTypeEnum.DateTime:
                    break;
                case DataTypeEnum.TimeStampMs:
                    break;
                case DataTypeEnum.TimeStampS:
                    break;
                case DataTypeEnum.Any:
                    break;
                case DataTypeEnum.Custome1:
                    break;
                case DataTypeEnum.Custome2:
                    break;
                case DataTypeEnum.Custome3:
                    break;
                case DataTypeEnum.Custome4:
                    break;
                case DataTypeEnum.Custome5:
                    break;
                case DataTypeEnum.Gb2312String:
                    break;
                default:
                    break;
            }

            if (data is null)
            {
                resp.IsSuccess = false;
            }
            else
            {
                await Task.Run(() =>
                {
                    _socket?.Write(new CanFrame()
                    {
                        Data = data
                    });
                });
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "write device {d} error.", DeviceId);
        }

        return resp;
    }

    public DeviceCanOpen(string device, ILogger logger)
    {
        _logger = logger;
    }
}
