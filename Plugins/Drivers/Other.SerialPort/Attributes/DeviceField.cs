namespace DevicesSimulator.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
public class DeviceField : Attribute
{
    public int Order { get; }

    public DeviceField(int order)
    {
        Order = order;
    }
}
