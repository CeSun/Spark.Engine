using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.Components;

public class SpotLightComponent : LightComponent
{
    public SpotLightComponent(Component parent, string name = "SpotLight") : base(parent, name)
    {

    }

    

}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SpotLightInfo
{
    Vector3 Position;
    Vector3 Direction;
    float Radius;

    // 衰减用
    float Constant;
    float Linear;
    float Quadratic;

}