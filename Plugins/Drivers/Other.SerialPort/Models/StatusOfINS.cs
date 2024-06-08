using System.Text.Json.Serialization;

namespace DevicesSimulator.Models;

/// <summary>
/// 光纤惯导 INS 工作状态
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatusOfINSEnum
{
    INS = 0b000_000_0_0,
    INS_BD2 = 0b001_000_0_0,
    INS_GNSS = 0b010_000_0_0,
    DB2 = 0b011_000_0_0,
    GNSS = 0b100_000_0_0,
}

/// <summary>
/// INS 数据无效/有效
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidOfINSEnum
{
    INVALID = 0b000_000_0_0,
    VALID = 0b000_000_1_0,
}

/// <summary>
/// INS 对准/导航
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NavigationOfINSEnum
{
    ALIGNMENT = 0b000_000_0_0,
    NAVIGATION = 0b000_000_0_1,
}

/// <summary>
/// INS 工作状态字，参考表 2-1 光纤惯导 INS 工作状态字定义
/// </summary>
public struct StatusOfINS
{
    public StatusOfINSEnum Status { get; set; }
    public ValidOfINSEnum Valid { get; set; }
    public NavigationOfINSEnum Navigation { get; set; }

    public byte ToByte() => (byte)((int)Status | (int)Valid | (int)Navigation);

    public void FromByte(byte bin)
    {
        Status = (StatusOfINSEnum)(bin & 0b11100000);
        Valid = (ValidOfINSEnum)(bin & 0b00000010);
        Navigation = (NavigationOfINSEnum)(bin & 0b00000001);
    }
}
