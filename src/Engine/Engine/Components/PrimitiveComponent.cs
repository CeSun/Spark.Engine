using Spark.Engine.Actors;
using Spark.Engine.GameLevel;
using System.Numerics;

namespace Spark.Engine.Components;

public class PrimitiveComponent
{
    public Level CurrentLevel { get; private set; }
    public PrimitiveComponent(Level level) 
    { 
        CurrentLevel = level;
        Children = new List<PrimitiveComponent>();
    }

    private PrimitiveComponent? _Parent;
    public PrimitiveComponent? Parent
    {
        get => _Parent;
        set
        {
            if (_Parent == value) 
                return;
            if (_Parent != null)
            {
                _Parent.Children.Remove(this);
            }
            _Parent = value;
            if (_Parent != null)
            {
                _Parent.Children.Add(this);
            }
        }
    }

    private List<PrimitiveComponent> Children;
    
    private Vector3 _RelativeLocation;
    private Vector3 _RelativeScale;
    private Quaternion _RelativeQuaternion;
    private Vector3 _WorldLocation;
    private Vector3 _WorldScale;
    private Quaternion _WorldRotation;

    private bool TransformDirty;

    public Vector3 RelativeLocation 
    { 
        get => _RelativeLocation;
        set
        {
            Children.ForEach(child => child.TransformDirty = true);
            _RelativeLocation = value;
        }
    }
    public Vector3 RelativeScale 
    {
        get => _RelativeScale;
        set
        {
            Children?.ForEach(child => child.TransformDirty = true);
            _RelativeScale = value;
        } 
    }
    public Quaternion RelativeQuaternion 
    {
        get => _RelativeQuaternion;
        set
        {
            Children.ForEach(child => child.TransformDirty = true);
            _RelativeQuaternion = value;
        }
    }
    public Vector3 WorldLocation 
    {
        get
        {
            if (TransformDirty)
            {
                CalcWorldTransform();
            }
            return _WorldLocation;
        }
        set
        {
            _WorldLocation = value;
            CalcRelativeTransform();
        }
    }
    public Vector3 WorldScale 
    { 
        get
        {
            if (TransformDirty)
            {
                CalcWorldTransform();
            }
            return _WorldScale;
        }
        set
        {
            _WorldScale = value;
            CalcRelativeTransform();
        }
    }
    public Quaternion WorldRotation
    {
        get
        {
            if (TransformDirty)
            {
                CalcWorldTransform();
            }
            return _WorldRotation;
        }
        set
        {
            _WorldRotation = value;
            CalcRelativeTransform();
        }
    }

    public Matrix4x4 RelativeTransform { get; private set; }
    public Matrix4x4 WorldTransform { get; private set; }
    private void CalcWorldTransform()
    {

    }

    public void CalcRelativeTransform()
    {

    }
}
