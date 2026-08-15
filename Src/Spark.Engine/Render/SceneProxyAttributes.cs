namespace Spark.Engine.Render;

/// <summary>
/// 标记一个组件，声明由 SceneProxy 源生成器产出对应的 SceneProxy 子类、payload struct 与
/// SceneSnapshot 分类缓冲字段。参数 <paramref name="category"/> 即场景对象类别；
/// 快照字段名由生成器从类别推导（Mesh 结尾 → +es，其余 → +s），组件无需、也无法指定字符串。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SceneProxyAttribute : Attribute
{
    public SceneCategory Category { get; }

    public SceneProxyAttribute(SceneCategory category)
    {
        Category = category;
    }
}

/// <summary>标记进 payload 的字段/属性（由源生成器搬运到代理与 payload struct）。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class ScenePayloadAttribute : Attribute
{
}
