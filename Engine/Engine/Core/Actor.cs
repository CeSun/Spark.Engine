using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class Actor
{
    public Actor ()
    {
        FunctionalComponents = new List<FunctionalComponent>();
    }

    public SceneComponent? RootComponent { get; set; }
    protected List<FunctionalComponent> FunctionalComponents { get; private set; }

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
