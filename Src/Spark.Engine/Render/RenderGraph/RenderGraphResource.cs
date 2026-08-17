namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 图资源句柄（值类型，pass 间只传句柄，不传 GPU 对象）。
/// 编译期用于建依赖、算生命周期；执行期经 <see cref="RenderGraphContext"/> 解析为真实 GPU 资源。
/// </summary>
public readonly struct RenderGraphResource : IEquatable<RenderGraphResource>
{
    /// <summary>图内唯一 ID（RegisterTexture / ImportTexture 分配）。</summary>
    public readonly int Id;

    /// <summary>
    /// 是否为外部导入资源（如窗口 backbuffer、持久纹理）。
    /// false = transient（图管理生命周期：分配/释放/别名）。
    /// </summary>
    public readonly bool IsExternal;

    public RenderGraphResource(int id, bool isExternal)
    {
        Id = id;
        IsExternal = isExternal;
    }

    public bool Equals(RenderGraphResource other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is RenderGraphResource other && Equals(other);
    public override int GetHashCode() => Id;
    public override string ToString() => IsExternal ? $"External({Id})" : $"Transient({Id})";

    public static bool operator ==(RenderGraphResource left, RenderGraphResource right) => left.Equals(right);
    public static bool operator !=(RenderGraphResource left, RenderGraphResource right) => !left.Equals(right);
}
