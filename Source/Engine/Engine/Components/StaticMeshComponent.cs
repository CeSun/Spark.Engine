using Silk.NET.OpenGLES;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.World;
using System.Numerics;

namespace Spark.Engine.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;

    private StaticMesh? _StaticMesh;

    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set => _StaticMesh = value;
    }
    public StaticMeshComponent(Actor actor) : base(actor)
    {

    }

}
