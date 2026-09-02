namespace Spark.Engine.Components;

/// <summary>显式标记需要进入 SceneDocument 的可编辑组件属性。</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class ScenePropertyAttribute : Attribute
{
}

/// <summary>标记不应进入编辑场景文档、只由宿主在运行时创建的 Actor 类型。</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SceneTransientAttribute : Attribute
{
}
