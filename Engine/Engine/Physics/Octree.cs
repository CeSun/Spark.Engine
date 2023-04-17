using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Physics;
    
public class Octree
{
    static readonly float SideLength = 1024;

    Vector3 LeftTopPoint;

    Vector3 RightBottomPoint;

    List<BoundingBox> boundingBoxes = new List<BoundingBox>();
    public Octree() : this(new Vector3(-SideLength / 2, SideLength / 2, -SideLength / 2), new Vector3(SideLength / 2, -SideLength / 2, SideLength / 2))
    {

    }

    private Octree(Vector3 leftTopPoint, Vector3 rightBottomPoint)
    {
        LeftTopPoint = leftTopPoint;
        RightBottomPoint = rightBottomPoint;
    }

    public IReadOnlyList<Octree>? Children => _Children;

    Octree[]? _Children;

    public void InsertObject(BoundingBox Box)
    {
        if (Box.LeftTopPoint.X < LeftTopPoint.X || Box.LeftTopPoint.Y > LeftTopPoint.Y || Box.LeftTopPoint.Z < LeftTopPoint.Z)
            return;
        if (Box.RightBottomPoint.X > RightBottomPoint.X || Box.RightBottomPoint.Y < RightBottomPoint.Y || Box.RightBottomPoint.Z > RightBottomPoint.Z)
            return;
        if (_Children == null)
        {
            
        }
        if (_Children == null)
        {
            boundingBoxes.Add(Box);
            Box.ParentNodes.Add(this);
        }
        else
        {
            foreach(var child in _Children)
            {
                child.InsertObject(Box);
            }
        }
    }
}

public class BoundingBox
{
    public List<Octree> ParentNodes;
    public BoundingBox()
    {
        ParentNodes = new List<Octree>();
    }
    
    public Vector3 LeftTopPoint;

    public Vector3 RightBottomPoint;
}


public class T
{
  
}