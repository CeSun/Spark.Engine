using Silk.NET.Core.Native;
using Spark.Engine.Actors;
using Spark.Engine.GameLevel;
using Spark.Engine.Render;
using Spark.Engine.Render.Proxy;
using System.Numerics;

namespace Spark.Engine.Components;

public partial class PrimitiveComponent
{
    protected RenderThread RenderThread { get => CurrentLevel.Engine.RenderThread; }
    public Level CurrentLevel { get; private set; }
    public PrimitiveProxy PrimitiveProxy { get; private set; }
    public PrimitiveComponent(Level level) 
    { 
        CurrentLevel = level;
        Children = new List<PrimitiveComponent>();
        PrimitiveProxy = CreateProxy();
        level.AddComponent(this);
    }
    
    protected virtual PrimitiveProxy CreateProxy()
    {
        return new PrimitiveProxy();
    }
    public bool NeedUpdateTransformToRenderThread { private set; get; }

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
            CalcRelativeTransform();
        }
    }

    private List<PrimitiveComponent> Children;
    
    private Vector3 _RelativeLocation;
    private Vector3 _RelativeScale;
    private Quaternion _RelativeQuaternion;
    private Vector3 _WorldLocation;
    private Vector3 _WorldScale;
    private Quaternion _WorldRotation;
    public Vector3 RelativeLocation 
    { 
        get => _RelativeLocation;
        set
        {
            _RelativeLocation = value;
            CalcWorldTransform();
        }
    }
    public Vector3 RelativeScale 
    {
        get => _RelativeScale;
        set
        {
            _RelativeScale = value;
            CalcWorldTransform();
        } 
    }
    public Quaternion RelativeQuaternion 
    {
        get => _RelativeQuaternion;
        set
        {
            _RelativeQuaternion = value;
            CalcWorldTransform();
        }
    }
    public Vector3 WorldLocation 
    {
        get =>_WorldLocation;
        set
        {
            _WorldLocation = value;
            CalcRelativeTransform();
            NeedUpdateTransformToRenderThread = true;
        }
    }
    public Vector3 WorldScale 
    { 
        get =>_WorldScale;
        set
        {
            _WorldScale = value;
            CalcRelativeTransform();
            NeedUpdateTransformToRenderThread = true;
        }
    }
    public Quaternion WorldRotation
    {
        get => _WorldRotation;
        set
        {
            _WorldRotation = value;
            CalcRelativeTransform();
            NeedUpdateTransformToRenderThread = true;
        }
    }

    private Matrix4x4 _RelativeTransform;
    private Matrix4x4 _WorldTransform;
    public Matrix4x4 RelativeTransform 
    { 
        get => _RelativeTransform;
    }
    public Matrix4x4 WorldTransform 
    { 
        get => _WorldTransform;
    }
    private void CalcWorldTransform()
    {
        if (Parent == null)
        {
            _WorldTransform = RelativeTransform;
        }
        else
        {
            _WorldTransform = Parent.WorldTransform * RelativeTransform;
        }
        _WorldLocation = _WorldTransform.Translation;
        _WorldRotation = _WorldTransform.Rotation();
        _WorldScale = _WorldTransform.Scale();
        NeedUpdateTransformToRenderThread = true;
    }
    private void CalcRelativeTransform()
    {
        if (Parent == null)
        {
            _RelativeTransform = WorldTransform;
        }
        else
        {
            Matrix4x4.Invert(Parent.WorldTransform, out var InvertParentTransform);
            _RelativeTransform = InvertParentTransform * WorldTransform;
        }
        _RelativeLocation = _RelativeTransform.Translation;
        _RelativeQuaternion = _RelativeTransform.Rotation();
        _RelativeScale = _RelativeTransform.Scale();
    }

    public Vector3 Up => Vector3.Transform(Vector3.UnitY, Matrix4x4.CreateFromQuaternion(WorldRotation));
    public Vector3 Forward => Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateFromQuaternion(WorldRotation));
    public Vector3 Right => Vector3.Transform(Vector3.UnitX * -1, Matrix4x4.CreateFromQuaternion(WorldRotation));
}



public partial class PrimitiveComponent
{
    public void Start()
    {
        RenderThread.AddCommand(rt =>
        {
            rt.Scene.AddPrimitive(PrimitiveProxy);
        });
    }

    public void Update(double DeltaTime)
    {
        if (NeedUpdateTransformToRenderThread)
        {
            var localTransform = WorldTransform;
            RenderThread.AddCommand(rt =>
            {
                PrimitiveProxy.Transform = localTransform;
            });
        }
    }

    public void End()
    {
        RenderThread.AddCommand(rt =>
        {
            rt.Scene.RemovePrimitive(PrimitiveProxy);
        });
    }
}