
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

    List<BaseBounding> boundingBoxes = new List<BaseBounding>();
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
    public void InsertObject(BaseBounding Box)
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
            if (subBox.Contains(new Box() { MaxPoint = Box.MaxPoint, MinPoint = Box.MinPoint}))
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
    
    public void RemoveObject(BaseBounding Box)
    {
        if (Box.ParentNode == null)
            return;
        Box.ParentNode.boundingBoxes.Remove(Box);
        if (Box.ParentNode.NodeCount == 0)
            Box.ParentNode._Children = null;

        Box.ParentNode = null;
    }

    public void FrustumCulling<T>(List<T> Components, Plane[] Planes)
    {
        if (this.CurrentBox.TestPlanes(Planes) == false)
            return;
        foreach (var subox in boundingBoxes)
        {
            if (subox.PrimitiveComponent is T t && subox.TestPlanes(Planes))
            {
                Components.Add(t);
            }
        }
        if (_Children != null)
        {
            foreach (var child in _Children)
            {
                child.FrustumCulling(Components, Planes);
            }
        }
    }
    public void SphereCulling<T>(List<T> Components, Sphere sphere) where T : PrimitiveComponent
    {
        if (sphere.TestBox(CurrentBox) == false)
            return;
        foreach (var subox in boundingBoxes)
        {
            if (subox.PrimitiveComponent is T t &&  sphere.TestBox(new Box() { MinPoint = subox.MinPoint, MaxPoint = subox.MaxPoint }))
            {
                Components.Add(t);
            }
        }
        if (_Children != null)
        {
            foreach (var child in _Children)
            {
                child.SphereCulling<T>(Components, sphere);
            }
        }
    }
}


public abstract class BaseBounding
{
    public BaseBounding(PrimitiveComponent PrimitiveComponent)
    {
        this.PrimitiveComponent = PrimitiveComponent;
    }
    public Octree? ParentNode;

    public PrimitiveComponent PrimitiveComponent;
    public abstract bool TestPlanes(Plane[] planes);

    public abstract float XLength { get; }

    public abstract float YLength { get; }
    public abstract float ZLength { get; }

    public abstract Vector3 MinPoint { get;}
    public abstract Vector3 MaxPoint { get;}


}
public class BoundingBox : BaseBounding
{

    public Box Box;
    public BoundingBox(PrimitiveComponent PrimitiveComponent) : base(PrimitiveComponent)
    {
        Box.MinPoint = Vector3.Zero;
        Box.MaxPoint = Vector3.Zero;
    }

    public override float XLength => MaxPoint.X - MinPoint.X;

    public override float YLength => MaxPoint.Y - MinPoint.Y;
    public override float ZLength => MaxPoint.Z - MinPoint.Z;

    public override Vector3 MinPoint => Box.MinPoint;

    public override Vector3 MaxPoint => Box.MaxPoint;

    public override bool TestPlanes(Plane[] planes)
    {
        return Box.TestPlanes(planes);
    }
}

public class BoundingSphere : BaseBounding
{
    public Sphere Sphere;
    public BoundingSphere(PrimitiveComponent PrimitiveComponent) : base(PrimitiveComponent)
    {

    }

    public float Radius
    {
        get => Sphere.Radius;
        set => Sphere.Radius = value;
    }

    public Vector3 Location
    {
        get => Sphere.Location;
        set => Sphere.Location = value;
    }
    public override float XLength => Sphere.Radius * 2;

    public override float YLength => Sphere.Radius * 2;

    public override float ZLength => Sphere.Radius * 2;

    public override Vector3 MinPoint => Sphere.Location - new Vector3(Sphere.Radius, Sphere.Radius, Sphere.Radius);
    public override Vector3 MaxPoint => Sphere.Location + new Vector3(Sphere.Radius, Sphere.Radius, Sphere.Radius);

    public override bool TestPlanes(Plane[] planes)
    {
        return Sphere.TestPlanes(planes);
    }
}
