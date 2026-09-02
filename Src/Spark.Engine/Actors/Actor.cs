using Spark.Engine.Components;
using Spark.Engine.Worlds;
using System.Runtime.ExceptionServices;

namespace Spark.Engine.Actors;

public class Actor
{
    private World? _world;

    public World? World => _world;

    private HashSet<ActorComponent> _ownedComponents = [];

    private bool _hasBegunPlay;

    /// <summary>编辑器和调试工具使用的稳定显示名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>场景持久化使用的稳定身份。</summary>
    public Guid ActorGuid { get; set; } = Guid.NewGuid();

    /// <summary>Actor 的空间根组件。第一个加入 Actor 的 SceneComponent 默认成为根组件。</summary>
    public SceneComponent? RootComponent { get; private set; }

    /// <summary>所有拥有的组件。</summary>
    public IEnumerable<ActorComponent> Components => _ownedComponents;

    public void AddOwnedComponent(ActorComponent component)
    {
        if (component == null) throw new ArgumentNullException(nameof(component));
        if (_ownedComponents.Contains(component))
            return;

        _ownedComponents.Add(component);
        component.Owner = this;

        if (component is SceneComponent sceneComponent && RootComponent == null)
            RootComponent = sceneComponent;

        // actor 已 BeginPlay：补调组件 BeginPlay，否则其代理注册/初始化永不被调用（中6）
        if (_hasBegunPlay)
        {
            try
            {
                component.BeginPlay();
            }
            catch (Exception beginException)
            {
                // 动态挂载失败时撤销组件，避免它以“已拥有但未启动”的状态留在 Actor 中。
                try { component.EndPlay(); } catch { /* 保留 BeginPlay 的根因 */ }
                _ownedComponents.Remove(component);
                if (ReferenceEquals(RootComponent, component))
                    RootComponent = null;
                component.Owner = null;
                ExceptionDispatchInfo.Capture(beginException).Throw();
            }
        }
    }

    /// <summary>设置 Actor 的空间根组件。根组件必须属于当前 Actor。</summary>
    public void SetRootComponent(SceneComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!ReferenceEquals(component.Owner, this))
            throw new InvalidOperationException("RootComponent must belong to this Actor.");

        if (ReferenceEquals(RootComponent, component))
            return;

        component.DetachFromComponent(DetachmentTransformRules.KeepWorldTransform);
        RootComponent = component;
    }

    public T? GetComponent<T>() where T : ActorComponent
    {
        foreach (var component in _ownedComponents)
        {
            if (component is T typed)
                return typed;
        }
        return null;
    }

    internal void SetWorld(World? world)
    {
        if (world != null)
        {
            foreach (var component in _ownedComponents)
            {
                if (component is SceneComponent scene && scene.AttachParent?.Owner?.World is { } parentWorld &&
                    !ReferenceEquals(parentWorld, world))
                {
                    throw new InvalidOperationException("An Actor cannot enter a World with a component attached across Worlds.");
                }
            }
        }

        _world = world;
    }

    public virtual void BeginPlay()
    {
        if (_hasBegunPlay)
            return;

        _hasBegunPlay = true;
        // 副本迭代：组件回调里 AddOwnedComponent 重入不破坏集合（中3）
        var started = new List<ActorComponent>();
        try
        {
            foreach (var component in _ownedComponents.ToArray())
            {
                // 把当前组件也记入回滚列表：BeginPlay 可能在注册代理后才抛异常。
                started.Add(component);
                component.BeginPlay();
            }
        }
        catch (Exception beginException)
        {
            for (int i = started.Count - 1; i >= 0; i--)
            {
                try { started[i].EndPlay(); } catch { /* 不覆盖原始启动异常 */ }
            }

            _hasBegunPlay = false;
            ExceptionDispatchInfo.Capture(beginException).Throw();
        }
    }

    public virtual void Update(float deltaTime)
    {
        foreach (var component in _ownedComponents.ToArray())
            component.Update(deltaTime);
    }

    public virtual void EndPlay()
    {
        if (!_hasBegunPlay)
            return;

        _hasBegunPlay = false;
        Exception? firstException = null;
        var components = _ownedComponents.ToArray();
        for (int i = components.Length - 1; i >= 0; i--)
        {
            try
            {
                components[i].EndPlay();
            }
            catch (Exception ex)
            {
                firstException ??= ex;
            }
        }

        if (firstException != null)
            ExceptionDispatchInfo.Capture(firstException).Throw();
    }
}
