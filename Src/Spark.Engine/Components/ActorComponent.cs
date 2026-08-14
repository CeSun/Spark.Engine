using Spark.Engine.Actors;

namespace Spark.Engine.Components;

public class ActorComponent
{
    /// <summary>所属 Actor（由 <see cref="Actor.AddOwnedComponent"/> 设置）。</summary>
    public Actor? Owner { get; internal set; }
}
