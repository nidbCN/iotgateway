using System.Text.Json.Serialization;

namespace DevicesSimulator.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidOfBDEnum
{
    INVALID = 0b0_0_0_0000,
    VALID = 0b1_0_0_0000,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LockOfBDEnum
{
    LOCATION_LOCKED = 0b0_1_0_0000,
    LOCATION_UNLOCKED = 0b0_0_0_0000,
}

/// <summary>
/// BD 工作状态字，参考表 2-2 BD 工作状态字定义
/// </summary>
public struct StatusOfBD
{
    public ValidOfBDEnum ValidOfBD { get; set; }
    public LockOfBDEnum LockOfBD { get; set; }

    /// <summary>
    /// 可见星数目（低四位有效）
    /// </summary>
    public byte NumberOfVisibleStars { get; set; }

    public byte ToByte()
        => (byte)((byte)ValidOfBD | (byte)LockOfBD
                                  | (NumberOfVisibleStars & 0b00001111));

    public void FromByte(byte bin)
    {
        ValidOfBD = (ValidOfBDEnum)(bin & 0b10000000);
        LockOfBD = (LockOfBDEnum)(bin & 0b01000000);
        NumberOfVisibleStars = (byte)(bin & 0b11110000);
    }
}
