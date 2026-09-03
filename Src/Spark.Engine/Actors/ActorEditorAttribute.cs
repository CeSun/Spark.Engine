namespace Spark.Engine.Actors;

/// <summary>描述 Actor 在编辑器中的可见性和用户操作能力；不参与场景持久化。</summary>
[Flags]
public enum EditorActorFlags
{
    None = 0,
    HiddenInOutliner = 1 << 0,
    NotSelectable = 1 << 1,
    NotEditable = 1 << 2,
    NotUserDeletable = 1 << 3,
    NotDuplicable = 1 << 4,
    ExcludeFromLevelStats = 1 << 5,

    /// <summary>编辑器或宿主内部使用、不应作为关卡内容呈现的 Actor。</summary>
    Internal = HiddenInOutliner | NotSelectable | NotEditable |
        NotUserDeletable | NotDuplicable | ExcludeFromLevelStats,
}

/// <summary>
/// 为 Actor 类型声明编辑器呈现和操作限制。
/// <para><see cref="Components.SceneTransientAttribute"/> 只控制持久化，本特性只控制编辑器交互。</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class EditorActorAttribute(EditorActorFlags flags) : Attribute
{
    public EditorActorFlags Flags { get; } = flags;
}
