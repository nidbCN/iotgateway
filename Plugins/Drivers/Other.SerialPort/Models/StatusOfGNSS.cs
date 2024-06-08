using System.Text.Json.Serialization;

namespace DevicesSimulator.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidOfGNSSEnum
{
    INVALID = 0b0_0_0_0000,
    VALID = 0b1_0_0_0000,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LockOfGNSSEnum
{
    LOCATION_LOCKED = 0b0_1_0_0000,
    LOCATION_UNLOCKED = 0b0_0_0_0000,
}

/// <summary>
/// GNSS 工作状态字，参考表 2-3 GNSS 工作状态字定义
/// </summary>
public struct StatusOfGNSS
{
    public ValidOfGNSSEnum ValidOfGNSS { get; set; }
    public LockOfGNSSEnum LockOfGNSS { get; set; }

    /// <summary>
    /// 可见星数目（低四位有效）
    /// </summary>
    public byte NumberOfVisibleStars { get; set; }

    public byte ToByte()
        => (byte)((byte)ValidOfGNSS | (byte)LockOfGNSS
                                  | (NumberOfVisibleStars & 0b00001111));

    public void FromByte(byte bin)
    {
        ValidOfGNSS = (ValidOfGNSSEnum)(bin & 0b10000000);
        LockOfGNSS = (LockOfGNSSEnum)(bin & 0b01000000);
    }
}
