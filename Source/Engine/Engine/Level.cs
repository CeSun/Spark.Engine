using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Components;
using Spark.Util;
using Silk.NET.Input;

using Texture = Spark.Engine.Assets.Texture;
using Spark.Engine.Manager;
using Jitter2.Collision;
using PhyWorld = Jitter2.World;
using Spark.Engine.Physics;

namespace Spark.Engine;

public partial class Level
{
    public PhyWorld PhyWorld { get; private set; }
    public World CurrentWorld { private set; get; }

    public Engine Engine => CurrentWorld.Engine;
    public Level(World world)
    {
        CurrentWorld = world;
        UpdateManager = new UpdateManager();
        PhyWorld = new PhyWorld();
        RenderObjectOctree = new Octree();
        LightOctree = new Octree();
    }


    public Octree RenderObjectOctree { get; private set; }

    public Octree LightOctree { get; private set; }
    public UpdateManager UpdateManager { private set; get; }

 
    public void Destory() 
    {
        EndPlay();
    }
    public void Update(double DeltaTime)
    {
        double tmpTime = DeltaTime;
        while(tmpTime > 0)
        {
            if (tmpTime > 0.1F)
            {
                PhyWorld.Step(0.1F, false);
                tmpTime -= 0.1F;
            }
            else
            {
                PhyWorld.Step((float)tmpTime, false);
                tmpTime = 0;
            }


        }
        ActorUpdate(DeltaTime);
        UpdateManager.Update(DeltaTime);
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
        if (_DelActors.Contains(actor))
            return;
        _AddActors.Add(actor);
    }

    public void UnregistActor(Actor actor)
    {
        if (!_Actors.Contains(actor) && !_AddActors.Contains(actor))
            return;
        if (_DelActors.Contains(actor))
            return;
        _DelActors.Add(actor);

    }

    public void ActorUpdate(double DeltaTime)
    {
        //foreach (Actor actor in _Actors)
        //{
        //    actor.Update(DeltaTime);
        //}
        _Actors.AddRange(_AddActors);
        _AddActors.Clear();
        _DelActors.ForEach(actor => _Actors.Remove(actor));
        _DelActors.Clear();
    }

}

public partial class Level
{
    private HashSet<PrimitiveComponent> _PrimitiveComponents = new HashSet<PrimitiveComponent>();
    private HashSet<CameraComponent> _CameraComponents = new HashSet<CameraComponent>();
    private HashSet<DirectionLightComponent> _DirectionLightComponents = new HashSet<DirectionLightComponent>();
    private HashSet<PointLightComponent> _PointLightComponents = new HashSet<PointLightComponent>();
    private HashSet<SpotLightComponent> _SpotLightComponents = new HashSet<SpotLightComponent>();
    private HashSet<InstancedStaticMeshComponent> _ISMComponents = new HashSet<InstancedStaticMeshComponent>();
    private HashSet<DecalComponent> _DecalComponents = new HashSet<DecalComponent>();
    private HashSet<StaticMeshComponent> _StaticMeshComponents = new HashSet<StaticMeshComponent>();
    private HashSet<SkeletalMeshComponent> _SkeletalMeshComponents = new HashSet<SkeletalMeshComponent>();

    public IReadOnlySet<CameraComponent> CameraComponents => _CameraComponents;
    public IReadOnlySet<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;
    public IReadOnlySet<DirectionLightComponent> DirectionLightComponents => _DirectionLightComponents;
    public IReadOnlySet<PointLightComponent> PointLightComponents => _PointLightComponents;
    public IReadOnlySet<SpotLightComponent> SpotLightComponents => _SpotLightComponents;
    public IReadOnlySet<InstancedStaticMeshComponent> ISMComponents => _ISMComponents;
    public IReadOnlySet<DecalComponent> DecalComponents => _DecalComponents;
    public IReadOnlySet<StaticMeshComponent> StaticMeshComponents => _StaticMeshComponents;
    public IReadOnlySet<SkeletalMeshComponent> SkeletalMeshComponents => _SkeletalMeshComponents;

