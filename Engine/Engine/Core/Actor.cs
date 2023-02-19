using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class Actor
{
    public Actor()
    {
        FunctionalComponents = new List<FunctionalComponent>();
        SceneComponents = new List<SceneComponent>();
        Console.WriteLine(this.GetType().FullName);
        InitComponentFromAttribute();
    }

    public Actor SpawnActor<T>() where T : Actor, new()
    {
        var actor = new Actor() { Level = this.Level };
        Level.ActorManager.RegistActor(actor);
        return actor;
    }

    private Level? _Level;
    public Level Level 
    {
        get 
        {
            if (_Level == null)
            {
                throw new Exception("没有找到关卡");
            }
            return _Level;
        } 
        private set
        {
            _Level = value;
        }
    }
    public virtual void InitComponentFromAttribute()
    {

    }
    public SceneComponent? RootComponent { get; set; }
    internal void AddComponent(FunctionalComponent Component)
    {
        if (Component == null)
            return;
        if (FunctionalComponents.Contains(Component))
        {
            return;
        }
        FunctionalComponents.Add(Component);
    }                                        
    internal void AddComponent(SceneComponent Component)
    {
        if (Component == null)
            return;
        if (SceneComponents.Contains(Component))
        {
            return;
        }
        SceneComponents.Add(Component);
    }
    internal void RemoveComponent(SceneComponent Component)
    {
        if (Component == null)
            return;
        if (SceneComponents.Contains(Component))
        {
            SceneComponents.Remove(Component);
        }
    }
    internal void RemoveComponent(FunctionalComponent Component)
    {
        if (Component == null)
            return;
        if (FunctionalComponents.Contains(Component))
        {
            FunctionalComponents.Remove(Component);
        }
    }

    protected List<FunctionalComponent> FunctionalComponents { get; private set; }
    protected List<SceneComponent> SceneComponents { get; private set; }
    public Vector3 WorldLocation
    { 
        get => RootComponent == null ? Vector3.Zero : RootComponent.WorldLocation; 
        set 
        { 
            if (RootComponent != null) 
                RootComponent.WorldLocation = value;
        } 
    }

    public Vector3 WorldScale
    {
        get => RootComponent == null ? Vector3.Zero : RootComponent.WorldScale;
        set
        {
            if (RootComponent != null)
                RootComponent.WorldScale = value;
        }
    }

    public Quaternion WorldRotation
    {
        get => RootComponent == null ? Quaternion.Zero : RootComponent.WorldRotation;
        set
        {
            if (RootComponent != null)
                RootComponent.WorldRotation = value;
        }
    }


    public void BeginPlay()
    {
        OnBeginPlay();
        FunctionalComponents.ForEach( component => component.BeginPlay());

    }

    public void Destory()
    {

        FunctionalComponents.ForEach(component => component.Destory());
        OnDestory();
    }

    public virtual void OnDestory()
    {

    }

    protected virtual void OnBeginPlay()
    {

    }
}
