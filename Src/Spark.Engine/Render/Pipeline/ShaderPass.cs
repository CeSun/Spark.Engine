namespace Spark.Engine.Render.Pipeline;

/// <summary>
/// 着色 pass（对应 UE 的 shader type）：同一材质按 pass 编出不同 shader 与 pipeline。
/// 材质静态属性（<c>MaterialShaderKey</c>）决定"材质是什么"，<see cref="ShaderPass"/> 决定"在哪个阶段画"，
/// 两者共同构成完整 shader 变体身份。
/// </summary>
public enum ShaderPass : byte
{
    /// <summary>前向基础 pass：完整着色（shade_lit），输出颜色。</summary>
    Forward = 0,

    /// <summary>阴影贴图深度 pass：仅写深度；masked 材质按 alpha/mask discard。</summary>
    ShadowDepth = 1,

    /// <summary>深度预 pass：仅写深度；masked 材质按 alpha/mask discard。</summary>
    DepthOnly = 2,
}
