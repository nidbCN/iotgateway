using Microsoft.Extensions.Logging;
using PluginInterface;
using System.IO.Ports;
using System.Text;

namespace Other.UART;

[DriverSupported("UART")]
[DriverInfo("UART", "V1.0.0", "Copyleft gaein.cn")]
public class DeviceSerialPort : IDriver
{
    #region 配置参数
    [ConfigParameter("设备Id")]
    public string DeviceId { get; set; }

    [ConfigParameter("串口名")]
    public string PortName { get; set; } = "/dev/ttyS0";

    [ConfigParameter("波特率")]
    public int BaudRate { get; set; } = 9600;

    [ConfigParameter("数据位")]
    public int DataBits { get; set; } = 8;

    [ConfigParameter("校验位")]
    public Parity Parity { get; set; } = Parity.None;

    [ConfigParameter("停止位")]
    public StopBits StopBits { get; set; } = StopBits.One;

    [ConfigParameter("超时时间ms")]
    public int Timeout { get; set; } = 500;

    [ConfigParameter("最小通讯周期ms")]
    public uint MinPeriod { get; set; } = 3000;

    #endregion



    public ILogger _logger { get; set; }

    private readonly string _device;

    private byte[] _lastRecv;

    public DeviceSerialPort(string device, ILogger logger)
    {
        _logger = logger;
        _device = device;
    }

    private SerialPort? _serialPort;

    public bool IsConnected => _serialPort != null && _serialPort.IsOpen;


    public bool Close()
    {
        _serialPort?.Close();
        return true;
    }

    public bool Connect()
    {
        try
        {
            _serialPort = new SerialPort(PortName, BaudRate, Parity, DataBits);
            _serialPort.Open();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Can not connect {port}", PortName);
            return false;
        }

        return true;
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _serialPort?.Dispose();
        _serialPort = null;
    }

    [Method("读串口设备数据", description: "读取数据")]
    public DriverReturnValueModel Read(DriverAddressIoArgModel ioArg)
    {
        var ret = new DriverReturnValueModel { StatusType = VaribaleStatusTypeEnum.Good };

        if (IsConnected)
        {
            try
            {
                var len = _serialPort!.BytesToRead;

                if (len != 0)
                {
                    var buffer = new byte[len];
                    var _ = _serialPort.Read(buffer, 0, len);
                    _lastRecv = buffer;
                }

                if (_lastRecv is { })
                {

                    switch (ioArg.ValueType)
                    {
                        case DataTypeEnum.AsciiString:
                            ret.Value = Encoding.ASCII.GetString(_lastRecv);
                            break;

                        case DataTypeEnum.Int32:
                            ret.Value = Convert.ToInt32(_lastRecv);
                            break;
                    }
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e, "Device:[{dev}],Connect(),Error: {m}", _device, e.Message);
                ret.StatusType = VaribaleStatusTypeEnum.Bad;
                ret.Message = e.Message;
            }
        }
        else
        {
            ret.StatusType = VaribaleStatusTypeEnum.Bad;
        }

        return ret;
    }

    [Method("写串口设备数据", description: "写入数据")]
    public async Task<RpcResponse> WriteAsync(string RequestId, string Method, DriverAddressIoArgModel ioArg)
    {
        var resp = new RpcResponse();
        if (IsConnected)
        {
            try
            {
                using var stream = _serialPort!.BaseStream;

                byte[] data = null;

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
                    await stream.WriteAsync(data);
                }
            }
            catch (Exception e)
            {
                resp.IsSuccess = false;
            }
        }

        resp.IsSuccess = false;
        return resp;
    }
}
