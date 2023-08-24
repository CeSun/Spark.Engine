using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Physics;
public struct Box :
    IAdditionOperators<Box, Vector3, Box>, IAdditionOperators<Box, Box, Box>
{
    public int ComperaTo(Box box, int Axis)
    {
        var num = (MiddlePoint[Axis] - box.MiddlePoint[Axis]);  
        if (num == 0)
            return 0;
        return num > 0 ? 1: -1;
    }
    public Vector3 MinPoint 
    {
        get => _MinPoint;
        set 
        {
            _MinPoint = value;
            MiddlePoint = (MinPoint + MaxPoint) / 2;
        }
    }
    public Vector3 MaxPoint 
    {
        get => _MaxPoint;
        set 
        {
            _MaxPoint = value;
            MiddlePoint = (MaxPoint - MinPoint) / 2;
        }
    }

    private Vector3 _MinPoint;
    private Vector3 _MaxPoint;
    public Vector3 MiddlePoint { get; private set; }
    public static Box operator +(Box left, Vector3 right)
    {
        for(var i = 0; i < 3; i ++)
        {
            if (left.MinPoint[i] > right[i])
            {
                var tmp = left.MinPoint;
                tmp[i] = right[i] ;
                left.MinPoint = tmp;
            }

            if (left.MaxPoint[i] < right[i])
            {
                var tmp = left.MaxPoint;
                tmp[i] = right[i];
                left.MaxPoint = tmp;
            }
        }
        return left;
    }

    public static Box operator +(Box left, Box right)
    {
        left += right.MinPoint;
        left += right.MaxPoint;
        return left;
    }

    public static Box operator*(Box left, Matrix4x4 matrix)
    {
        left.MaxPoint = Vector3.Transform(left.MaxPoint, matrix);
        left.MinPoint = Vector3.Transform(left.MinPoint, matrix);
        return left;
    }

}
