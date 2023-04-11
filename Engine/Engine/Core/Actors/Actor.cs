using Silk.NET.OpenGL;
using Spark.Engine.Core.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Actors;

public partial class Actor
{
    /// <summary>
    /// Actor所在关卡
    /// </summary>
    public Level CurrentLevel { get; private set; }

    private bool _ReceieveUpdate;

    protected void ReceieveUpdate()
    {
        if (_ReceieveUpdate) return;
        _ReceieveUpdate = true;
        CurrentLevel.UpdateManager.RegistUpdate(Update);
    }
    /// <summary>
    /// Actor所在世界
    /// </summary>
    public World CurrentWorld { get => CurrentLevel.CurrentWorld; }

    public Actor(Level level)
    {
        CurrentLevel = level;
        level.RegistActor(this);
    }

    public void BeginPlay()
    {
        OnBeginPlay();
    }

    public void Update(double DeltaTime)
    {
        OnUpdate(DeltaTime);
    }

    protected virtual void OnUpdate(double DeltaTime)
    {

    }
    protected virtual void OnBeginPlay()
    {

    }
    public void Destory()
    {
        OnEndPlay();
        foreach (var component in PrimitiveComponents)
        {
            UnregistComponent(component);
        }
        if (_ReceieveUpdate)
        {
            CurrentLevel.UpdateManager.UnregistUpdate(Update);
        }
        CurrentLevel.UnregistActor(this);
    }

    protected virtual void OnEndPlay()
    {

    }


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
        CurrentLevel.RegistComponent(Component);
        foreach (var SubComponent in Component.ChildrenComponent)
        {
            if (PrimitiveComponents.Contains(SubComponent))
            {
                continue;
            }
            _PrimitiveComponents.Add(SubComponent);
            CurrentLevel.RegistComponent(SubComponent);
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
        CurrentLevel.UnregistComponent(Component);
        foreach (var SubComponent in Component.ChildrenComponent)
        {
            if (!PrimitiveComponents.Contains(SubComponent))
            {
                continue;
            }
            _PrimitiveComponents.Remove(SubComponent);
            CurrentLevel.UnregistComponent(SubComponent);
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
