using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using Spark.Engine.Core.Actors;
using Spark.Engine.Core.Assets;
using Spark.Engine.Core.Components;
using Spark.Util;
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
        
        StaticMesh mesh = new StaticMesh("/StaticMesh/untitled.glb");
        Actor CameraActor = new Actor(this);
        CameraComponent cameraComponent = new CameraComponent(CameraActor);
        CameraActor.RootComponent = cameraComponent;
        Actor StaticMeshActor = new Actor(this);
        StaticMeshComponent staticMeshComponent = new StaticMeshComponent(StaticMeshActor);
        StaticMeshActor.RootComponent = staticMeshComponent;
        staticMeshComponent.WorldScale = new System.Numerics.Vector3(10, 10, 10);
        staticMeshComponent.StaticMesh = mesh;
        staticMeshComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(180f.DegreeToRadians(), 0, 0);
        staticMeshComponent.WorldLocation = cameraComponent.WorldLocation + (cameraComponent.ForwardVector * 20) - staticMeshComponent.UpVector * 10 ;
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
        foreach (var camera in CameraComponents)
        {
            camera.RenderScene(DeltaTime);
        }
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
    private List<DirectionLightComponent> _DirectionLightComponents = new List<DirectionLightComponent>();
    public IReadOnlyList<CameraComponent> CameraComponents => _CameraComponents;
    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;
    public IReadOnlyList<DirectionLightComponent> DirectionLightComponents => _DirectionLightComponents;
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
        else if (component is DirectionLightComponent directionLightComponent)
        {
            if (!_DirectionLightComponents.Contains(directionLightComponent))
            {
                _DirectionLightComponents.Add(directionLightComponent);
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
        else if (component is DirectionLightComponent directionLightComponent)
        {
            if (_DirectionLightComponents.Contains(directionLightComponent))
            {
                _DirectionLightComponents.Remove(directionLightComponent);
            }

        }
    }
}