    public SkyboxComponent?  CurrentSkybox { get; private set; }
    public void RegistComponent(PrimitiveComponent component)
    {
        if (PrimitiveComponents.Contains(component))
        {
            return;
        }
        if (component is SubInstancedStaticMeshComponent == false)
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
        else if (component is PointLightComponent pointLightComponent)
        {
            if (!_PointLightComponents.Contains(pointLightComponent))
            {
                _PointLightComponents.Add(pointLightComponent);
            }
        }
        else if (component is SpotLightComponent spotLightComponent)
        {
            if (!_SpotLightComponents.Contains(spotLightComponent))
            {
                _SpotLightComponents.Add(spotLightComponent);
            }
        }
        else if (component is InstancedStaticMeshComponent InstancedStaticMeshComponent)
        {
            if (!_ISMComponents.Contains(InstancedStaticMeshComponent))
            {
                _ISMComponents.Add(InstancedStaticMeshComponent);  
            }
        }
        else if (component is DecalComponent DecalComponent)
        {
            if (!_DecalComponents.Contains(DecalComponent))
            {
                _DecalComponents.Add(DecalComponent);
            }
        }
        else if (component is SkeletalMeshComponent SkeletalMeshComponent)
        {
            if (!_SkeletalMeshComponents.Contains(SkeletalMeshComponent))
            {
                _SkeletalMeshComponents.Add(SkeletalMeshComponent);
            }
        }
        else if (component is StaticMeshComponent StaticMeshComponent)
        {
            if (!_StaticMeshComponents.Contains(StaticMeshComponent))
            {
                _StaticMeshComponents.Add(StaticMeshComponent);
            }
        }

        if (component is SkyboxComponent && CurrentSkybox == null)
        {
            foreach (var compon in _PrimitiveComponents)
            {
                if (compon is SkyboxComponent skyboxComponent)
                {
                    CurrentSkybox = skyboxComponent;
                }
            }
        }
    }

    public void UnregistComponent(PrimitiveComponent component)
    {
        if (!PrimitiveComponents.Contains(component))
        {
            return;
        }
        if (component is SubInstancedStaticMeshComponent == false)
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
        else if (component is PointLightComponent pointLightComponent)
        {
            if (_PointLightComponents.Contains(pointLightComponent))
            {
                _PointLightComponents.Remove(pointLightComponent);
            }
        }
        else if (component is SpotLightComponent spotLightComponent)
        {
            if (_SpotLightComponents.Contains(spotLightComponent))
            {
                _SpotLightComponents.Remove(spotLightComponent);
            }
        }
        else if (component is InstancedStaticMeshComponent InstancedStaticMeshComponent) 
        {
            if (_ISMComponents.Contains(InstancedStaticMeshComponent))
            {
                _ISMComponents.Remove(InstancedStaticMeshComponent);
            }
        }
        else if (component is DecalComponent DecalComponent)
        {
            if (_DecalComponents.Contains(DecalComponent))
            {
                _DecalComponents.Remove(DecalComponent);
            }
        }
        else if (component is SkeletalMeshComponent SkeletalMeshComponent)
        {
            if (_SkeletalMeshComponents.Contains(SkeletalMeshComponent))
            {
                _SkeletalMeshComponents.Remove(SkeletalMeshComponent);
            }
        }
        else if (component is StaticMeshComponent StaticMeshComponent)
        {
            if (_StaticMeshComponents.Contains(StaticMeshComponent))
            {
                _StaticMeshComponents.Remove(StaticMeshComponent);
            }
        }
        if (component is SkyboxComponent && CurrentSkybox == component)
        {
            CurrentSkybox = null;
            foreach (var compon in _PrimitiveComponents)
            {
                if (compon is SkyboxComponent skyboxComponent)
                {
                    CurrentSkybox = skyboxComponent;
                }
            }
        }
    }
}


public partial class Level
{
    Actor? RobotActor;
    CameraComponent? CameraComponent;

    Vector2 MoveData = default;
    Vector2 LastPosition;

    private MouseButton ViewButton = MouseButton.Right;
    public void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (Engine.MainMouse.IsButtonPressed(ViewButton))
        {
            if (CameraComponent == null)
                return;
            var moveable = position - LastPosition;
            LastPosition = position;

            MoveData += (moveable * 0.1f);
            var rotation = Quaternion.CreateFromYawPitchRoll(-1 * MoveData.X.DegreeToRadians(), -1 * MoveData.Y.DegreeToRadians(), 0);
            CameraComponent.WorldRotation = rotation;


            rotation = Quaternion.CreateFromYawPitchRoll(-1 * MoveData.X.DegreeToRadians(), 0, 0);

            // RobotActor.WorldRotation = rotation;
        }
    }

    public void OnMouseKeyDown(IMouse mouse, Silk.NET.Input.MouseButton key)
    {
        if (key == ViewButton)
        {
            LastPosition = mouse.Position;
        }
    }

    Actor? CameraActor;

    public void EndPlay()
    {
        Engine.OnEndPlay?.Invoke(this);
    }
    public void BeginPlay()
    {
        Engine.OnBeginPlay?.Invoke(this);
       
    }
   

}