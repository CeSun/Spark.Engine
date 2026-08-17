using System.Numerics;
using System.Runtime.InteropServices;

namespace Spark.Engine.Resources;

/// <summary>着色模型：决定生成 WGSL 的着色片段。</summary>
public enum ShadingModel : byte
{
    Unlit = 0,
    Lit = 1,   // Blinn-Phong（PBR 见设计文档 §14）
    PBR = 2,   // 预留
}

/// <summary>混合模式：决定 pipeline 的 blend 状态与是否 alpha test。</summary>
public enum BlendMode : byte
{
    Opaque = 0,
    Masked = 1,
    Translucent = 2,
}

/// <summary>剔除模式：Back = 单面（背面剔除），None = 双面。命名避开 <c>Silk.NET.WebGPU.CullMode</c>。</summary>
public enum MaterialCullMode : byte
{
    Back = 0,
    None = 1,
}

/// <summary>材质参数标识（标量/向量/纹理三类；纹理槽位见 <see cref="TextureFlags"/> 与绑定组 group3）。</summary>
public enum MaterialParam : byte
{
    BaseColor = 0,
    Metallic = 1,
    Roughness = 2,
    EmissiveColor = 3,
    EmissiveStrength = 4,
    NormalStrength = 5,

    // 纹理槽位（固定 5 槽，绑定组 group3 binding 0..4）
    BaseColorTexture = 100,
    NormalTexture = 101,
    EmissiveTexture = 102,
    MetallicRoughnessTexture = 103,
    MaskTexture = 104,
}

/// <summary>纹理开关位：决定生成 WGSL 是否采样对应槽位（只改 shader 代码，不改绑定组布局）。</summary>
[Flags]
public enum TextureFlags : byte
{
    None = 0,
    BaseColor = 1 << 0,
    Normal = 1 << 1,              // 预留：法线贴图（codegen 尚未实现）
    Emissive = 1 << 2,
    MetallicRoughness = 1 << 3,
    Mask = 1 << 4,
}

/// <summary>
/// 材质参数的 GPU uniform 布局（group2，64 字节，与 WGSL <c>MaterialParamsUniform</c> 一一对应）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct MaterialParamsUniform
{
    /// <summary>rgb = 底色 tint，a = 不透明度。</summary>
    public Vector4 BaseColor;

    /// <summary>x = metallic，y = roughness，z/w 未用。</summary>
    public Vector4 MetallicRoughness;

    /// <summary>rgb = 自发光颜色，w = 自发光强度。</summary>
    public Vector4 Emissive;

    /// <summary>x = 法线强度（预留），yzw 未用。</summary>
    public Vector4 NormalStrength;

    public MaterialParamsUniform(Vector4 baseColor, Vector4 metallicRoughness, Vector4 emissive, Vector4 normalStrength)
    {
        BaseColor = baseColor;
        MetallicRoughness = metallicRoughness;
        Emissive = emissive;
        NormalStrength = normalStrength;
    }
}

/// <summary>
/// 材质资产（UE 的 UMaterial）：静态属性决定"编译出什么 shader"（<see cref="MaterialShaderKey"/>），
/// 默认参数决定"参数取什么初始值"。作为 <see cref="SceneResource"/> 走统一上传通道（upload-once + ADR-7 延迟释放）。
/// </summary>
public class Material : SceneResource
{
    public ShadingModel ShadingModel { get; set; } = ShadingModel.Lit;
    public BlendMode BlendMode { get; set; } = BlendMode.Opaque;
    public MaterialCullMode CullMode { get; set; } = MaterialCullMode.Back;

    // 默认参数（实例未覆写时生效）
    public Vector4 BaseColor { get; set; } = Vector4.One;
    public float Metallic { get; set; }
    public float Roughness { get; set; } = 0.5f;
    public Vector4 EmissiveColor { get; set; } = Vector4.Zero;
    public float EmissiveStrength { get; set; }
    public float NormalStrength { get; set; } = 1f;

    // 纹理参数（null = 该槽位绑定 fallback 纹理）
    public Texture2D? BaseColorTexture { get; set; }
    public Texture2D? NormalTexture { get; set; }
    public Texture2D? EmissiveTexture { get; set; }
    public Texture2D? MetallicRoughnessTexture { get; set; }
    public Texture2D? MaskTexture { get; set; }

