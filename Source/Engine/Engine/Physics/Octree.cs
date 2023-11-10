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

    Box CurrentBox;

    Vector3 MaxPoint
    {
        get => CurrentBox.MaxPoint;
        set => CurrentBox.MaxPoint = value;
    }
    Vector3 MinPoint
    {
        get => CurrentBox.MinPoint;
        set => CurrentBox.MinPoint = value;
    }
    int Layer = 0;

    Octree? ParentNode;

    List<BoundingBox> boundingBoxes = new List<BoundingBox>();
    public Octree() : this(new Vector3(-SideLength / 2, -SideLength / 2, -SideLength / 2), new Vector3(SideLength / 2, SideLength / 2, SideLength / 2))
    {
    }
    private Octree(Vector3 leftTopPoint, Vector3 rightBottomPoint)
    {
        MinPoint = leftTopPoint;
        MaxPoint = rightBottomPoint;

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
    public float XLength => MaxPoint.X - MinPoint.X;
    public float YLength => MaxPoint.Y - MinPoint.Y;
    public float ZLength => MaxPoint.Z - MinPoint.Z;
    public IReadOnlyList<Octree>? Children => _Children;

    Octree[]? _Children = null;

    readonly Box[] ChildBox;

    public int NodeCount
    {
        get
        {
            if (_Children == null)
                return boundingBoxes.Count;
            int len = boundingBoxes.Count;
            foreach (var child in _Children)
            {
                len += child.NodeCount;
            }
            return len;
        }
    }
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
    
    public void RemoveObject(BoundingBox Box)
    {
        if (CurrentBox.Contains(Box.Box) == false)
            return;
        if (boundingBoxes.Contains(Box))
        {
            boundingBoxes.Remove(Box);
            Box.ParentNode = null;
        }
        else
        {
            if (_Children != null)
            {
                foreach(var child in _Children)
                {
                    child.RemoveObject(Box);
                }
            }
        }

        if (NodeCount == 0)
        {
            _Children = null;
        }
    }

    public void FrustumCulling(List<PrimitiveComponent> Components, Plane[] Planes)
    {
        if (this.CurrentBox.TestPlanes(Planes) == false)
            return;
        foreach (var subox in boundingBoxes)
        {
            if(subox.Box.TestPlanes(Planes))
            {
                Components.Add(subox.PrimitiveComponent);
            }
        }
        if (_Children != null)
        {
            foreach(var child in _Children)
            {
                child.FrustumCulling(Components, Planes);
            }
        }
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
