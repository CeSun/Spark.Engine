using Spark.Core.Actors;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Spark.Core.Components;

public class SpotLightComponent : LightComponent
{
    public SpotLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        InnerAngle = 12.5f;
        OuterAngle = 17.5f;
    }

    private float _innerAngle;
    public float InnerAngle 
    {
        get => _innerAngle;
        set =>_innerAngle = value;
    }
    public float _outerAngle { get; set; }

    public float OuterAngle 
    {
        get => _outerAngle;
        set =>_outerAngle = value;
    }

    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new SpotLightComponentProperties
        {
            LightStrength = LightStrength,
            Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f),
            InnerAngle = _innerAngle,
            OuterAngle = _outerAngle,
        });
    }
}

public class SpotLightComponentProxy : LightComponentProxy
{
    public float OuterAngle {  get; set; }
    public float InnerAngle {  get; set; }

    public unsafe override void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        ref SpotLightComponentProperties properties = ref Unsafe.AsRef<SpotLightComponentProperties>((void*)pointer);
        LightStrength = properties.LightStrength;
        Color = properties.Color;
        OuterAngle = properties.OuterAngle;
        InnerAngle = properties.InnerAngle;
    }

}


public struct SpotLightComponentProperties
{
    private IntPtr Destructors { get; set; }
    public float LightStrength { get; set; }
    public Vector3 Color { get; set; }
    public float OuterAngle { get; set; }
    public float InnerAngle { get; set; }
}