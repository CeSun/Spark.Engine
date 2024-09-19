using System.Numerics;
using Spark.Core.Actors;
using Jitter2.Dynamics;
using Spark.Util;
using Spark.Core.Render;
using Spark.Core.Assets;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Spark.Core.Components;

public enum AttachRelation
{
    KeepRelativeTransform,
    KeepWorldTransform
}
public partial class PrimitiveComponent
{
    public GCHandle WeakGCHandle { get; private set; }
    public WorldObjectState ComponentState { get; private set; }
    public Engine Engine => Owner.World.Engine;
    public World World => Owner.World;

    protected virtual bool ReceiveUpdate => false;
    public virtual bool IsStatic { get; set; } = false;

    private bool _castShadow = true;
    public bool CastShadow 
    {
        get => _castShadow; 
        set =>  _castShadow = value;
    }
    private bool _hidden = false;
    public bool Hidden 
    {
        get => _hidden;
        set => _hidden = value;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="actor">所属actor</param>
    public PrimitiveComponent(Actor actor, bool registerToWorld = true)
    {
        WeakGCHandle = GCHandle.Alloc(this, GCHandleType.WeakTrackResurrection);
        _owner = actor;
        ComponentState = WorldObjectState.Invaild;
        _owner.AddComponent(this);
        if (registerToWorld)
        {
            RegisterToWorld();
        }
    }

    public virtual void RegisterToWorld()
    {
        if (ComponentState != WorldObjectState.Invaild)
            return;
        ComponentState = WorldObjectState.Registered;
        if (ReceiveUpdate)
        {
            Owner.World.UpdateManager.RegisterUpdate(Update);
        }
        var getProxyDelegate = GetRenderProxyDelegate();
        if ( getProxyDelegate != null && Engine.SceneRenderer != null && World.RenderWorld != null)
        {
            Engine.SceneRenderer.AddRunOnRendererAction(renderer =>
            {
                var proxy = getProxyDelegate(renderer);
                if (proxy != null)
                {
                    World.RenderWorld.AddPrimitiveComponentProxy(this, proxy);
                }
            });
        }
    }

    public virtual void UnregisterFromWorld()
    {
        if (ReceiveUpdate)
        {
            Owner.World.UpdateManager.UnregisterUpdate(Update);
        }
        if (Engine.SceneRenderer != null && World.RenderWorld != null)
        {
            Engine.SceneRenderer.AddRunOnRendererAction(renderer =>
            {
                World.RenderWorld.RemovePrimitiveComponentProxy(this);
            });
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
    public void Destory()
    {
        if (ComponentState != WorldObjectState.Began)
            return;
        OnEndPlay();
        UnregisterFromWorld();
        ComponentState = WorldObjectState.Destoryed;
    }

    protected void BeginPlay()
    {
        if (ComponentState != WorldObjectState.Registered)
            return;
        ComponentState = WorldObjectState.Began;
        OnBeginPlay();
    }

    protected virtual void OnBeginPlay()
    {

    }

    protected virtual void OnEndPlay()
    {

    }

    public virtual Func<IRenderer, PrimitiveComponentProxy>? GetRenderProxyDelegate()
    {
        return null;
    }

    /// <summary>
    /// 更新
    /// </summary>
    /// <param name="DeltaTime"></param>
    public void Update(double DeltaTime)
    {
        if (ComponentState != WorldObjectState.Began)
            return;
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
        get => _owner;
        set
        {
            if (value == null)
            {
                return;
            }
            _owner = value;
        }
    }

    public PrimitiveComponent? ParentComponent 
    {
        get => _parentComponent;
        set
        {
            if (_parentComponent != null)
            {
                if (_parentComponent._childrenComponent.Contains(this))
                {
                    _parentComponent._childrenComponent.Remove(this);
                }
                _parentComponent = value;
            }
            _parentComponent = value;
            if (_parentComponent != null)
            {
                _parentComponent._childrenComponent.Add(this);
            }
        }
    }

    public IReadOnlyList<PrimitiveComponent> ChildrenComponent => _childrenComponent;

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
        get => _relativeLocation;
        set
        {
            _relativeLocation = value;
        }
    }

    public virtual Quaternion RelativeRotation
    {

        get => _relativeRotation;
        set
        {
            _relativeRotation = value;
        }
    }

    public virtual Vector3 RelativeScale
    {
        get => _relativeScale;
        set
        {
            _relativeScale = value;
        }
    }

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


    public PrimitiveComponentProperties GetPrimitiveComponentProperties()
    {
        var properties = new PrimitiveComponentProperties();

        properties.WorldTransform = WorldTransform;
        properties.CastShadow = _castShadow;
        properties.Hidden = _hidden;

        properties.CustomProperties = GetSubComponentProperties();

        return properties;
    }

    public virtual IntPtr GetSubComponentProperties()
    {
        return IntPtr.Zero;
    }
}
public partial class PrimitiveComponent
{
    private Actor _owner;

    private PrimitiveComponent? _parentComponent;

    private List<PrimitiveComponent> _childrenComponent = new List<PrimitiveComponent>();

    public Vector3 _relativeLocation;

    public Quaternion _relativeRotation;

    public Vector3 _relativeScale = Vector3.One;

}
public partial class PrimitiveComponent
{
    public virtual RigidBody? RigidBody { get; }
}

public struct PrimitiveComponentProperties
{
    private IntPtr Destructors;

    public PrimitiveComponentProperties()
    {
        unsafe
        {
            delegate* unmanaged[Cdecl]<IntPtr, void> p = &destory;
            Destructors = (nint)p;
        }
    }

    public Matrix4x4 WorldTransform;

    public bool CastShadow;

    public bool Hidden;

    public IntPtr CustomProperties;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void destory(IntPtr intptr)
    {


    }
}
