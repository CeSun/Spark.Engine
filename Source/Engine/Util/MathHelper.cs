using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Util;

public static class MathHelper
{
    public static float DegreeToRadians(this float Degree)
    {
        return (float)(Math.PI / 180) * Degree;
    }
    public static float RadiansToDegree(this float Radians)
    {
        return Radians / (float)(Math.PI / 180);
    }
}
