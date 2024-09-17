using Spark.Core.Components;
using System.Numerics;

namespace Spark.Core.Actors;

public partial class Actor
{

    public WorldObjectState ActorState { get; private set; }
    /// <summary>
    /// Actor所在关卡
    /// </summary>
    protected virtual bool ReceiveUpdate => false;
 
    /// <summary>
    /// Actor所在世界
    /// </summary>
    public World World { get; set; }

    public Actor(World world, bool registerToWorld = true)
    {
        ActorState = WorldObjectState.Invaild;
        World = world;
        if (registerToWorld)
        {
            RegisterToWorld();
        }
    }

    public virtual void RegisterToWorld()
    {
        if (ActorState == WorldObjectState.Invaild)
            return;
        ActorState = WorldObjectState.Registered;
        World.AddActor(this);
        if (ReceiveUpdate)
        {
            World.UpdateManager.RegisterUpdate(Update);
        }
        foreach(var component in _PrimitiveComponents)
        {
            component.RegisterToWorld();
        }
        
    }

    public virtual void UnregisterFromWorld()
    {
        World.RemoveActor(this);
        if (ReceiveUpdate)
        {
            World.UpdateManager.UnregisterUpdate(Update);
        }
        foreach (var component in _PrimitiveComponents)
        {
            component.UnregisterFromWorld();
        }
    }


    public void BeginPlay()
    {
        if (ActorState != WorldObjectState.Registered)
            return;
        ActorState = WorldObjectState.Began;
        OnBeginPlay();
    }
    public void Update(double deltaTime)
    {
        if (ActorState != WorldObjectState.Began)
            return;
        OnUpdate(deltaTime);
    }

    protected virtual void OnUpdate(double deltaTime)
    {

    }
    protected virtual void OnBeginPlay()
    {

    }

    public void Destory()
    {
        if (ActorState != WorldObjectState.Began)
            return;
        OnEndPlay();
        UnregisterFromWorld();
        ActorState = WorldObjectState.Destoryed;
    }

    protected virtual void OnEndPlay()
    {

    }

    public Vector3 ForwardVector => Vector3.Transform(new Vector3(0, 0, -1), WorldRotation);
    public Vector3 RightVector => Vector3.Transform(new Vector3(1, 0, 0), WorldRotation);
    public Vector3 UpVector => Vector3.Transform(new Vector3(0, 1, 0), WorldRotation);

}

public partial class Actor
{
    public PrimitiveComponent? RootComponent;

    public Vector3 WorldLocation
    {
        get 
        {
            if (RootComponent == null)
                return Vector3.Zero;
            return RootComponent.WorldLocation;
        }
        set
        {
            if (RootComponent == null)
                return;
            RootComponent.WorldLocation = value;
        }
    }
    public Quaternion WorldRotation
    {
        get
        {
            if (RootComponent == null)
                return Quaternion.Zero;
            return RootComponent.WorldRotation;
        }
        set
        {
            if (RootComponent == null)
                return;
            RootComponent.WorldRotation = value;
        }
    }
    public Vector3 WorldScale
    {
        get
        {
            if (RootComponent == null)
                return Vector3.One;
            return RootComponent.WorldScale;
        }
        set
        {
            if (RootComponent == null)
                return;
            RootComponent.WorldScale = value;
        }
    }



}

/// <summary>
/// Component
/// </summary>
public partial class Actor
{
    HashSet<PrimitiveComponent> _PrimitiveComponents = new HashSet<PrimitiveComponent>();
    public IReadOnlySet<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;

    /// <summary>
    /// 向Actor上注册组件
    /// </summary>
    /// <param name="Component"></param>
    public void AddComponent(PrimitiveComponent Component)
    {
        if (PrimitiveComponents.Contains(Component))
        {
            return;
        }
        _PrimitiveComponents.Add(Component);
        foreach (var SubComponent in Component.ChildrenComponent)
        {
            if (PrimitiveComponents.Contains(SubComponent))
            {
                continue;
            }
            _PrimitiveComponents.Add(SubComponent);
        }
    }

    /// <summary>
    /// 从Actor上注销组件
    /// </summary>
    /// <param name="Component"></param>
    public void RemoveComponent(PrimitiveComponent Component)
    {
        if (!PrimitiveComponents.Contains(Component))
        {
            return;
        }
        _PrimitiveComponents.Remove(Component);
        foreach (var SubComponent in Component.ChildrenComponent)
        {
            if (!PrimitiveComponents.Contains(SubComponent))
            {
                continue;
            }
            _PrimitiveComponents.Remove(SubComponent);
        }
    }


    public T? GetComponent<T>() where T : PrimitiveComponent
    {
        foreach(var comp in PrimitiveComponents)
        {
            if (comp is T c)
            {
                return c;
            }
        }
        return null;
    }

    public List<T> GetComponents<T>() where T : PrimitiveComponent
    {
        var list = new List<T>();
        foreach(var comp in PrimitiveComponents)
        {
            if (comp is T c)
            {
                list.Add(c);
            }
        }
        return list;
    }
}


public enum WorldObjectState
{
    Invaild,
    Registered,
    Began,
    Destoryed,
}
