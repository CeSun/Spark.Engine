using Spark.Core.Assets;
using Spark.Core.Actors;
using Spark.Core.Render;
using System.Runtime.InteropServices;
using Spark.Util;

namespace Spark.Core.Components;

public class DecalComponent : PrimitiveComponent
{
    protected override bool ReceiveUpdate => true;
    public DecalComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }

    private Material? _material;
    public Material? Material
    {
        get => _material;
        set => _material = value;
    }

    public override nint GetSubComponentProperties()
    {
        return StructPointerHelper.Malloc(new DecalComponentProperties
        {
            Material = Material == null ? default : Material.WeakGCHandle,
        });
    }
}

public class DecalComponentProxy : PrimitiveComponentProxy
{
    public MaterialProxy? MaterialProxy { get; set; }
}

public struct DecalComponentProperties
{
    private IntPtr Destructors { get; set; }
    public GCHandle Material {  get; set; }
}