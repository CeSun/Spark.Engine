using Spark.Core.Actors;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;

namespace Spark.Core.Components;

public class DirectionLightComponent : LightComponent
{
    public DirectionLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }

    public override nint GetSubComponentProperties()
    {
        return StructPointerHelper.Malloc(new DirectionLightComponentProperties
        {
            LightStrength = LightStrength,
            Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f),
        });
    }
}


public class DirectionLightComponentProxy : LightComponentProxy
{

}

public struct DirectionLightComponentProperties
{
    private IntPtr Destructors { get; set; }
    public float LightStrength { get; set; }
    public Vector3 Color { get; set; }
}