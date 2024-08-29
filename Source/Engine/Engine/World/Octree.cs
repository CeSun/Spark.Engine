using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.World;

public class Octree
{
    static readonly float SideLength = 1024;
    static readonly int MaxLayer = 8;

    Box _currentBox;

    Vector3 MaxPoint
    {
        get => _currentBox.MaxPoint;
        set => _currentBox.MaxPoint = value;
    }
    Vector3 MinPoint
    {
        get => _currentBox.MinPoint;
        set => _currentBox.MinPoint = value;
    }

    private int _layer;

    private Octree? _parentNode;

    private readonly List<BaseBounding> _boundingBoxes = [];
    public Octree(Octree? parentNode) : this(new Vector3(-SideLength / 2, -SideLength / 2, -SideLength / 2), new Vector3(SideLength / 2, SideLength / 2, SideLength / 2), parentNode)
    {
    }
    private Octree(Vector3 leftTopPoint, Vector3 rightBottomPoint, Octree? parentNode)
    {
        MinPoint = leftTopPoint;
        MaxPoint = rightBottomPoint;
        _parentNode = parentNode;

        var halfVector = (MinPoint + MaxPoint) / 2;
        _childBox =
        [
            new Box
            {
                MinPoint = new Vector3(MinPoint.X, MinPoint.Y, MinPoint.Z),
                MaxPoint = new Vector3(halfVector.X, halfVector.Y, halfVector.Z)
            },
            new Box
            {
                MinPoint = new Vector3(halfVector.X, MinPoint.Y, MinPoint.Z),
                MaxPoint = new Vector3(MaxPoint.X, halfVector.Y, halfVector.Z)
            },
            new Box
            {
                MinPoint = new Vector3(MinPoint.X, MinPoint.Y, halfVector.Z),
                MaxPoint = new Vector3(halfVector.X, halfVector.Y, MaxPoint.Z)
            },
            new Box
            {
                MinPoint = new Vector3(halfVector.X, MinPoint.Y, halfVector.Z),
                MaxPoint = new Vector3(MaxPoint.X, halfVector.Y, MaxPoint.Z)
            },

            new Box
            {
                MinPoint = new Vector3(MinPoint.X, halfVector.Y, MinPoint.Z),
                MaxPoint = new Vector3(halfVector.X, MaxPoint.Y, halfVector.Z)
            },
            new Box
            {
                MinPoint = new Vector3(halfVector.X, halfVector.Y, MinPoint.Z),
                MaxPoint = new Vector3(MaxPoint.X, MaxPoint.Y, halfVector.Z)
            },
            new Box
            {
                MinPoint = new Vector3(MinPoint.X, halfVector.Y, halfVector.Z),
                MaxPoint = new Vector3(halfVector.X, MaxPoint.Y, MaxPoint.Z)
            },
            new Box
            {
                MinPoint = new Vector3(halfVector.X, halfVector.Y, halfVector.Z),
                MaxPoint = new Vector3(MaxPoint.X, MaxPoint.Y, MaxPoint.Z)
            }
        ];
    }
    public float XLength => MaxPoint.X - MinPoint.X;
    public float YLength => MaxPoint.Y - MinPoint.Y;
    public float ZLength => MaxPoint.Z - MinPoint.Z;
    public IReadOnlyList<Octree>? Children => _children;

    private Octree[]? _children;

    private readonly Box[] _childBox;

