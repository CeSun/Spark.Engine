using Spark.Engine.Actors;

namespace Spark.Engine.Components;

public class ActorComponent
{
    /// <summary>场景持久化使用的稳定组件身份。</summary>
    public Guid ComponentGuid { get; set; } = Guid.NewGuid();

    /// <summary>所属 Actor（由 <see cref="Actor.AddOwnedComponent"/> 设置）。</summary>
    public Actor? Owner { get; internal set; }

    /// <summary>进入世界时调用（对应 UE 的 BeginPlay）。</summary>
    public virtual void BeginPlay() { }

    /// <summary>每逻辑帧更新（对应 UE 的 TickComponent）。</summary>
    public virtual void Update(float deltaTime) { }

    /// <summary>
    /// 编辑器预览使用的渲染代理同步钩子。该调用只复制当前组件状态到 SceneProxy，
    /// 不执行 gameplay Tick；带代理的生成组件会覆盖此方法。
    /// </summary>
    public virtual void RefreshSceneProxy() { }

    /// <summary>离开世界时调用（对应 UE 的 EndPlay）。</summary>
    public virtual void EndPlay() { }
}
