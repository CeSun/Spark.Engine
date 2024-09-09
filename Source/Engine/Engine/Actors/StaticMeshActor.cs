using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;

namespace Spark.Engine;

public class StaticMeshActor : Actor
{
    public StaticMeshComponent StaticMeshComponent { get; private set; }
    public StaticMeshActor(World.Level level) : base(level)
    {
        StaticMeshComponent = new StaticMeshComponent(this);
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
