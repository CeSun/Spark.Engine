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
    private bool _isRegistered;

    internal bool HasBegunPlay => _hasBegunPlay;

    /// <summary>编辑器和调试工具使用的稳定显示名称。</summary>
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_name, next, StringComparison.Ordinal))
                return;
            _name = next;
            _world?.NotifyStructureChanged();
        }
    }

    /// <summary>场景持久化使用的稳定身份。</summary>
    public Guid ActorGuid { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 编辑器视口的会话级临时隐藏状态；不属于 SceneProperty，不会保存到场景或复制到 RuntimeWorld。
    /// </summary>
    public bool IsTemporarilyHiddenInEditor { get; internal set; }

    /// <summary>由编辑器可见性服务调用；游戏逻辑不应把它当作运行时 HiddenInGame。</summary>
    public void SetTemporarilyHiddenInEditor(bool hidden) => IsTemporarilyHiddenInEditor = hidden;

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

        // 动态组件遵循 Actor 当前阶段：先注册共有状态，再进入 gameplay 生命周期。
        if (_isRegistered)
        {
            try
            {
                component.RegisterComponent();
                if (_hasBegunPlay)
                    component.BeginPlayComponent();
            }
            catch (Exception lifecycleException)
            {
                try { component.EndPlayComponent(); } catch { /* 保留注册/BeginPlay 的根因 */ }
                try { component.UnregisterComponent(); } catch { /* 保留注册/BeginPlay 的根因 */ }
                _ownedComponents.Remove(component);
                if (ReferenceEquals(RootComponent, component))
                    RootComponent = null;
                component.Owner = null;
                ExceptionDispatchInfo.Capture(lifecycleException).Throw();
            }
        }
        _world?.NotifyStructureChanged();
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
        _world?.NotifyStructureChanged();
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

    internal void RegisterComponents()
    {
        if (_isRegistered)
            return;

        _isRegistered = true;
        var registered = new List<ActorComponent>();
        try
        {
            foreach (var component in _ownedComponents.ToArray())
            {
                registered.Add(component);
                component.RegisterComponent();
            }
        }
        catch (Exception registerException)
        {
            for (var index = registered.Count - 1; index >= 0; index--)
            {
                try { registered[index].UnregisterComponent(); } catch { /* 保留注册根因 */ }
            }
            _isRegistered = false;
            ExceptionDispatchInfo.Capture(registerException).Throw();
        }
    }

    internal void UnregisterComponents()
    {
        if (!_isRegistered)
            return;

        _isRegistered = false;
        Exception? firstException = null;
        var components = _ownedComponents.ToArray();
        for (var index = components.Length - 1; index >= 0; index--)
        {
            try { components[index].UnregisterComponent(); }
            catch (Exception exception) { firstException ??= exception; }
        }
        if (firstException != null)
            ExceptionDispatchInfo.Capture(firstException).Throw();
    }

    public virtual void BeginPlay()
    {
        if (_hasBegunPlay)
            return;

        _hasBegunPlay = true;
        // 副本迭代：组件回调里 AddOwnedComponent 重入不破坏集合（中3）
        try
        {
            foreach (var component in _ownedComponents.ToArray())
                component.BeginPlayComponent();
        }
        catch (Exception beginException)
        {
            var components = _ownedComponents.ToArray();
            for (int i = components.Length - 1; i >= 0; i--)
            {
                try { components[i].EndPlayComponent(); } catch { /* 不覆盖原始启动异常 */ }
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
                components[i].EndPlayComponent();
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
