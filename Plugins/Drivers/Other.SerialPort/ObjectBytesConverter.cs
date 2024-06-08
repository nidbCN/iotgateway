using System.Reflection;
using System.Runtime.InteropServices;
using DevicesSimulator.Attributes;

namespace DevicesSimulator;

public class ObjectBytesConverter
{
    private static readonly Dictionary<Type, Func<object, byte[]>> CastTable = new()
    {
        { typeof(short), o => BitConverter.GetBytes((short)o) },
        { typeof(ushort), o => BitConverter.GetBytes((ushort)o) },
        { typeof(int), o => BitConverter.GetBytes((int)o) },
        { typeof(uint), o => BitConverter.GetBytes((uint)o) },
        { typeof(float), o => BitConverter.GetBytes((float)o) },
    };

    private static readonly Dictionary<Type, Func<byte[], object>> ReserveCastTable = new()
    {
        {typeof(byte), b=> b[0]},
        { typeof(short), b => BitConverter.ToInt16(b) },
        { typeof(ushort), b => BitConverter.ToUInt16(b) },
        { typeof(int), b => BitConverter.ToInt16(b) },
        { typeof(uint), b => BitConverter.ToUInt16(b) },
        { typeof(float), b => BitConverter.ToSingle(b) },
    };

    public static T ToObject<T>(byte[] data) where T : new()
    {
        ArgumentNullException.ThrowIfNull(data);
        var dataSpan = data.AsSpan();
        var obj = new T();

        var modelType = typeof(T);

        //var length = data.Length;
        //var lengthProp = modelType
        //    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        //    .FirstOrDefault(p => Attribute.IsDefined(p, typeof(DeviceContentLength)));

        var props = modelType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => Attribute.IsDefined(p, typeof(DeviceField)))
            .OrderBy(p => p.GetCustomAttribute<DeviceField>()!.Order)
            .ToArray();

        var offset = 0;
        foreach (var p in props)
        {
            var fieldSize = Marshal.SizeOf(p.PropertyType);
            var propData = dataSpan[offset..(offset + fieldSize)];
            offset += fieldSize;

            if (ReserveCastTable.TryGetValue(p.PropertyType, out var func))
            {
                try
                {
                    var propVal = func.Invoke(propData.ToArray());
                    p.SetValue(obj, propVal);
                }
                catch (ArgumentException e)
                {
                    Console.WriteLine($"设置字段 {p.Name} 错误，{e}");
                }

            }
            else
            {
                throw new NotSupportedException($"不支持的属性类型 {p.PropertyType}");
            }
        }

        return obj;
    }

    public static byte[] ToBytes<T>(T obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        var modelType = typeof(T);

        var lengthProp = modelType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(p => Attribute.IsDefined(p, typeof(DeviceContentLength)));

        var props = modelType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => Attribute.IsDefined(p, typeof(DeviceField)))
            .OrderBy(p => p.GetCustomAttribute<DeviceField>()!.Order)
            .ToArray();

        var filedLength = lengthProp?.GetValue(obj);

        var length = filedLength is null ?
            props.Sum(p => Marshal.SizeOf(p.PropertyType))
            : Convert.ToInt32(filedLength);

        var result = new byte[length];

        var offset = 0;
        foreach (var property in props)
        {
            var propValue = property.GetValue(obj) ?? 0;
            var propType = property.PropertyType;

            if (propType == typeof(byte))
            {
                result[offset] = (byte)propValue;
                offset++;
            }
            else
            {
                if (CastTable.TryGetValue(propType, out var func))
                {
                    var propertyBytes = func.Invoke(propValue);
                    Array.Reverse(propertyBytes);
                    Array.Copy(propertyBytes, 0, result, offset, propertyBytes.Length);
                    offset += propertyBytes.Length;
                }
                else
                {
                    throw new InvalidOperationException($"不支持的属性类型 {propType}");
                }
            }
        }

        return result;
    }
}
