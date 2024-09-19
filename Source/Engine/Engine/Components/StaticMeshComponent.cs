using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    public StaticMeshComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }

    protected override bool ReceiveUpdate => false;

    private StaticMesh? _StaticMesh;

    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set => _StaticMesh = value;
    }

    public override nint GetSubComponentProperties()
    {
        GCHandle gchandle = default;
        if (this.StaticMesh != null) 
        {
            gchandle = StaticMesh.WeakGCHandle;
        }
        return StructPointerHelper.Malloc(new StaticMeshComponentProperties
        {
            StaticMesh = gchandle
        });
    }
}

public class StaticMeshComponentProxy : PrimitiveComponentProxy
{
    public StaticMeshProxy? StaticMeshProxy { get; set; }

}

public struct StaticMeshComponentProperties
{
    private IntPtr Destructors { get; set; }

    public GCHandle StaticMesh {  get; set; }

    
}