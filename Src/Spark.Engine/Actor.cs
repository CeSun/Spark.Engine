using Spark.Engine.Components;

namespace Spark.Engine;

public class Actor
{
    public SceneComponent? RootComponent { get; }

    private HashSet<ActorComponent> _ownedComponents = [];

    public void AddOwnedComponent(ActorComponent component)
    {
        if (_ownedComponents.Contains(component))
            return;
        _ownedComponents.Add(component);
    }


}
