using System.Text.Json.Serialization;
using DevicesSimulator.Attributes;

// ReSharper disable InconsistentNaming

namespace DevicesSimulator.Models;

public class InertialNavigationData
{
    [JsonIgnore]
    public ushort Header => 0x9966;

    [DeviceField(1)]
    [DeviceContentLength]
    public byte Length { get; set; }

    /// <summary>
    /// 机体X轴陀螺角速度
    /// </summary>
    [DeviceField(2)]
    public int GyrosAngleRateX { get; set; }

    /// <summary>
    /// 机体Y轴陀螺角速度
    /// </summary>
    [DeviceField(3)]
    public int GyrosAngleRateY { get; set; }

    /// <summary>
    /// 机体Z轴陀螺角速度
    /// </summary>
    [DeviceField(4)]
    public int GyrosAngleRateZ { get; set; }

    /// <summary>
    /// 机体X轴加速度
    /// </summary>
    [DeviceField(5)]
    public int AccelerationX { get; set; }

    /// <summary>
    /// 机体Y轴加速度
    /// </summary>
    [DeviceField(6)]
    public int AccelerationY { get; set; }

    /// <summary>
    /// 机体Z轴加速度
    /// </summary>
    [DeviceField(7)]
    public int AccelerationZ { get; set; }

    /// <summary>
    /// 系统温度
    /// </summary>
    [DeviceField(8)]
    public short SystemTemperature { get; set; }

    /// <summary>
    /// INS状态字
    /// </summary>
    public StatusOfINS StatusOfINS { get; set; }

    [JsonIgnore]
    [DeviceField(9)]
    public byte ByteOfStatusINS
    {
        get => StatusOfINS.ToByte();
        set => StatusOfINS.FromByte(value);
    }

    /// <summary>
    /// INS故障字
    /// </summary>
    [DeviceField(10)]
    public byte ByteOfErrorINS { get; set; }


    [JsonIgnore] private readonly DateTime _now = DateTime.Now;

    /// <summary>
    /// 年
    /// </summary>
    [JsonIgnore]
    [DeviceField(11)]
    public ushort Year
    {
        get => (ushort)(_now.Year - 2000);
        set => _ = value;   // 无法为年赋值
    }

    /// <summary>
    /// 月
    /// </summary>
    [JsonIgnore]
    [DeviceField(12)]
    public byte Month
    {
        get => (byte)_now.Month;
        set => _ = value;
    }

    /// <summary>
    /// 日
    /// </summary>
    [JsonIgnore]
    [DeviceField(13)]
    public byte Day
    {
        get => (byte)_now.Day;
        set => _ = value;
    }

    [JsonIgnore]
    [DeviceField(14)]
    public uint MillisecondInDay
    {
        get =>
            (uint)(TimeSpan.FromHours(_now.Hour) +
                   TimeSpan.FromMinutes(_now.Minute) +
                   TimeSpan.FromSeconds(_now.Second) +
                   TimeSpan.FromMilliseconds(_now.Millisecond)).TotalMilliseconds;
        set => _ = value;
    }

    /// <summary>
    /// 经度
    /// </summary>
    [DeviceField(15)]
    public float Longitude { get; set; }

    /// <summary>
    /// 纬度
    /// </summary>
    [DeviceField(16)]
    public float Latitude { get; set; }

    /// <summary>
    /// 高度
    /// </summary>
    [DeviceField(17)]
    public int Altitude { get; set; }

    /// <summary>
    /// 北向速度
    /// </summary>
    [DeviceField(18)]
    public int NorthboundSpeed { get; set; }

    /// <summary>
    /// 天向速度
    /// </summary>
    [DeviceField(19)]
    public int SkySpeed { get; set; }

    /// <summary>
    /// 东向速度
    /// </summary>
    [DeviceField(20)]
    public int EastboundSpeed { get; set; }

    /// <summary>
    /// 横滚角
    /// </summary>
    [DeviceField(21)]
    public int RollAngle { get; set; }

    /// <summary>
    /// 航向角
    /// </summary>
    [DeviceField(22)]
    public int HeadingAngle { get; set; }

    /// <summary>
    /// 俯仰角
    /// </summary>
    [DeviceField(23)]
    public int PitchAngle { get; set; }

    /// <summary>
    /// 卫星纬度
    /// </summary>
    [DeviceField(24)]
    public float SatelliteLatitude { get; set; }

    /// <summary>
    /// 卫星经度
    /// </summary>
    [DeviceField(25)]
    public float SatelliteLongitude { get; set; }

    /// <summary>
    /// 卫星高度
    /// </summary>
    [DeviceField(26)]
    public short SatelliteAltitude { get; set; }

    /// <summary>
    /// 卫星北向速度
    /// </summary>
    [DeviceField(27)]
    public short SatelliteNorthboundSpeed { get; set; }

    /// <summary>
    /// 卫星天向速度
    /// </summary>
    [DeviceField(28)]
    public short SatelliteSkySpeed { get; set; }

    /// <summary>
    /// 卫星东向速度
    /// </summary>
    [DeviceField(29)]
    public short SatelliteEastboundSpeed { get; set; }

    /// <summary>
    /// BD状态字
    /// </summary>
    public StatusOfBD StatusOfBd { get; set; }

    [JsonIgnore]
    [DeviceField(30)]
    public byte ByteOfStatusDB
    {
        get => StatusOfBd.ToByte();
        set => StatusOfBd.FromByte(value);
    }

    /// <summary>
    /// GNSS状态字
    /// </summary>
    public StatusOfGNSS StatusOfGNSS { get; set; }

    [JsonIgnore]
    [DeviceField(31)]
    public byte ByteOfStatusGNSS
    {
        get => StatusOfGNSS.ToByte();
        set => StatusOfGNSS.FromByte(value);
    }
}
