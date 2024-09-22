using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Render;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Spark.Core.Components;

public abstract class LightComponent : PrimitiveComponent
{
    public LightComponent(Actor actor, bool registerToWorld = true) : base(actor, registerToWorld)
    {

    }

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

}

public class LightComponentProxy : PrimitiveComponentProxy
{
    public float LightStrength { get; set; }

    public Vector3 Color { get; set; }

    public unsafe override void UpdateSubComponentProxy(nint pointer, BaseRenderer renderer)
    {
        ref LightComponentProperties properties = ref Unsafe.AsRef<LightComponentProperties>((void*)pointer);
        LightStrength = properties.LightStrength;
        Color = properties.Color;
    }
}

public struct LightComponentProperties 
{
    private IntPtr Destructors { get; set; }
    public float LightStrength { get; set; }
    public Vector3 Color { get; set; }
}