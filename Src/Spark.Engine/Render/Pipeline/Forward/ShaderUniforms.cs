using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Engine.Render.Pipeline.Forward;

/// <summary>光照 uniform 数组容量（与 WGSL <c>array&lt;Light, 16&gt;</c> 同步）。</summary>
public static class ShaderConstants
{
    public const int MaxLights = 16;
}

/// <summary>单个光源的 GPU uniform（4×vec4 = 64 字节，与 WGSL <c>Light</c> 一一对应）。</summary>
[StructLayout(LayoutKind.Sequential)]
public struct LightUniform
{
    /// <summary>rgb = 颜色，w = 强度。</summary>
    public Vector4 ColorIntensity;

    /// <summary>xyz = 位置，w = 衰减半径（平行光忽略）。</summary>
    public Vector4 PositionRange;

    /// <summary>xyz = 方向（平行光/聚光），w = cos(内锥角)。</summary>
    public Vector4 DirectionCone;

    /// <summary>x = 类型（0 点光 / 1 平行光 / 2 聚光），y = cos(外锥角)，zw 未用。</summary>
    public Vector4 TypeOuter;
}

/// <summary>固定容量光源数组（InlineArray，供 <see cref="FrameUniformData"/> 内嵌）。</summary>
[InlineArray(ShaderConstants.MaxLights)]
public struct LightUniformArray
{
    private LightUniform _e0;
}

/// <summary>
/// 每帧 uniform（group0，与 WGSL <c>FrameUniforms</c> 一一对应）。
/// 布局：view_proj(64) + camera_pos(16) + light_count(4) + pad(12) + lights(16×64)
///     + shadow_view_proj(64) + shadow_light(4) + pad(12)。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct FrameUniformData
{
    public Matrix4x4 ViewProjection;

    public Vector4 CameraPosition;

    public uint LightCount;

    public uint Pad0;

    public uint Pad1;

    public uint Pad2;

    public LightUniformArray Lights;

    /// <summary>阴影光源的 view-proj（forward pass 把世界坐标变换到阴影贴图空间）。</summary>
    public Matrix4x4 ShadowViewProjection;

    /// <summary>阴影光源在 lights 数组中的下标；0xFFFFFFFF = 无阴影。</summary>
    public uint ShadowLightIndex;

    public uint Pad3;

    public uint Pad4;

    public uint Pad5;
}

/// <summary>每实例 uniform（group1，与 WGSL <c>ObjectUniforms</c> 一一对应）。</summary>
[StructLayout(LayoutKind.Sequential)]
public struct ObjectUniformData
{
    /// <summary>世界矩阵（行主序，与 MVP 同一约定）。</summary>
    public Matrix4x4 World;

    /// <summary>法线矩阵（世界矩阵的逆转置，上 3x3 生效）。</summary>
    public Matrix4x4 NormalMatrix;
}
