using Spark.Engine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Components;

public class DirectionLightComponent : LightComponent
{
    public float LightStrength
    {
        get => _LightStrength;
        set
        {
            if (value < 0)
                return;
            if (value > 1)
                return;
            _LightStrength = value;

        }
    }

    public float _LightStrength = 0.7f;
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
