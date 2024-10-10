using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.Shapes;

public struct Box
{
    public Vector3 Min;

    public Vector3 Max;

    public void GetPoints(Span<Vector3> value)
    {
        value[0] = new(Min.X, Min.Y, Min.Z);
        value[1] = new(Max.X, Max.Y, Max.Z);

        value[2] = new(Min.X, Max.Y, Max.Z);
        value[3] = new(Min.X, Min.Y, Max.Z);

        value[4] = new(Max.X, Min.Y, Min.Z);
        value[5] = new(Max.X, Max.Y, Min.Z);

        value[6] = new(Min.X, Max.Y, Min.Z);
        value[7] = new(Max.X, Min.Y, Max.Z);

    }
    public static Box operator+ (Box left, Vector3 right)
    {
        Box box = left;
        if (right.X > box.Max.X)
            box.Max.X = right.X;
        if (right.Y > box.Max.Y)
            box.Max.Y = right.Y;
        if (right.Z > box.Max.Z)
            box.Max.Z = right.Z;

        if (right.X < box.Min.X)
            box.Min.X = right.X;
        if (right.Y < box.Min.Y)
            box.Min.Y = right.Y;
        if (right.Z < box.Min.Z)
            box.Min.Z = right.Z;

        return box;
    }

}
