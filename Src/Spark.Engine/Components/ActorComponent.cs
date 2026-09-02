using Spark.Engine.Actors;

namespace Spark.Engine.Components;

public class ActorComponent
{
    private bool _isRegistered;
    private bool _hasBegunPlay;

    /// <summary>场景持久化使用的稳定组件身份。</summary>
    public Guid ComponentGuid { get; set; } = Guid.NewGuid();

    /// <summary>所属 Actor（由 <see cref="Actor.AddOwnedComponent"/> 设置）。</summary>
    public Actor? Owner { get; internal set; }

    /// <summary>组件是否已进入 World 并完成编辑器/运行时共有的注册阶段。</summary>
    public bool IsRegistered => _isRegistered;

    /// <summary>组件是否已进入 gameplay 生命周期。</summary>
    public bool HasBegunPlay => _hasBegunPlay;

    internal void RegisterComponent()
    {
        if (_isRegistered)
            return;
        _isRegistered = true;
        try
        {
            OnRegister();
        }
        catch
        {
            try { OnUnregister(); } catch { /* 保留 OnRegister 根因 */ }
            _isRegistered = false;
            throw;
        }
    }

    internal void UnregisterComponent()
    {
        if (!_isRegistered)
            return;
        try
        {
            OnUnregister();
        }
        finally
        {
            _isRegistered = false;
        }
    }

    /// <summary>进入 World 时调用；用于注册渲染代理等编辑器和运行时共有状态。</summary>
    protected virtual void OnRegister() { }

    internal void BeginPlayComponent()
    {
        if (_hasBegunPlay)
            return;
        _hasBegunPlay = true;
        BeginPlay();
    }

    /// <summary>仅运行时 World 进入 gameplay 时调用（对应 UE 的 BeginPlay）。</summary>
    public virtual void BeginPlay() { }

    /// <summary>每逻辑帧更新（对应 UE 的 TickComponent）。</summary>
    public virtual void Update(float deltaTime) { }

    /// <summary>
    /// 编辑器预览使用的渲染代理同步钩子。该调用只复制当前组件状态到 SceneProxy，
    /// 不执行 gameplay Tick；带代理的生成组件会覆盖此方法。
    /// </summary>
    public virtual void RefreshSceneProxy() { }

    internal void EndPlayComponent()
    {
        if (!_hasBegunPlay)
            return;
        try
        {
            EndPlay();
        }
        finally
        {
            _hasBegunPlay = false;
        }
    }

    /// <summary>离开 World 时调用；与 <see cref="OnRegister"/> 对称，不代表 gameplay EndPlay。</summary>
    protected virtual void OnUnregister() { }

    /// <summary>仅已 BeginPlay 的运行时组件退出 gameplay 时调用（对应 UE 的 EndPlay）。</summary>
    public virtual void EndPlay() { }
}
