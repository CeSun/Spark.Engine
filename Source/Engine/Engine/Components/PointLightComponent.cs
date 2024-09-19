using Spark.Core.Actors;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Spark.Core.Components;

public class PointLightComponent : LightComponent
{
    public PointLightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {
        AttenuationRadius = 1f;
    }

    private float _attenuationRadius;
    public float AttenuationRadius 
    {
        get => _attenuationRadius;
        
        set =>_attenuationRadius = value;
    }
    public override nint GetSubComponentProperties()
    {
        return UnsafeHelper.Malloc(new PointLightComponentProperties
        {
            LightStrength = LightStrength,
            Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f),
            AttenuationRadius = _attenuationRadius,
        });
    }
}

public class PointLightComponentProxy : LightComponentProxy
{
    public float AttenuationRadius { get; set; }

    public unsafe override void UpdateSubComponentProxy(nint pointer, IRenderer renderer)
    {
        ref PointLightComponentProperties properties = ref Unsafe.AsRef<PointLightComponentProperties>((void*)pointer);
        LightStrength = properties.LightStrength;
        Color = properties.Color;
        AttenuationRadius = properties.AttenuationRadius;
    }
}

public struct PointLightComponentProperties
{
    private IntPtr Destructors { get; set; }
    public float LightStrength { get; set; }
    public Vector3 Color { get; set; }
    public float AttenuationRadius { get; set; }
}