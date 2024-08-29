using System.Numerics;

namespace Spark.Engine.World;

public struct Sphere
{
    public float Radius { get; set; }
    public Vector3 Location { get; set; }

    public bool TestBox(Box box)
    {
        if (Location.X < box.MinPoint.X - Radius)
            return false;
        if (Location.Y < box.MinPoint.Y - Radius)
            return false;
        if (Location.Z < box.MinPoint.Z - Radius)
            return false;
        if (Location.X > box.MaxPoint.X + Radius)
            return false;
        if (Location.Y > box.MaxPoint.Y + Radius)
            return false;
        if (Location.Z > box.MaxPoint.Z + Radius)
            return false;
        return true;
    }
    public bool TestPlanes(Plane[] Planes)
    {
        foreach (var plane in Planes)
        {
            if (plane.Point2Plane(Location) <= -Radius)
            {
                return false;
            }
        }
        return true;
    }
}
