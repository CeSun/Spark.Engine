﻿using Spark.Core.Actors;
namespace Spark.Core.Components;

public class ActorComponent
{
    public Engine Engine =>CurrentWorld.Engine;
    public ActorComponent(Actor actor) 
    {
        Owner = actor;
    }

    /// <summary>
    /// Actor所在世界
    /// </summary>
    public World CurrentWorld { get => Owner.World; }

    /// <summary>
    /// 组件拥有者
    /// </summary>
    public Actor Owner;

    public void BeginPlay()
    {
        OnBeginPlay();
    }

    public virtual void OnBeginPlay()
    {

    }
    private bool IsBegined = false;
    public void Update(double DeltaTime)
    {
        if (IsBegined == false)
        {
            IsBegined = true;
            BeginPlay();
        }
        OnUpdate(DeltaTime);
    }

    protected virtual void OnUpdate(double DeltaTime) 
    { 

    }

    public void Destory() 
    {
        OnEndPlay();
    }

    protected virtual void OnEndPlay()
    {

    }
}
