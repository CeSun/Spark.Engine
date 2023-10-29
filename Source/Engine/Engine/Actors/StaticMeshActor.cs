using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;

namespace Spark.Engine.Actors;

public class StaticMeshActor : Actor
{
    [Property]
    public StaticMeshComponent StaticMeshComponent { get; private set; }
    public StaticMeshActor(Level level, string Name = "") : base(level, Name)
    {
        StaticMeshComponent = new StaticMeshComponent(this);
    }

    [Property]
    public bool IsStatic
    {
        get => StaticMeshComponent.IsStatic;
        set => StaticMeshComponent.IsStatic = value;
    }

    [Property]
    public StaticMesh? StaticMesh
    {
        get => StaticMeshComponent.StaticMesh;
        set => StaticMeshComponent.StaticMesh = value;
    }

}
