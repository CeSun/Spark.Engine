using Spark.Engine.Actors;

namespace Spark.Engine.Components;

public class ActorComponent
{
    /// <summary>所属 Actor（由 <see cref="Actor.AddOwnedComponent"/> 设置）。</summary>
    public Actor? Owner { get; internal set; }

    /// <summary>进入世界时调用（对应 UE 的 BeginPlay）。</summary>
    public virtual void BeginPlay() { }

    /// <summary>每逻辑帧更新（对应 UE 的 TickComponent）。</summary>
    public virtual void Update(float deltaTime) { }

    /// <summary>离开世界时调用（对应 UE 的 EndPlay）。</summary>
    public virtual void EndPlay() { }
}
