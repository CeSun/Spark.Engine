using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Spark.Components;

public class DirectionLightComponent : LightComponent
{
    public DirectionLightComponent(Actor actor) : base(actor)
    {
    }

    public DirectionLightInfo LightInfo
    {
        get => new DirectionLightInfo
        {
            Direction = ForwardVector,
            Color = _Color
        };
    }

}

public struct DirectionLightInfo
{
    public Vector3 Direction;
    public Vector3 Color;

}
