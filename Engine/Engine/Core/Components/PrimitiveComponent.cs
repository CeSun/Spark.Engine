using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Spark.Engine.Core.Actors;

namespace Spark.Engine.Core.Components;

public partial class PrimitiveComponent
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="actor">所属actor</param>
    public PrimitiveComponent(Actor actor)
    {
        IsDestoryed = false;
        Owner = actor;
        Owner.RegistComponent(this);
        OnBeginGame();
    }

    public void Destory()
    {
        if (IsDestoryed)
        {
            return;
        }
        OnEndGame();
        Owner.UnregistComponent(this);
        if (ParentComponent != null)
        {
            ParentComponent = null;
        }
        IsDestoryed = true;
    }

    protected virtual void OnBeginGame()
    {

    }

    protected virtual void OnEndGame()
    {

    }


    /// <summary>
    /// 渲染
    /// </summary>
    /// <param name="DeltaTime"></param>
    public virtual void Render(double DeltaTime)
    {

    }
    
    /// <summary>
    /// 组件拥有者
    /// </summary>
    public Actor Owner
    {
        get => _Owner;
        set
        {
            if (value == null)
            {
                return;
            }
            _Owner = value;
        }
    }

    public PrimitiveComponent? ParentComponent 
    {
        get => _ParentComponent;
        set
        {
            if (_ParentComponent != null)
            {
                UpdateTransform();
                if (_ParentComponent._ChildrenComponent.Contains(this))
                {
                    _ParentComponent._ChildrenComponent.Remove(this);
                }
                _ParentComponent = value;
            }
            _ParentComponent = value;
            if (_ParentComponent != null)
            {
                if (TransformDirty == false && _ParentComponent.TransformDirty == true)
                {
                    var l = _WorldLocation;
                    var r = _WorldRotation;
                    var s = _WorldScale;
                    UpdateRelativeTransformFromWorldTransfor(l, r, s);
                }
                _ParentComponent._ChildrenComponent.Add(this);
            }
        }
    }

    public IReadOnlyList<PrimitiveComponent> ChildrenComponent => _ChildrenComponent;

    public bool IsDestoryed { protected set; get; }
    public bool TransformDirty 
    { 
        protected set
        {
            if (_TransformDirty == false)
            {
                _TransformDirty = true;
            }
            foreach (var child in ChildrenComponent)
            {
                child.TransformDirty = true;
            }
        }
        get => _TransformDirty;
    }

    public Vector3 WorldLocation
    {
        get
        {

            if (TransformDirty == true)
            {
                UpdateTransform();
                _WorldLocation = _WorldTransform.Translation;
            }
            return _WorldLocation;
        }
        set
        {
            UpdateRelativeTransformFromWorldTransfor(value, WorldRotation, WorldScale);
        }
    }
    private void UpdateRelativeTransformFromWorldTransfor(Vector3 pWorldLocation, Quaternion pWorldRotation,  Vector3 pWorldScale)
    {
        var worldTransform = MatrixHelper.CreateTransform(pWorldLocation, pWorldRotation, pWorldScale);
        Matrix4x4 relativeTransform = default;
        if (ParentComponent == null)
        {
            relativeTransform = worldTransform;
        }
        else
        {
            if (Matrix4x4.Invert(ParentComponent.WorldTransform, out var WorldInvertTransform))
            {

                relativeTransform = WorldInvertTransform * worldTransform;
            }
        }
        RelativeLocation = relativeTransform.Translation;
        RelativeRotation = relativeTransform.Rotation();
        RelativeScale = relativeTransform.Scale();
    }

    public Quaternion WorldRotation
    {
        get
        {
            if (TransformDirty == true)
            {
                UpdateTransform();
                _WorldRotation = WorldTransform.Rotation();
            }
            return _WorldRotation;
        }
        set
        {
            UpdateRelativeTransformFromWorldTransfor(WorldLocation, value, WorldScale);
        }
    }


    public Vector3 WorldScale
    {
        get
        {
            if (TransformDirty == true)
            {
                UpdateTransform();
                _WorldScale = WorldTransform.Scale();
            }
            return _WorldScale;
        }
        set
        {
            UpdateRelativeTransformFromWorldTransfor(WorldLocation, WorldRotation, value);
        }
    }

    public Vector3 RelativeLocation
    {
        get => _RelativeLocation;
        set
        {
            _RelativeLocation = value;
            TransformDirty = true;
        }
    }

    public Quaternion RelativeRotation
    {
        get => _RelativeRotation;
        set
        {
            _RelativeRotation = value;
            TransformDirty = true;
        }
    }

    public Vector3 RelativeScale
    {
        get => _RelativeScale;
        set
        {
            _RelativeScale = value;
            
            TransformDirty = true;
        }
    }
    public Matrix4x4 RelativeTransform
    {
        get
        {
            if (TransformDirty == true)
            {
                UpdateTransform();
            }
            return _RelativeTransform;
        }
    }
    public Matrix4x4 WorldTransform
    {
        get
        {
            if (TransformDirty == true)
            {
                UpdateTransform();
            }
            return _WorldTransform;
        }
    }

    private void UpdateTransform()
    {
        if (_TransformDirty == false)
            return;
        var RootComponent = GetRootTransformDirtyNode();
        RootComponent.UpdateSelfTransform();
    }

    private void UpdateSelfTransform()
    {
        _RelativeTransform = MatrixHelper.CreateTransform(RelativeLocation, RelativeRotation, RelativeScale);
        if (ParentComponent == null)
        {
            _WorldTransform = _RelativeTransform;
        }
        else
        {
            _WorldTransform = ParentComponent._WorldTransform * _RelativeTransform;
        }
        _TransformDirty = false;
        foreach (var child in ChildrenComponent)
        {
            if (child.TransformDirty == true)
            {
                child.UpdateSelfTransform();
            }
        }
    }

    private PrimitiveComponent GetRootTransformDirtyNode()
    {
        if (ParentComponent == null)
            return this;
        if (ParentComponent.TransformDirty == false)
            return this;
        return ParentComponent.GetRootTransformDirtyNode();
    }
}


public partial class PrimitiveComponent
{

    private bool _TransformDirty;

    private Actor _Owner;

    private PrimitiveComponent? _ParentComponent;

    private List<PrimitiveComponent> _ChildrenComponent = new List<PrimitiveComponent>();

    private Vector3 _WorldLocation;

    private Quaternion _WorldRotation;

    private Vector3 _WorldScale;

    private Vector3 _RelativeLocation;

    private Quaternion _RelativeRotation;

    private Vector3 _RelativeScale = Vector3.One;

    public Matrix4x4 _WorldTransform;

    public Matrix4x4 _RelativeTransform;
}