    public int NodeCount
    {
        get
        {
            if (_children == null)
                return _boundingBoxes.Count;
            int len = _boundingBoxes.Count;
            foreach (var child in _children)
            {
                len += child.NodeCount;
            }
            return len;
        }
    }
    public void InsertObject(BaseBounding box)
    {
        if (box.MinPoint.X < MinPoint.X || box.MinPoint.Y < MinPoint.Y || box.MinPoint.Z < MinPoint.Z)
            return;
        if (box.MaxPoint.X > MaxPoint.X || box.MaxPoint.Y > MaxPoint.Y || box.MaxPoint.Z > MaxPoint.Z)
            return;
        if (_layer == MaxLayer)
        {
            goto InsertCurrentNode;
        }
        if (box.XLength > XLength / 2 || box.YLength > YLength / 2 || box.ZLength > ZLength / 2)
        {
            goto InsertCurrentNode;
        }
        int targetIndex = -1;
        for (int i = 0; i < 8; i++)
        {
            var subBox = _childBox[i];
            if (subBox.Contains(new Box() { MaxPoint = box.MaxPoint, MinPoint = box.MinPoint }))
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0)
        {
            goto InsertCurrentNode;
        }

        _children ??=
        [
            new Octree(_childBox[0].MinPoint, _childBox[0].MaxPoint, this) { _layer = _layer + 1 },
            new Octree(_childBox[1].MinPoint, _childBox[1].MaxPoint, this) { _layer = _layer + 1 },
            new Octree(_childBox[2].MinPoint, _childBox[2].MaxPoint, this) { _layer = _layer + 1 },
            new Octree(_childBox[3].MinPoint, _childBox[3].MaxPoint, this) { _layer = _layer + 1 },
            new Octree(_childBox[4].MinPoint, _childBox[4].MaxPoint, this) { _layer = _layer + 1 },
            new Octree(_childBox[5].MinPoint, _childBox[5].MaxPoint, this) { _layer = _layer + 1 },
            new Octree(_childBox[6].MinPoint, _childBox[6].MaxPoint, this) { _layer = _layer + 1 },
            new Octree(_childBox[7].MinPoint, _childBox[7].MaxPoint, this) { _layer = _layer + 1 }
        ];
        _children[targetIndex].InsertObject(box);
        return;


    InsertCurrentNode:
        _boundingBoxes.Add(box);
        box.ParentNode = this;
    }

    public void RemoveObject(BaseBounding box)
    {
        if (box.ParentNode == null)
            return;
        box.ParentNode._boundingBoxes.Remove(box);
        if (box.ParentNode.NodeCount == 0)
            box.ParentNode._children = null;

        box.ParentNode = null;
    }

    public void FrustumCulling<T>(List<T> components, Plane[] planes)
    {
        if (_currentBox.TestPlanes(planes) == false)
            return;
        foreach (var subox in _boundingBoxes)
        {
            if (subox.PrimitiveComponent is T t && subox.TestPlanes(planes))
            {
                components.Add(t);
            }
        }
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.FrustumCulling(components, planes);
            }
        }
    }
    public void SphereCulling<T>(List<T> components, Sphere sphere) where T : PrimitiveComponent
    {
        if (sphere.TestBox(_currentBox) == false)
            return;
        foreach (var subox in _boundingBoxes)
        {
            if (subox.PrimitiveComponent is T t && sphere.TestBox(new Box() { MinPoint = subox.MinPoint, MaxPoint = subox.MaxPoint }))
            {
                components.Add(t);
            }
        }
        if (_children != null)
        {
            foreach (var child in _children)
            {
                child.SphereCulling(components, sphere);
            }
        }
    }
}


public abstract class BaseBounding
{
    public BaseBounding(PrimitiveComponent primitiveComponent)
    {
        PrimitiveComponent = primitiveComponent;
    }
    public Octree? ParentNode;

    public PrimitiveComponent PrimitiveComponent;
    public abstract bool TestPlanes(Plane[] planes);

    public abstract float XLength { get; }

    public abstract float YLength { get; }
    public abstract float ZLength { get; }

    public abstract Vector3 MinPoint { get; }
    public abstract Vector3 MaxPoint { get; }


}
public class BoundingBox : BaseBounding
{

    public Box Box;
    public BoundingBox(PrimitiveComponent primitiveComponent) : base(primitiveComponent)
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

public class BoundingSphere(PrimitiveComponent primitiveComponent) : BaseBounding(primitiveComponent)
{
    public Sphere Sphere;

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
