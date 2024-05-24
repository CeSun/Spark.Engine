using System.Numerics;

namespace Spark.Engine.Physics;
public struct Box :
    IAdditionOperators<Box, Vector3, Box>, IAdditionOperators<Box, Box, Box>
{
    public int CompareTo(Box box, int axis)
    {
        var num = (MiddlePoint[axis] - box.MiddlePoint[axis]);  
        if (num == 0)
            return 0;
        return num > 0 ? 1: -1;
    }
    public Vector3 MinPoint 
    {
        get => _minPoint;
        set 
        {
            _minPoint = value;
            MiddlePoint = (MinPoint + MaxPoint) / 2;
        }
    }
    public Vector3 MaxPoint 
    {
        get => _maxPoint;
        set 
        {
            _maxPoint = value;
            MiddlePoint = (MaxPoint - MinPoint) / 2;
        }
    }

    private Vector3 _minPoint;
    private Vector3 _maxPoint;
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
            return index switch
            {
                0 => new Vector3 { X = MinPoint.X, Y = MinPoint.Y, Z = MinPoint.Z },
                1 => new Vector3 { X = MaxPoint.X, Y = MinPoint.Y, Z = MinPoint.Z },
                2 => new Vector3 { X = MaxPoint.X, Y = MinPoint.Y, Z = MaxPoint.Z },
                3 => new Vector3 { X = MinPoint.X, Y = MinPoint.Y, Z = MaxPoint.Z },
                4 => new Vector3 { X = MinPoint.X, Y = MaxPoint.Y, Z = MinPoint.Z },
                5 => new Vector3 { X = MaxPoint.X, Y = MaxPoint.Y, Z = MinPoint.Z },
                6 => new Vector3 { X = MaxPoint.X, Y = MaxPoint.Y, Z = MaxPoint.Z },
                7 => new Vector3 { X = MinPoint.X, Y = MaxPoint.Y, Z = MaxPoint.Z },
                _ => throw new IndexOutOfRangeException()
            };
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


    public bool TestPlanes(Plane[] planes)
    {
        foreach(var plane in planes)
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

