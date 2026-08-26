using Spark.Engine.Components;
using Spark.Engine.Worlds;

namespace Spark.Engine.Actors;

public class Actor
{
    private World? _world;

    public World? World => _world;

    public SceneComponent? RootComponent { get; }

    private HashSet<ActorComponent> _ownedComponents = [];

    /// <summary>所有拥有的组件。</summary>
    public IEnumerable<ActorComponent> Components => _ownedComponents;

    public void AddOwnedComponent(ActorComponent component)
    {
        if (component == null) throw new ArgumentNullException(nameof(component));
        if (_ownedComponents.Contains(component))
            return;

        _ownedComponents.Add(component);
        component.Owner = this;
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

    internal void SetWorld(World? world) => _world = world;

    public virtual void BeginPlay()
    {
        // 副本迭代：组件回调里 AddOwnedComponent 重入不破坏集合（中3）
        foreach (var component in _ownedComponents.ToArray())
            component.BeginPlay();
    }

    public virtual void Update(float deltaTime)
    {
        foreach (var component in _ownedComponents.ToArray())
            component.Update(deltaTime);
    }

    public virtual void EndPlay()
    {
        foreach (var component in _ownedComponents.ToArray())
            component.EndPlay();
    }
}
