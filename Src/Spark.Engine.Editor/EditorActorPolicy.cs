using System.Collections.Concurrent;
using Spark.Engine.Actors;
using Spark.Engine.Components;

namespace Spark.Engine.Editor;

/// <summary>集中解析 Actor 的编辑器可见性和用户操作能力。</summary>
public static class EditorActorPolicy
{
    private static readonly ConcurrentDictionary<Type, EditorActorFlags> FlagsByType = new();

    public static EditorActorFlags GetFlags(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return FlagsByType.GetOrAdd(actor.GetType(), static type =>
            type.GetCustomAttributes(typeof(EditorActorAttribute), inherit: true)
                .OfType<EditorActorAttribute>()
                .Aggregate(EditorActorFlags.None, static (flags, attribute) => flags | attribute.Flags));
    }

    public static bool IsVisibleInOutliner(Actor actor)
        => !GetFlags(actor).HasFlag(EditorActorFlags.HiddenInOutliner);

    public static bool CanSelect(object target)
        => !TryGetOwningActor(target, out var actor) ||
           !GetFlags(actor).HasFlag(EditorActorFlags.NotSelectable);

    public static bool CanEdit(object target)
        => !TryGetOwningActor(target, out var actor) ||
           !GetFlags(actor).HasFlag(EditorActorFlags.NotEditable);

    public static bool CanDelete(Actor actor)
        => !GetFlags(actor).HasFlag(EditorActorFlags.NotUserDeletable);

    public static bool CanDuplicate(Actor actor)
        => !GetFlags(actor).HasFlag(EditorActorFlags.NotDuplicable);

    public static bool IncludeInLevelStats(Actor actor)
        => !GetFlags(actor).HasFlag(EditorActorFlags.ExcludeFromLevelStats);

    private static bool TryGetOwningActor(object target, out Actor actor)
    {
        switch (target)
        {
            case Actor targetActor:
                actor = targetActor;
                return true;
            case ActorComponent { Owner: { } owner }:
                actor = owner;
                return true;
            default:
                actor = null!;
                return false;
        }
    }
}
