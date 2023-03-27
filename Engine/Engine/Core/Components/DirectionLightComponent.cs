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
    public Color Color
    {
        get
        {
            return  Color.FromArgb(255, (int)(_Color.X * 255), (int)(_Color.Y *  255), (int)(_Color.Z * 255));
        }
        set
        {
            _Color = new Vector3(value.R / 255f, value.G / 255f, value.B / 255f);
        }
    }

    public Vector3 _Color;
    public DirectionLightComponent(Actor actor) : base(actor)
    {

    }
}

public struct DirectionLightInfo
{
    public Vector3 Color;

}
