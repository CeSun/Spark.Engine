using Spark.Engine.Components;
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
    static readonly int MaxLayer = 8;
    Vector3 MaxPoint;

    Vector3 MinPoint;

    int Layer = 0;

    Octree? ParentNode;

    List<BoundingBox> boundingBoxes = new List<BoundingBox>();
    public Octree() : this(new Vector3(-SideLength / 2, -SideLength / 2, -SideLength / 2), new Vector3(SideLength / 2, SideLength / 2, SideLength / 2))
    {

    }

    public float XLength => MaxPoint.X - MinPoint.X;

    public float YLength => MaxPoint.Y - MinPoint.Y;

    public float ZLength => MaxPoint.Z - MinPoint.Z;
    private Octree(Vector3 leftTopPoint, Vector3 rightBottomPoint)
    {
        MaxPoint = leftTopPoint;
        MinPoint = rightBottomPoint;

        Vector3 HalfVector = (MinPoint + MaxPoint) / 2;
        ChildBox = new Box[]
        {
            new Box() {
                MinPoint = new Vector3(MinPoint.X, MinPoint.Y, MinPoint.Z),
                MaxPoint = new Vector3(HalfVector.X, HalfVector.Y, HalfVector.Z)
            },
            new Box()
            {
                MinPoint = new Vector3(HalfVector.X, MinPoint.Y, MinPoint.Z),
                MaxPoint = new Vector3(MaxPoint.X, HalfVector.Y, HalfVector.Z)
            },
            new Box()
            {
                MinPoint = new Vector3(MinPoint.X, MinPoint.Y, HalfVector.Z),
                MaxPoint = new Vector3(HalfVector.X, HalfVector.Y, MaxPoint.Z)
            },
            new Box()
            {
                MinPoint = new Vector3(HalfVector.X, MinPoint.Y, HalfVector.Z),
                MaxPoint = new Vector3(MaxPoint.X, HalfVector.Y, MaxPoint.Z)
            },

            new Box()
            {
                MinPoint = new Vector3(MinPoint.X, HalfVector.Y, MinPoint.Z),
                MaxPoint = new Vector3(HalfVector.X, MaxPoint.Y, HalfVector.Z)
            },
            new Box()
            {
                MinPoint = new Vector3(HalfVector.X, HalfVector.Y, MinPoint.Z),
                MaxPoint = new Vector3(MaxPoint.X, MaxPoint.Y, HalfVector.Z)
            },
            new Box()
            {
                MinPoint = new Vector3(MinPoint.X, HalfVector.Y, HalfVector.Z),
                MaxPoint = new Vector3(HalfVector.X, MaxPoint.Y, MaxPoint.Z)
            },
            new Box()
            {
                MinPoint = new Vector3(HalfVector.X, HalfVector.Y, HalfVector.Z),
                MaxPoint = new Vector3(MaxPoint.X, MaxPoint.Y, MaxPoint.Z)
            }
        };
    }

    public IReadOnlyList<Octree>? Children => _Children;

    Octree[]? _Children = null;

    Box[] ChildBox;
    public void InsertObject(BoundingBox Box)
    {
        if (Box.MinPoint.X < MinPoint.X || Box.MinPoint.Y < MinPoint.Y || Box.MinPoint.Z < MinPoint.Z)
            return;
        if (Box.MaxPoint.X > MaxPoint.X || Box.MaxPoint.Y > MaxPoint.Y || Box.MaxPoint.Z > MaxPoint.Z)
            return;

      
        if (Layer == MaxLayer)
        {
            goto InsertCurrentNode;
        }
        if (Box.XLength > XLength / 2 || Box.YLength > YLength / 2 || Box.ZLength > ZLength / 2)
        {
            goto InsertCurrentNode;
        }
        int TargetIndex = -1;
        for (int i = 0; i < 8; i ++)
        {
            var subBox = ChildBox[i];
            if (subBox.Contains(Box.Box))
            {
                TargetIndex = i;
                break;
            }
        }

        if (TargetIndex < 0)
        {
            goto InsertCurrentNode;
        }

        if (_Children == null)
        {
            _Children = new Octree[]
            {
            new Octree(ChildBox[0].MinPoint, ChildBox[0].MaxPoint) { Layer = Layer + 1, ParentNode = this},
            new Octree(ChildBox[1].MinPoint, ChildBox[1].MaxPoint) { Layer = Layer + 1, ParentNode = this},
            new Octree(ChildBox[2].MinPoint, ChildBox[2].MaxPoint) { Layer = Layer + 1, ParentNode = this},
            new Octree(ChildBox[3].MinPoint, ChildBox[3].MaxPoint) { Layer = Layer + 1, ParentNode = this},
            new Octree(ChildBox[4].MinPoint, ChildBox[4].MaxPoint) { Layer = Layer + 1, ParentNode = this},
            new Octree(ChildBox[5].MinPoint, ChildBox[5].MaxPoint) { Layer = Layer + 1, ParentNode = this},
            new Octree(ChildBox[6].MinPoint, ChildBox[6].MaxPoint) { Layer = Layer + 1, ParentNode = this},
            new Octree(ChildBox[7].MinPoint, ChildBox[7].MaxPoint) { Layer = Layer + 1, ParentNode = this},
            };

        }
        _Children[TargetIndex].InsertObject(Box);
        return;


InsertCurrentNode:
        boundingBoxes.Add(Box);
        Box.ParentNode = this;
        return;
    }
}

public class BoundingBox
{
    public Octree? ParentNode;

    public PrimitiveComponent PrimitiveComponent;

    public Box Box;
    public BoundingBox(Vector3 MaxPoint, Vector3 MinPoint, PrimitiveComponent PrimitiveComponent)
    {
        this.MinPoint = MinPoint;
        this.MaxPoint = MaxPoint;
        this.PrimitiveComponent = PrimitiveComponent;
    }

    public float XLength => MaxPoint.X - MinPoint.X;

    public float YLength => MaxPoint.Y - MinPoint.Y;


    public float ZLength => MaxPoint.Z - MinPoint.Z;

    public Vector3 MinPoint
    {
        get => Box.MinPoint;
        set => Box.MinPoint = value;
    }

    public Vector3 MaxPoint
    {
        get => Box.MaxPoint;
        set => Box.MaxPoint = value;
    }
    
}
