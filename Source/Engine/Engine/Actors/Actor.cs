using Spark.Engine.Components;
using System.Numerics;

namespace Spark.Engine;

public partial class Actor
{

    public bool IsEditorActor = false;
    /// <summary>
    /// Actor所在关卡
    /// </summary>
    protected virtual bool ReceiveUpdate => false;
 
    /// <summary>
    /// Actor所在世界
    /// </summary>
    public World.World CurrentWorld { get; set; }

    public Actor(World.World world)
    {
        CurrentWorld = world;
        if (ReceiveUpdate)
        {
            CurrentWorld.UpdateManager.RegisterUpdate(Update);
        }
    }

    public void BeginPlay()
    {
        _isBegan = true;
        OnBeginPlay();
    }
    private bool _isBegan = false;
    public void Update(double deltaTime)
    {
        if (_isBegan == false)
            BeginPlay();
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
        OnEndPlay();
        foreach (var component in PrimitiveComponents.ToList())
        {
            UnregistComponent(component);
        }
        if (ReceiveUpdate)
        {
            CurrentWorld.UpdateManager.UnregisterUpdate(Update);
        }
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
    List<PrimitiveComponent> _PrimitiveComponents = new List<PrimitiveComponent>();
    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents { get { return _PrimitiveComponents; } }

    /// <summary>
    /// 向Actor上注册组件
    /// </summary>
    /// <param name="Component"></param>
    public void RegistComponent(PrimitiveComponent Component)
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
    public void UnregistComponent(PrimitiveComponent Component)
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
