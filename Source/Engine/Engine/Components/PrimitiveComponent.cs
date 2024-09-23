using System.Numerics;
using Spark.Core.Actors;
using Jitter2.Dynamics;
using Spark.Util;
using Spark.Core.Render;
using Spark.Core.Assets;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Silk.NET.OpenGLES;

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

    protected virtual int propertiesStructSize => Marshal.SizeOf<PrimitiveComponentProperties>();
    protected virtual bool ReceiveUpdate => false;
    public virtual bool IsStatic { get; set; } = false;

    private bool _castShadow = true;
    public bool CastShadow 
    {
        get => _castShadow; 
        set => ChangeProperty(ref _castShadow, value);
    }
    private bool _hidden = false;
    public bool Hidden 
    {
        get => _hidden;
        set => ChangeProperty(ref _hidden, value);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="actor">所属actor</param>
    public PrimitiveComponent(Actor actor, bool registerToWorld = true)
    {
        WeakGCHandle = GCHandle.Alloc(this, GCHandleType.Weak);
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
        MakeRenderDirty();
    }

    protected void ChangeProperty<T>(ref T Field, in T NewValue)
    {
        Field = NewValue;
        MakeRenderDirty();
    }

    protected void ChangeAssetProperty<T>(ref T? Field, in T? NewValue) where T : AssetBase
    {
        if (Engine.SceneRenderer != null && NewValue != null)
            NewValue.PostProxyToRenderer(Engine.SceneRenderer);
        ChangeProperty(ref Field, in NewValue);
    }

    protected void MakeRenderDirty()
    {
        if (ComponentState != WorldObjectState.Began && ComponentState != WorldObjectState.Registered)
            return;
        World.AddRenderDirtyComponent(this);
    }

    public virtual void UnregisterFromWorld()
    {
        if (ReceiveUpdate)
        {
            Owner.World.UpdateManager.UnregisterUpdate(Update);
        }
        MakeRenderDirty();
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
        set => ChangeProperty(ref _relativeLocation, value);
    }

    public virtual Quaternion RelativeRotation
    {

        get => _relativeRotation;
        set => ChangeProperty(ref _relativeRotation, value);
    }

    public virtual Vector3 RelativeScale
    {
        get => _relativeScale;
        set => ChangeProperty(ref _relativeScale, value);
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
            MakeRenderDirty();
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


    public virtual IntPtr GetPrimitiveComponentProperties()
    {
        var ptr = AllocPropertiesMemory();
        ref var properties = ref UnsafeHelper.AsRef<PrimitiveComponentProperties>(ptr);
        properties.ComponentWeakGChandle = WeakGCHandle;
        properties.ComponentState = ComponentState;
        properties.WorldTransform = WorldTransform;
        properties.CastShadow = _castShadow;
        properties.Hidden = _hidden;
        properties.CreateProxyObject = GetCreateProxyObjectFunctionPointer();
        return ptr;
    }

    public virtual IntPtr AllocPropertiesMemory()
    {
        if (propertiesStructSize <= 0)
            return IntPtr.Zero;
        return Marshal.AllocHGlobal(propertiesStructSize);
    }

    public virtual IntPtr GetCreateProxyObjectFunctionPointer()
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

public class PrimitiveComponentProxy
{
    public Vector3 Forward;
    public Vector3 Right;
    public Vector3 Up;
    public Quaternion WorldRotation;
    public Vector3 WorldLocation;
    public Vector3 WorldScale;
    public bool Hidden { get; set; }
    public bool CastShadow { get; set; }
    public Matrix4x4 Trasnform { get; set; }
    public virtual void UpdateProperties(IntPtr propertiesPtr, BaseRenderer renderer)
    {
        ref var properties = ref UnsafeHelper.AsRef<PrimitiveComponentProperties>(propertiesPtr);
        Hidden = properties.Hidden;
        CastShadow = properties.CastShadow;
        Trasnform = properties.WorldTransform;

        WorldRotation = Trasnform.Rotation();
        WorldLocation = Trasnform.Translation;
        WorldScale = Trasnform.Scale();

        Forward = Vector3.Transform(new Vector3(0, 0, -1), WorldRotation);
        Right = Vector3.Transform(new Vector3(1, 0, 0), WorldRotation);
        Up = Vector3.Transform(new Vector3(0, 1, 0), WorldRotation);
    }

    public virtual void RebuildGpuResource(GL gl)
    {

    }

    public virtual void DestoryGpuResource(GL gl)
    {

    }
}

public struct PrimitiveComponentProperties
{
    public IntPtr CreateProxyObject;

    public GCHandle ComponentWeakGChandle;

    public WorldObjectState ComponentState;

    public Matrix4x4 WorldTransform;

    public bool CastShadow;

    public bool Hidden;

}

