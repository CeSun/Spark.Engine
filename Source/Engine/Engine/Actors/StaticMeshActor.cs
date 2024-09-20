using Spark.Core.Assets;
using Spark.Core.Components;

namespace Spark.Core.Actors;

public class StaticMeshActor : Actor
{
    public StaticMeshComponent StaticMeshComponent { get; private set; }
    public StaticMeshActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        StaticMeshComponent = new StaticMeshComponent(this, registorToWorld);
    }

    public bool IsStatic
    {
        get => StaticMeshComponent.IsStatic;
        set => StaticMeshComponent.IsStatic = value;
    }

    public StaticMesh? StaticMesh
    {
        get => StaticMeshComponent.StaticMesh;
        set => StaticMeshComponent.StaticMesh = value;
    }

}
