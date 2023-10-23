using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Assets;

namespace Spark.Engine.Components;

public partial class PrimitiveComponent
{
    public Engine Engine => Owner.CurrentWorld.Engine;

    public World World => Owner.CurrentWorld;

    public Level CurrentLevel => Owner.CurrentLevel;

    protected virtual bool ReceieveUpdate => false;
    public virtual bool IsStatic { get; set; } = false;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="actor">所属actor</param>
    public PrimitiveComponent(Actor actor)
    {
        IsDestoryed = false;
        _Owner = actor;
        _Owner.RegistComponent(this);
        OnBeginGame();
        if (ReceieveUpdate)
        {
            this.Owner.CurrentLevel.UpdateManager.RegistUpdate(Update);
        }
    }

    public virtual void AddForce(Vector3 Force)
    {

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
        if (ReceieveUpdate)
        {
            this.Owner.CurrentLevel.UpdateManager.UnregistUpdate(Update);
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
    /// 更新
    /// </summary>
    /// <param name="DeltaTime"></param>
    public virtual void Update(double DeltaTime)
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
                if (_ParentComponent._ChildrenComponent.Contains(this))
                {
                    _ParentComponent._ChildrenComponent.Remove(this);
                }
                _ParentComponent = value;
            }
            _ParentComponent = value;
            if (_ParentComponent != null)
            {
                _ParentComponent._ChildrenComponent.Add(this);
            }
        }
    }

    public IReadOnlyList<PrimitiveComponent> ChildrenComponent => _ChildrenComponent;

    public bool IsDestoryed { protected set; get; }

}

public partial class PrimitiveComponent
{

    public virtual Vector3 WorldLocation
    {
        get => _WorldLocation;
        set =>_WorldLocation = value;
    }

    public virtual Quaternion WorldRotation
    {
        get => _WorldRotation;
        set => _WorldRotation = value;
    }


    public virtual Vector3 WorldScale
    {
        get => _WorldScale;
        set => _WorldScale = value;
    }

    public Vector3 RelativeLocation
    {
        get => RelativeTransform.Translation;
        set => RelativeTransform = MatrixHelper.CreateTransform(value, RelativeRotation, RelativeScale);
    }

    public Quaternion RelativeRotation
    {
        get => RelativeTransform.Rotation();
        set => RelativeTransform = MatrixHelper.CreateTransform(RelativeLocation, value, RelativeScale);
    }

    public Vector3 RelativeScale
    {
        get => RelativeTransform.Scale();
        set => RelativeTransform = MatrixHelper.CreateTransform(RelativeLocation, RelativeRotation, value);
    }
    public Matrix4x4 RelativeTransform
    {
        get
        {
            if (_ParentComponent == null)
                return WorldTransform;
            Matrix4x4.Invert(_ParentComponent.WorldTransform, out var ParentInvertTransform);
            return WorldTransform * ParentInvertTransform;
        }
        set
        {
            Matrix4x4 wt; 
            if (_ParentComponent == null)
                wt = value;
            else
                wt = value * _ParentComponent.WorldTransform;

            WorldLocation = wt.Translation;
            WorldRotation = wt.Rotation();
            WorldScale = wt.Scale();
        }
    }
    public Matrix4x4 WorldTransform
    {
        get => MatrixHelper.CreateTransform(WorldLocation, WorldRotation, WorldScale);
    }

    public Matrix4x4 NormalTransform
    {
        get
        {
            var m = WorldTransform with
            {
                M14 = 0,
                M24 = 0,
                M34 = 0,
                M41 = 0,
                M42 = 0,
                M43 = 0,
                M44 = 1
            };

            Matrix4x4.Invert(m, out var m2);
            m = Matrix4x4.Transpose(m2);

            return m;

        }
    }


    public Vector3 ForwardVector => Vector3.Transform(new Vector3(0, 0, -1), WorldRotation);
    public Vector3 RightVector => Vector3.Transform(new Vector3(1, 0, 0), WorldRotation);
    public Vector3 UpVector => Vector3.Transform(new Vector3(0, 1, 0), WorldRotation);

}
public partial class PrimitiveComponent
{
    private Actor _Owner;

    private PrimitiveComponent? _ParentComponent;

    private List<PrimitiveComponent> _ChildrenComponent = new List<PrimitiveComponent>();

    protected Vector3 _WorldLocation;

    protected Quaternion _WorldRotation;

    protected Vector3 _WorldScale = Vector3.One;

}