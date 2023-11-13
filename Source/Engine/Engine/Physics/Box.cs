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

    public float GetDistance(Vector3 location)
    {
        float min = (MinPoint - location).Length();

        for(int i = 1; i < 8; i ++)
        {
            float tmp = (this[i] - location).Length();
            if (tmp < min)
                min = tmp;
        }
        return min;
    }
    public bool Contains(in Box box)
    {
        if (box.MinPoint.X < this.MinPoint.X)
            return false;
        if (box.MinPoint.Y < this.MinPoint.Y)
            return false;
        if (box.MinPoint.Z < this.MinPoint.Z)
            return false;
        if (box.MaxPoint.X > this.MaxPoint.X)
            return false;
        if (box.MaxPoint.Y > this.MaxPoint.Y)
            return false;
        if (box.MaxPoint.Z > this.MaxPoint.Z)
            return false;

        return true;
    }
    public Vector3 this[int index]
    {
        get
        {
            switch (index)
            {
                case 0:
                    return new Vector3() { X = MinPoint.X, Y = MinPoint.Y, Z = MinPoint.Z };
                case 1:
                    return new Vector3() { X = MaxPoint.X, Y = MinPoint.Y, Z = MinPoint.Z };
                case 2:
                    return new  Vector3() { X = MaxPoint.X, Y = MinPoint.Y, Z = MaxPoint.Z };
                case 3:
                    return new Vector3() { X = MinPoint.X, Y = MinPoint.Y, Z = MaxPoint.Z };
                case 4:
                    return new Vector3() { X = MinPoint.X, Y = MaxPoint.Y, Z = MinPoint.Z };
                case 5:
                    return new Vector3() { X = MaxPoint.X, Y = MaxPoint.Y, Z = MinPoint.Z };
                case 6:
                    return new Vector3() { X = MaxPoint.X, Y = MaxPoint.Y, Z = MaxPoint.Z };
                case 7:
                    return new Vector3() { X = MinPoint.X, Y = MaxPoint.Y, Z = MaxPoint.Z };
                default:
                    throw new IndexOutOfRangeException();
            }

        }
    }
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


    public bool TestPlanes(Plane[] Planes)
    {
        foreach(var plane in Planes)
        {
            var num = 0;
            for(var j = 0; j < 8; j ++)
            {
                if (plane.Point2Plane(this[j]) >= 0 == false)
                {
                    num++;
                }
            }
            if (num >= 8)
                return false;
        }
        return true;
    }
}

