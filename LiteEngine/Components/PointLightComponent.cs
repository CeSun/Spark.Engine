using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.Components;

public class PointLightComponent : LightComponent
{
    public PointLightComponent(Component parent, string name = "PointLight") : base(parent, name)
    {

    }

    PointLightInfo Info;
    public ref PointLightInfo GetLightRef()
    {
        return ref Info;
    }
}



[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PointLightInfo
{
    public Vector3 Position;


    // 衰减用
    float Constant;
    float Linear;
    float Quadratic;

}