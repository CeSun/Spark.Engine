namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 资源访问类型：编译时据此建依赖边与 barrier。
/// </summary>
public enum ResourceAccess
{
    /// <summary>采样纹理（ShaderResource / TextureBinding）。</summary>
    Sample,

    /// <summary>写入渲染目标（颜色附件或深度附件）。</summary>
    RenderTarget,
}
