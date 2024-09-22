using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using Spark.Util;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Components;

public abstract class LightComponent : PrimitiveComponent
{
    public LightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }
    protected override int propertiesStructSize => Marshal.SizeOf<LightComponentProperties>();

    private float _lightStrength;
    public float LightStrength 
    {
        get => _lightStrength;
        set => ChangeProperty(ref _lightStrength, value);
    }

    private Color _color;

    public Color Color
    {
        get => _color;
        set => ChangeProperty(ref _color, value);
    }

    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<LightComponentProperties>(ptr);
        properties.LightStrength = LightStrength;
        properties.Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f);
        return ptr;
    }
}

public abstract class LightComponentProxy : PrimitiveComponentProxy
{
    public float LightStrength { get; set; }

    public Vector3 Color { get; set; }

    public override void UpdateProperties(nint propertiesPtr, BaseRenderer renderer)
    {
        base.UpdateProperties(propertiesPtr, renderer);
        ref var properties = ref UnsafeHelper.AsRef<LightComponentProperties>(propertiesPtr);
        LightStrength = properties.LightStrength;
        Color = properties.Color;
    }
}

public struct LightComponentProperties
{
    public PrimitiveComponentProperties BaseProperties;
    public float LightStrength { get; set; }
    public Vector3 Color { get; set; }
}