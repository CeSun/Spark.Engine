namespace Spark.Engine.Resources;

/// <summary>
/// shader 变体的值类型 key：由材质的静态属性（着色模型/混合模式/双面/纹理开关）折叠而成，
/// 作为编译缓存（<c>Dictionary&lt;MaterialShaderKey, MaterialVariant&gt;</c>）的键。
/// 静态属性相同的材质共享同一编译产物（ADR-14）。
/// </summary>
public readonly struct MaterialShaderKey : IEquatable<MaterialShaderKey>
{
    public readonly ShadingModel ShadingModel;
    public readonly BlendMode BlendMode;
    public readonly MaterialCullMode CullMode;
    public readonly TextureFlags TextureFlags;

    public MaterialShaderKey(ShadingModel shadingModel, BlendMode blendMode, MaterialCullMode cullMode, TextureFlags textureFlags)
    {
        ShadingModel = shadingModel;
        BlendMode = blendMode;
        CullMode = cullMode;
        TextureFlags = textureFlags;
    }

    public bool Equals(MaterialShaderKey other)
        => ShadingModel == other.ShadingModel
        && BlendMode == other.BlendMode
        && CullMode == other.CullMode
        && TextureFlags == other.TextureFlags;

    public override bool Equals(object? obj) => obj is MaterialShaderKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(ShadingModel, BlendMode, CullMode, TextureFlags);

    public static bool operator ==(MaterialShaderKey left, MaterialShaderKey right) => left.Equals(right);

    public static bool operator !=(MaterialShaderKey left, MaterialShaderKey right) => !left.Equals(right);
}
