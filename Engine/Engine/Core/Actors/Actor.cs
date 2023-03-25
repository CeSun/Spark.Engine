using Silk.NET.OpenGL;
using Spark.Engine.Core.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Actors;

public partial class Actor
{
    /// <summary>
    /// Actor所在关卡
    /// </summary>
    public Level CurrentLevel { get; private set; }

    /// <summary>
    /// Actor所在世界
    /// </summary>
    public World CurrentWorld { get => CurrentLevel.CurrentWorld; }

    public Actor(Level level)
    {
        CurrentLevel = level;
    }

    public void BeginPlay()
    {
        OnBeginPlay();
    }

    protected virtual void OnBeginPlay()
    {

    }
    public void Destory() 
    {
        foreach(var component in PrimitiveComponents)
        {
            UnregistComponent(component);
        }
        OnEndPlay();
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
}
