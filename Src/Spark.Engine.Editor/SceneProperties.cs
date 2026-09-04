using System.Numerics;
using System.Reflection;
using Spark.Engine.Components;
using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

public enum ScenePropertyKind : byte
{
    Null = 0,
    Boolean = 1,
    Int64 = 2,
    UInt64 = 3,
    Single = 4,
    Double = 5,
    String = 6,
    Guid = 7,
    Vector2 = 8,
    Vector3 = 9,
    Vector4 = 10,
    Quaternion = 11,
    Matrix4x4 = 12,
    AssetReference = 13,
}

public sealed record ScenePropertyValue(ScenePropertyKind Kind, object? Value)
{
    public T Get<T>() => Value is T value
        ? value
        : throw new InvalidCastException($"Scene property {Kind} does not contain {typeof(T).Name}.");
}

internal static class ScenePropertySerializer
{
    public static Dictionary<string, ScenePropertyValue> Capture(ActorComponent component)
    {
        var result = new Dictionary<string, ScenePropertyValue>(StringComparer.Ordinal);
        foreach (var property in GetSceneProperties(component.GetType()))
        {
            var value = property.GetValue(component);
            result.Add(property.Name, value == null
                ? new ScenePropertyValue(ScenePropertyKind.Null, null)
                : FromValue(property.PropertyType, value));
        }
        return result;
    }

    public static void Restore(
        ActorComponent component,
        IReadOnlyDictionary<string, ScenePropertyValue> values,
        Func<Guid, Type, SceneResource> assetResolver)
    {
        foreach (var property in GetSceneProperties(component.GetType()))
        {
            if (!property.CanWrite || !values.TryGetValue(property.Name, out var encoded))
                continue;
            property.SetValue(component, ToValue(property.PropertyType, encoded, assetResolver));
        }
    }

    /// <summary>
    /// Actor 编辑器预览使用的恢复入口。恢复可独立解析的标量/变换属性，
    /// 暂时跳过需要 AssetRegistry 的引用字段，避免预览窗口因缺少运行时资源解析器而无法打开。
    /// </summary>
    internal static void RestorePreview(
        ActorComponent component,
        IReadOnlyDictionary<string, ScenePropertyValue> values)
    {
        foreach (var property in GetSceneProperties(component.GetType()))
        {
            if (!property.CanWrite || !values.TryGetValue(property.Name, out var encoded) ||
                encoded.Kind == ScenePropertyKind.AssetReference)
                continue;
            try
            {
                property.SetValue(component, ToValue(property.PropertyType, encoded,
                    static (_, _) => throw new InvalidOperationException("Asset references are not resolved in preview.")));
            }
            catch (InvalidOperationException) when (encoded.Kind == ScenePropertyKind.AssetReference)
            {
                // 仅跳过预览中无法解析的资源引用。
            }
        }
    }

    private static IEnumerable<PropertyInfo> GetSceneProperties(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetCustomAttribute<ScenePropertyAttribute>() != null &&
                               property.CanRead && property.GetIndexParameters().Length == 0)
            .OrderBy(property => property.Name, StringComparer.Ordinal);

    private static ScenePropertyValue FromValue(Type declaredType, object value)
    {
        var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (value is SceneResource resource)
            return new ScenePropertyValue(ScenePropertyKind.AssetReference, resource.AssetGuid);
        if (type.IsEnum)
            return new ScenePropertyValue(ScenePropertyKind.Int64, Convert.ToInt64(value));
        return value switch
        {
            bool typed => new(ScenePropertyKind.Boolean, typed),
            sbyte or short or int or long => new(ScenePropertyKind.Int64, Convert.ToInt64(value)),
            byte or ushort or uint or ulong => new(ScenePropertyKind.UInt64, Convert.ToUInt64(value)),
            float typed => new(ScenePropertyKind.Single, typed),
            double typed => new(ScenePropertyKind.Double, typed),
            string typed => new(ScenePropertyKind.String, typed),
            Guid typed => new(ScenePropertyKind.Guid, typed),
            Vector2 typed => new(ScenePropertyKind.Vector2, typed),
            Vector3 typed => new(ScenePropertyKind.Vector3, typed),
            Vector4 typed => new(ScenePropertyKind.Vector4, typed),
            Quaternion typed => new(ScenePropertyKind.Quaternion, typed),
            Matrix4x4 typed => new(ScenePropertyKind.Matrix4x4, typed),
            _ => throw new NotSupportedException(
                $"Scene property type '{declaredType.FullName}' is not supported.")
        };
    }

    private static object ToValue(
        Type declaredType,
        ScenePropertyValue encoded,
        Func<Guid, Type, SceneResource> assetResolver)
    {
        var type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (encoded.Kind == ScenePropertyKind.Null)
        {
            if (declaredType.IsValueType && Nullable.GetUnderlyingType(declaredType) == null)
                throw new InvalidDataException($"Scene property '{declaredType.FullName}' cannot be null.");
            return null!;
        }
        if (encoded.Kind == ScenePropertyKind.AssetReference)
            return assetResolver(encoded.Get<Guid>(), type);
        if (type.IsEnum)
            return Enum.ToObject(type, encoded.Get<long>());
        if (type == typeof(bool)) return encoded.Get<bool>();
        if (type == typeof(sbyte)) return checked((sbyte)encoded.Get<long>());
        if (type == typeof(short)) return checked((short)encoded.Get<long>());
        if (type == typeof(int)) return checked((int)encoded.Get<long>());
        if (type == typeof(long)) return encoded.Get<long>();
        if (type == typeof(byte)) return checked((byte)encoded.Get<ulong>());
        if (type == typeof(ushort)) return checked((ushort)encoded.Get<ulong>());
        if (type == typeof(uint)) return checked((uint)encoded.Get<ulong>());
        if (type == typeof(ulong)) return encoded.Get<ulong>();
        if (type == typeof(float)) return encoded.Get<float>();
        if (type == typeof(double)) return encoded.Get<double>();
        if (type == typeof(string)) return encoded.Get<string>();
        if (type == typeof(Guid)) return encoded.Get<Guid>();
        if (type == typeof(Vector2)) return encoded.Get<Vector2>();
        if (type == typeof(Vector3)) return encoded.Get<Vector3>();
        if (type == typeof(Vector4)) return encoded.Get<Vector4>();
        if (type == typeof(Quaternion)) return encoded.Get<Quaternion>();
        if (type == typeof(Matrix4x4)) return encoded.Get<Matrix4x4>();
        throw new NotSupportedException($"Scene property type '{declaredType.FullName}' is not supported.");
    }
}
