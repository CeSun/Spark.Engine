using Spark.Core.Actors;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Spark.Core.Components;

public class DirectionLightComponent : LightComponent
{
    public DirectionLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
    }

    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new DirectionLightComponentProperties
        {
            LightStrength = LightStrength,
            Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f),
        });
    }
}


public class DirectionLightComponentProxy : LightComponentProxy
{
    public unsafe override void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        ref DirectionLightComponentProperties properties = ref Unsafe.AsRef<DirectionLightComponentProperties>((void*)pointer);
        LightStrength = properties.LightStrength;
        Color = properties.Color;
    }
}

public struct DirectionLightComponentProperties
{
    private IntPtr Destructors { get; set; }
    public float LightStrength { get; set; }
    public Vector3 Color { get; set; }
}