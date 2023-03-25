using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using Spark.Engine.Core.Actors;
using Spark.Engine.Core.Components;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core;

public partial class Level
{

    public World CurrentWorld { private set; get; }
    public Level(World world)
    {
        CurrentWorld = world;
    }

    public void BeginPlay()
    {
        Actor actor = new Actor(this);
        PrimitiveComponent comp = new PrimitiveComponent(actor);
        actor.RootComponent = comp;
        PrimitiveComponent comp2 = new PrimitiveComponent(actor);
        comp2 = new PrimitiveComponent(actor);
        comp2.ParentComponent = comp;
        comp.WorldLocation = new (100, 0, 0);
        comp2.RelativeLocation = new(0, 200, 0);
        Console.WriteLine(comp.WorldLocation);
        Console.WriteLine(comp2.WorldLocation);
    }

    public void Destory() 
    { 

    }

    public void Update(double DeltaTime)
    {
        ActorUpdate(DeltaTime);
    }

    public void Render(double DeltaTime)
    {
    }
}



public partial class Level
{
    private List<Actor> _Actors = new List<Actor>();
    private List<Actor> _DelActors = new List<Actor>();
    private List<Actor> _AddActors = new List<Actor>();
    public IReadOnlyList<Actor> Actors => _Actors;

    public void RegistActor(Actor actor)
    {
        if (_Actors.Contains(actor))
            return;
        if (_AddActors.Contains(actor))
            return;
        if (!_DelActors.Contains(actor))
            return;
        _AddActors.Add(actor);
    }

    public void UnregistActor(Actor actor)
    {
        if (!_Actors.Contains(actor))
            return;
        if (!_AddActors.Contains(actor))
            return;
        if (_DelActors.Contains(actor))
            return;
        _DelActors.Add(actor);

    }

    public void ActorUpdate(double DeltaTime)
    {
        foreach (Actor actor in _Actors)
        {
            actor.Update(DeltaTime);
        }
        _Actors.AddRange(_AddActors);
        _AddActors.Clear();
        _DelActors.ForEach(actor => _Actors.Remove(actor));
        _DelActors.Clear();
    }

}

public partial class Level
{
    private List<PrimitiveComponent> _PrimitiveComponents = new List<PrimitiveComponent>();
    private List<CameraComponent> _CameraComponents = new List<CameraComponent>();

    public IReadOnlyList<CameraComponent> CameraComponents => _CameraComponents;
    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;

    public void RegistComponent(PrimitiveComponent component)
    {
        if (PrimitiveComponents.Contains(component))
        {
            return;
        }
        _PrimitiveComponents.Add(component);
        if (component is CameraComponent cameraComponent)
        {
            if (!_CameraComponents.Contains(cameraComponent))
            {
                _CameraComponents.Add(cameraComponent);
                _CameraComponents.Order();
            }
        }
    }

    public void UnregistComponent(PrimitiveComponent component)
    {
        if (!PrimitiveComponents.Contains(component))
        {
            return;
        }
        _PrimitiveComponents.Remove(component);
        if (component is CameraComponent cameraComponent)
        {
            if (_CameraComponents.Contains(cameraComponent))
                _CameraComponents.Remove(cameraComponent);
        }
    }
}