    /// <summary>折叠静态属性为 shader 变体 key（纯函数；实例委托给 parent，见 <see cref="MaterialInstance"/>）。</summary>
    public virtual MaterialShaderKey GetShaderKey()
    {
        var flags = TextureFlags.None;
        if (BaseColorTexture != null) flags |= TextureFlags.BaseColor;
        if (NormalTexture != null) flags |= TextureFlags.Normal;
        if (EmissiveTexture != null) flags |= TextureFlags.Emissive;
        if (MetallicRoughnessTexture != null) flags |= TextureFlags.MetallicRoughness;
        if (MaskTexture != null) flags |= TextureFlags.Mask;
        return new MaterialShaderKey(ShadingModel, BlendMode, CullMode, flags);
    }

    /// <summary>默认参数 → 固定 uniform 布局（纯函数；实例沿 parent 链解析）。</summary>
    public virtual MaterialParamsUniform GetParamsUniform() => new(
        BaseColor,
        new Vector4(Metallic, Roughness, 0f, 0f),
        new Vector4(EmissiveColor.X, EmissiveColor.Y, EmissiveColor.Z, EmissiveStrength),
        new Vector4(NormalStrength, 0f, 0f, 0f));

    /// <summary>本材质直接持有的纹理（实例会先查覆写表再沿 parent 链）。</summary>
    public virtual Texture2D? GetEffectiveTexture(MaterialParam param) => param switch
    {
        MaterialParam.BaseColorTexture => BaseColorTexture,
        MaterialParam.NormalTexture => NormalTexture,
        MaterialParam.EmissiveTexture => EmissiveTexture,
        MaterialParam.MetallicRoughnessTexture => MetallicRoughnessTexture,
        MaterialParam.MaskTexture => MaskTexture,
        _ => null,
    };
}

/// <summary>
/// 材质实例（UE 的 UMaterialInstance）：引用 parent 材质 + 参数覆写。
/// shader 变体只由 parent 决定（实例不产生新 shader，见 ADR-13/ADR-19），实例只改变参数值。
/// </summary>
public class MaterialInstance : Material
{
    private readonly Dictionary<MaterialParam, float> _scalarOverrides = new();
    private readonly Dictionary<MaterialParam, Vector4> _vectorOverrides = new();
    private readonly Dictionary<MaterialParam, Texture2D> _textureOverrides = new();

    /// <summary>父材质（定义 shader 与默认参数）。</summary>
    public Material? Parent { get; set; }

    public void SetScalar(MaterialParam param, float value) => _scalarOverrides[param] = value;

    public void SetVector(MaterialParam param, Vector4 value) => _vectorOverrides[param] = value;

    public void SetTexture(MaterialParam param, Texture2D? texture)
    {
        if (texture == null) _textureOverrides.Remove(param);
        else _textureOverrides[param] = texture;
    }

    /// <summary>shader 只由 parent 决定；无 parent 时才回退自身静态属性。</summary>
    public override MaterialShaderKey GetShaderKey() => Parent?.GetShaderKey() ?? base.GetShaderKey();

    /// <summary>有效参数 = parent 链默认 ⊕ 本实例覆写（纯函数）。</summary>
    public override MaterialParamsUniform GetParamsUniform()
    {
        var p = Parent?.GetParamsUniform() ?? base.GetParamsUniform();

        var baseColor = GetVector(MaterialParam.BaseColor, p.BaseColor);
        float metallic = GetScalar(MaterialParam.Metallic, p.MetallicRoughness.X);
        float roughness = GetScalar(MaterialParam.Roughness, p.MetallicRoughness.Y);
        var emissive = GetVector(MaterialParam.EmissiveColor, p.Emissive);
        float strength = GetScalar(MaterialParam.EmissiveStrength, p.Emissive.W);
        float normalStrength = GetScalar(MaterialParam.NormalStrength, p.NormalStrength.X);

        return new MaterialParamsUniform(
            baseColor,
            new Vector4(metallic, roughness, 0f, 0f),
            new Vector4(emissive.X, emissive.Y, emissive.Z, strength),
            new Vector4(normalStrength, 0f, 0f, 0f));
    }

    /// <summary>覆写 → parent 链 → 自身默认。</summary>
    public override Texture2D? GetEffectiveTexture(MaterialParam param)
    {
        if (_textureOverrides.TryGetValue(param, out var t))
            return t;
        if (Parent != null)
            return Parent.GetEffectiveTexture(param);
        return base.GetEffectiveTexture(param);
    }

    private float GetScalar(MaterialParam param, float fallback)
        => _scalarOverrides.TryGetValue(param, out var v) ? v : fallback;

    private Vector4 GetVector(MaterialParam param, Vector4 fallback)
        => _vectorOverrides.TryGetValue(param, out var v) ? v : fallback;
}
