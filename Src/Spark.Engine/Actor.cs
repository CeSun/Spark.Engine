using Spark.Engine.Components;
using Spark.Engine.Worlds;

namespace Spark.Engine;

public class Actor
{
    private World? _world;

    public World? World => _world;

    public SceneComponent? RootComponent { get; }

    private HashSet<ActorComponent> _ownedComponents = [];

    public void AddOwnedComponent(ActorComponent component)
    {
        if (_ownedComponents.Contains(component))
            return;
        _ownedComponents.Add(component);
    }

    public virtual void BeginPlay()
    {

    }
    public virtual void Update(float deltaTime)
    {

    }

    public virtual void EndPlay()
    {
    }
}
