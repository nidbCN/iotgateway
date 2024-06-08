using DevicesSimulator.Models;
using DevicesSimulator;
using Microsoft.Extensions.Logging;
using PluginInterface;
using System.IO.Ports;
using System.Text;
using System.Text.Json;

namespace Other.UART;

[DriverSupported("UART")]
[DriverInfo("UART", "V1.0.0", "Copyleft gaein.cn")]
public class DeviceSerialPort : IDriver
{
    #region 配置参数

    [ConfigParameter("设备Id")]
    public string DeviceId { get; set; }

    [ConfigParameter("串口名")]
    public string PortName { get; set; } = "/dev/ttySC0";

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

    private byte[]? _lastRec;

    public DeviceSerialPort(string device, ILogger logger)
    {
        _logger = logger;
        _device = device;
    }

    private SerialPort? _serialPort;

    public bool IsConnected => _serialPort is { IsOpen: true };

    public bool Close()
    {
        _serialPort?.Close();
        return true;
    }

    public bool Connect()
    {
        try
        {
            _serialPort = new(PortName, BaudRate, Parity, DataBits);
            _serialPort.Open();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Can not connect {port}", PortName);
            _serialPort = null;
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
                    _ = _serialPort.Read(buffer, 0, len);
                    _lastRec = buffer;
                }

                if (_lastRec is not null)
                {
                    ret.Value = ConvertToValue(ioArg.ValueType, _lastRec);
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
            ret.Message = "Not connected.";
        }

        return ret;
    }

    private static object ConvertToValue(DataTypeEnum type, byte[] data)
        => type switch
        {
            DataTypeEnum.Uint64 => BitConverter.ToUInt64(data),
            DataTypeEnum.Int64 => BitConverter.ToInt64(data),
            DataTypeEnum.Uint32 => BitConverter.ToUInt32(data),
            DataTypeEnum.Int32 => BitConverter.ToInt32(data),
            DataTypeEnum.Uint16 => BitConverter.ToUInt16(data),
            DataTypeEnum.Int16 => BitConverter.ToInt16(data),
            DataTypeEnum.UByte => Convert.ToByte(data),
            DataTypeEnum.Byte => Convert.ToSByte(data),
            DataTypeEnum.Bool => BitConverter.ToBoolean(data),
            DataTypeEnum.DateTime => Convert.ToDateTime(data),
            DataTypeEnum.AsciiString => Encoding.ASCII.GetString(data),
            DataTypeEnum.Utf8String => Encoding.UTF8.GetString(data),
            DataTypeEnum.Float => BitConverter.ToSingle(data),
            DataTypeEnum.Double => BitConverter.ToDouble(data),
            _ => Convert.ToHexString(data),
        };

    [Method("写串口设备数据", description: "写入数据")]
    public async Task<RpcResponse> WriteAsync(string RequestId, string Method, DriverAddressIoArgModel ioArg)
    {
        var resp = new RpcResponse() { IsSuccess = false };

        if (!IsConnected) return resp;

        if (ioArg.ValueType != DataTypeEnum.AsciiString) return resp;

        var obj = JsonSerializer
            .Deserialize<InertialNavigationData>((string)ioArg.Value);

        var bytes = ObjectBytesConverter.ToBytes(obj);

        var checkSum = (byte)bytes.Sum(b => b);

        var result = new byte[bytes.Length + 3];

        var header = BitConverter.GetBytes(obj.Header);
        (result[0], result[1]) = (header[0], header[1]);

        Array.Copy(bytes, 0, result, 2, bytes.Length);
        result[bytes.Length + 2] = checkSum;

        try
        {
            await using var stream = _serialPort!.BaseStream;
            await stream.WriteAsync(result);
            resp.IsSuccess = true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Device [{dev}] send failed, {m}", _device, e.Message);
        }

        return resp;
    }
}