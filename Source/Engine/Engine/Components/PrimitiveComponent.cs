using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Physics;

namespace Spark.Engine.Components;

public enum AttachRelation
{
    KeepRelativeTransform,
    KeepWorldTransform
}
public partial class PrimitiveComponent
{
    public Engine Engine => Owner.CurrentWorld.Engine;

    public GL gl => Engine.Gl;
    public World World => Owner.CurrentWorld;

    public Level CurrentLevel => Owner.CurrentLevel;
    public Jitter2.World PhysicsWorld => CurrentLevel.PhysicsWorld;
    protected virtual bool ReceieveUpdate => false;
    public virtual bool IsStatic { get; set; } = false;

    public virtual BaseBounding? Bounding
    {
        get => null;
    }

    public void UpdateOctree()
    {
        if (Bounding == null)
            return;
        CurrentLevel.RenderObjectOctree.RemoveObject(Bounding);
        CurrentLevel.RenderObjectOctree.InsertObject(Bounding);
    }
    public bool IsCastShadowMap { get; set; } = true;
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="actor">所属actor</param>
    public PrimitiveComponent(Actor actor)
    {
        IsDestoryed = false;
        _Owner = actor;
        _Owner.RegistComponent(this);
        if (Owner.RootComponent == null)
        {
            Owner.RootComponent = this;
        }
        if (ReceieveUpdate)
        {
            this.Owner.CurrentLevel.UpdateManager.RegistUpdate(Update);
        }
    }

    public void AttachTo(PrimitiveComponent Parent, string socket = "")
    {
        AttachTo(Parent, socket, Matrix4x4.Identity, AttachRelation.KeepWorldTransform);
    }

    private string? AttachToParentSocket;
    private Matrix4x4 ParentOffsetTransform = Matrix4x4.Identity;
    public void AttachTo(PrimitiveComponent Parent, string socket, Matrix4x4 OffsetTransform, AttachRelation attachRelation)
    {
        if (ParentComponent != null)
            return;
        AttachToParentSocket = socket;
        ParentOffsetTransform = OffsetTransform;
        if (attachRelation == AttachRelation.KeepWorldTransform)
        {
            var worldTransform = WorldTransform;
            ParentComponent = Parent;
            WorldTransform = worldTransform;
        }
        ParentComponent = Parent;

    }

    public void DettachFrom(AttachRelation attachRelation)
    {
        if (ParentComponent == null)
            return;
        var worldTransform = WorldTransform;
        ParentComponent = null;
        ParentOffsetTransform = Matrix4x4.Identity;
        AttachToParentSocket = null;

        if (attachRelation == AttachRelation.KeepWorldTransform)
        {
            WorldTransform = worldTransform;
        }

    }

    protected virtual Matrix4x4 GetSocketWorldTransform(string socket)
    {
        return WorldTransform;
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
        OnEndPlay();
        if (RigidBody != null)
        {
            RigidBody.Tag = null;
            CurrentLevel.PhysicsWorld.Remove(RigidBody);
        }
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

    protected void BeginPlay()
    {
        OnBeginPlay();
    }

    protected virtual void OnBeginPlay()
    {

    }

    protected virtual void OnEndPlay()
    {

    }


    /// <summary>
    /// 渲染
    /// </summary>
    /// <param name="DeltaTime"></param>
    public virtual void Render(double DeltaTime)
    {
    }

    private bool IsBegined = false;
    /// <summary>
    /// 更新
    /// </summary>
    /// <param name="DeltaTime"></param>
    public void Update(double DeltaTime)
    {
        if (IsBegined == false)
        {
            IsBegined = true;
            BeginPlay();
        }
        OnUpdate(DeltaTime);
    }

    public virtual void OnUpdate(double DeltaTime)
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
        get => WorldTransform.Translation;
        set => WorldTransform = MatrixHelper.CreateTransform(value, WorldRotation, WorldScale);
    }

    public virtual Quaternion WorldRotation
    {
        get => WorldTransform.Rotation();
        set => WorldTransform = MatrixHelper.CreateTransform(WorldLocation, value, WorldScale);
    }


    public virtual Vector3 WorldScale
    {
        get => WorldTransform.Scale();
        set => WorldTransform = MatrixHelper.CreateTransform(WorldLocation, WorldRotation, value);
    }

    public virtual Vector3 RelativeLocation
    {
        get => _RelativeLocation;
        set
        {
            _RelativeLocation = value;
        }
    }

    public virtual Quaternion RelativeRotation
    {

        get => _RelativeRotation;
        set
        {
            _RelativeRotation = value;
        }
    }

    public virtual Vector3 RelativeScale
    {
        get => _RelativeScale;
        set
        {
            _RelativeScale = value;
        }
    }

    [Property(IsDispaly = false)]
    public Matrix4x4 RelativeTransform
    {
        get => MatrixHelper.CreateTransform(RelativeLocation, RelativeRotation, RelativeScale);
        set
        {
            RelativeLocation = value.Translation;
            RelativeRotation = value.Rotation();
            RelativeScale = value.Scale();
        }
    }
    public Matrix4x4 WorldTransform
    {
        get
        {
            return RelativeTransform * ParentWorldTransform;
        }
        set
        {
            Matrix4x4.Invert(ParentWorldTransform, out var InverseParentMatrix);

            var tmpRelativeTransform = value * InverseParentMatrix;

            RelativeTransform = tmpRelativeTransform;
        }
    }


    public Matrix4x4 ParentWorldTransform
    {
        get
        {
            if (ParentComponent == null)
                return Matrix4x4.Identity;
            return ParentOffsetTransform * ParentComponent.GetSocketWorldTransform(AttachToParentSocket ?? "");
        }
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

    public void PhysicsUpdateTransform(Vector3 Location, in Matrix4x4 matrix4X4)
    {
        this.WorldLocation = Location;
        this.WorldRotation = Quaternion.CreateFromRotationMatrix(matrix4X4);
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

    public Vector3 _RelativeLocation;

    public Quaternion _RelativeRotation;

    public Vector3 _RelativeScale = Vector3.One;

}
public partial class PrimitiveComponent
{
    public virtual RigidBody? RigidBody { get; }
    public virtual Shape? Shape { get; }
}