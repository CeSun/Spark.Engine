using Silk.NET.OpenGLES;
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
        LightStrength = 1.0f;
        Color = Color.White;
        _shadowMapSize = 512;
    }
    protected override int propertiesStructSize => Marshal.SizeOf<LightComponentProperties>();

    private Color _color;

    public Color Color
    {
        get => _color;
        set => ChangeProperty(ref _color, value);
    }

    private float _lightStrength;
    public float LightStrength
    {
        get => _lightStrength;
        set => ChangeProperty(ref _lightStrength, value);
    }
    private uint _shadowMapSize;
    public uint ShadowMapSize
    {
        get => _shadowMapSize;
        set => ChangeProperty(ref _shadowMapSize, value);
    }
    public override nint GetPrimitiveComponentProperties()
    {
        var ptr = base.GetPrimitiveComponentProperties();
        ref var properties = ref UnsafeHelper.AsRef<LightComponentProperties>(ptr);
        properties.Color = new Vector3(Color.R / 255f, Color.G / 255f, Color.B / 255f);
        properties.LightStrength = LightStrength;
        properties.ShadowMapSize = ShadowMapSize;
        return ptr;
    }
}

public abstract class LightComponentProxy : PrimitiveComponentProxy
{
    public Vector3 Color;

    public float LightStrength;

    public uint ShadowMapSize;
    public override void UpdateProperties(nint propertiesPtr, RenderDevice renderDevice)
    {
        base.UpdateProperties(propertiesPtr, renderDevice);
        ref var properties = ref UnsafeHelper.AsRef<LightComponentProperties>(propertiesPtr);
        Color = properties.Color;
        LightStrength = properties.LightStrength;
        ShadowMapSize = properties.ShadowMapSize;
    }

    public virtual unsafe void UninitShadowMap(RenderDevice device)
    {
    }
    public virtual unsafe void InitShadowMap(RenderDevice device)
    {
    }
}

public struct LightComponentProperties
{
    public PrimitiveComponentProperties BaseProperties;
    public Vector3 Color;
    public float LightStrength;
    public uint ShadowMapSize;
}