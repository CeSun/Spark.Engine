using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Manager;
using PhyWorld = Jitter2.World;
using Spark.Engine.Physics;
using Spark.Engine.Assets;
using Spark.Util;

namespace Spark.Engine;

public partial class Level : ISerializable
{
    public World CurrentWorld { private set; get; }

    public PhyWorld PhysicsWorld;
    public Engine Engine => CurrentWorld.Engine;

    public PlayerController? LocalPlayerController;
    public string Name = string.Empty;
    public Level(World world)
    {
        CurrentWorld = world;
        UpdateManager = new UpdateManager();
        RenderObjectOctree = new Octree(null);
        PhysicsWorld = new PhyWorld();
    }


    public Octree RenderObjectOctree { get; private set; }

    public UpdateManager UpdateManager { private set; get; }

 
    public void Destory() 
    {
        EndPlay();
    }
    public void Update(double deltaTime)
    {
        ActorUpdate(deltaTime);
        UpdateManager.Update(deltaTime);
    }


    public void Render(double deltaTime)
    {
        foreach (var camera in CameraComponents)
        {
            camera.RenderScene(deltaTime);
        }
    }

    public void Serialize(BinaryWriter writer, Engine engine)
    {
        writer.WriteInt32(MagicCode.Asset);
        writer.WriteInt32(MagicCode.Level);
        writer.WriteString2(Name);
        var actors = new List<Actor>();
        foreach(var actor in Actors)
        {
            if (_delActors.Contains(actor))
                continue;
            if (actor.IsEditorActor)
                continue;
            actors.Add(actor);
        }
        foreach (var actor in _addActors)
        {
            if (_delActors.Contains(actor))
                continue;
            actors.Add(actor);
        }

        writer.WriteInt32(actors.Count);
        actors.ForEach(actor => actor.Serialize(writer, engine));
        
    }

    public void Deserialize(BinaryReader reader, Engine engine)
    {
        if (reader.ReadInt32() != MagicCode.Asset)
            throw new Exception("");
        if (reader.ReadInt32() != MagicCode.Level)
            throw new Exception("");
        Name = reader.ReadString2();
        var actorNum = reader.ReadInt32();
        for (var i = 0; i < actorNum; i++)
        {
            var typename = reader.ReadString2();
            var type = AssemblyHelper.GetType(typename);
            if (type == null) continue;
            var actor = (Actor)Activator.CreateInstance(type, [ this, "" ])!;
            actor.Deserialize(reader, Engine);
        }
    }
}



public partial class Level
{
    private readonly List<Actor> _actors = [];
    private readonly List<Actor> _delActors = [];
    private readonly List<Actor> _addActors = [];
    public IReadOnlyList<Actor> Actors => _actors;
    public void RegisterActor(Actor actor)
    {
        if (_actors.Contains(actor))
            return;
        if (_addActors.Contains(actor))
            return;
        if (_delActors.Contains(actor))
            return;
        _addActors.Add(actor);
    }

    public void UnregistActor(Actor actor)
    {
        if (!_actors.Contains(actor) && !_addActors.Contains(actor))
            return;
        if (_delActors.Contains(actor))
            return;
        _delActors.Add(actor);

    }

    public void ActorUpdate(double deltaTime)
    {
        PhysicsUpdate(deltaTime);
        _actors.AddRange(_addActors);
        _addActors.Clear();
        _delActors.ForEach(actor => _actors.Remove(actor));
        _delActors.Clear();
    }

}

public partial class Level
{
    private readonly HashSet<PrimitiveComponent> _primitiveComponents = [];
    private readonly HashSet<CameraComponent> _cameraComponents = [];
    private readonly HashSet<DirectionLightComponent> _directionLightComponents = [];
    private readonly HashSet<PointLightComponent> _pointLightComponents = [];
    private readonly HashSet<SpotLightComponent> _spotLightComponents = [];
    private readonly HashSet<InstancedStaticMeshComponent> _ismComponents = [];
    private readonly HashSet<DecalComponent> _decalComponents = [];
    private readonly HashSet<StaticMeshComponent> _staticMeshComponents = [];
    private readonly HashSet<SkeletalMeshComponent> _skeletalMeshComponents = [];

    public IReadOnlySet<CameraComponent> CameraComponents => _cameraComponents;
    public IReadOnlySet<PrimitiveComponent> PrimitiveComponents => _primitiveComponents;
    public IReadOnlySet<DirectionLightComponent> DirectionLightComponents => _directionLightComponents;
    public IReadOnlySet<PointLightComponent> PointLightComponents => _pointLightComponents;
    public IReadOnlySet<SpotLightComponent> SpotLightComponents => _spotLightComponents;
    public IReadOnlySet<InstancedStaticMeshComponent> IsmComponents => _ismComponents;
    public IReadOnlySet<DecalComponent> DecalComponents => _decalComponents;
    public IReadOnlySet<StaticMeshComponent> StaticMeshComponents => _staticMeshComponents;
    public IReadOnlySet<SkeletalMeshComponent> SkeletalMeshComponents => _skeletalMeshComponents;

    public SkyboxComponent?  CurrentSkybox { get; private set; }
    public void RegistComponent(PrimitiveComponent component)
    {
        if (PrimitiveComponents.Contains(component))
        {
            return;
        }
        if (component is SubInstancedStaticMeshComponent == false)
            _primitiveComponents.Add(component);
        switch (component)
        {
            case CameraComponent cameraComponent:
            {
                if (_cameraComponents.Add(cameraComponent))
                {
                    _cameraComponents.Order();
                }

                break;
            }
            case DirectionLightComponent directionLightComponent:
            {
                _directionLightComponents.Add(directionLightComponent);
                break;
            }
            case PointLightComponent pointLightComponent:
            {
                _pointLightComponents.Add(pointLightComponent);
                break;
            }
            case SpotLightComponent spotLightComponent:
            {
                _spotLightComponents.Add(spotLightComponent);
                break;
            }
            case InstancedStaticMeshComponent instancedStaticMeshComponent:
            {
                _ismComponents.Add(instancedStaticMeshComponent);

                break;
            }
            case DecalComponent decalComponent:
            {
                _decalComponents.Add(decalComponent);

                break;
            }
            case SkeletalMeshComponent skeletalMeshComponent:
            {
                _skeletalMeshComponents.Add(skeletalMeshComponent);

                break;
            }
            case StaticMeshComponent staticMeshComponent:
            {
                _staticMeshComponents.Add(staticMeshComponent);
                break;
            }
            case SkyboxComponent when CurrentSkybox == null:
            {
                foreach (var tmpComponent in _primitiveComponents)
                {
                    if (tmpComponent is SkyboxComponent skyboxComponent)
                    {
                        CurrentSkybox = skyboxComponent;
                    }
                }

                break;
            }
        }
    }

    public void UnregisterComponent(PrimitiveComponent component)
    {
        if (!PrimitiveComponents.Contains(component))
        {
            return;
        }
        if (component is SubInstancedStaticMeshComponent == false)
            _primitiveComponents.Remove(component);
        switch (component)
        {
            case CameraComponent cameraComponent:
            {
                if (_cameraComponents.Contains(cameraComponent))
                    _cameraComponents.Remove(cameraComponent);
                break;
            }
            case DirectionLightComponent directionLightComponent:
            {
                if (_directionLightComponents.Contains(directionLightComponent))
                {
                    _directionLightComponents.Remove(directionLightComponent);
                }

                break;
            }
            case PointLightComponent pointLightComponent:
            {
                if (_pointLightComponents.Contains(pointLightComponent))
                {
                    _pointLightComponents.Remove(pointLightComponent);
                }

                break;
            }
            case SpotLightComponent spotLightComponent:
            {
                if (_spotLightComponents.Contains(spotLightComponent))
                {
                    _spotLightComponents.Remove(spotLightComponent);
                }

                break;
            }
            case InstancedStaticMeshComponent instancedStaticMeshComponent:
            {
                if (_ismComponents.Contains(instancedStaticMeshComponent))
                {
                    _ismComponents.Remove(instancedStaticMeshComponent);
                }

                break;
            }
            case DecalComponent decalComponent:
            {
                if (_decalComponents.Contains(decalComponent))
                {
                    _decalComponents.Remove(decalComponent);
                }

                break;
            }
            case SkeletalMeshComponent skeletalMeshComponent:
            {
                if (_skeletalMeshComponents.Contains(skeletalMeshComponent))
                {
                    _skeletalMeshComponents.Remove(skeletalMeshComponent);
                }

                break;
            }
            case StaticMeshComponent staticMeshComponent:
            {
                if (_staticMeshComponents.Contains(staticMeshComponent))
                {
                    _staticMeshComponents.Remove(staticMeshComponent);
                }

                break;
            }
            case SkyboxComponent when CurrentSkybox == component:
            {
                CurrentSkybox = null;
                foreach (var compon in _primitiveComponents)
                {
                    if (compon is SkyboxComponent skyboxComponent)
                    {
                        CurrentSkybox = skyboxComponent;
                    }
                }

                break;
            }
        }
    }
}


public partial class Level
{
    public void EndPlay()
    {
        Engine.OnEndPlay?.Invoke(this);
    }
    public void BeginPlay()
    {
        Engine.OnBeginPlay?.Invoke(this);
    }


    public void CreateLevel()
    {
       

    }

    private void PhysicsUpdate(double deltaTime)
    {
        while (deltaTime > 0.03)
        {
            PhysicsWorld.Step(0.03f);
            deltaTime -= 0.03f;
        }
        if (deltaTime > 0)
        {
            PhysicsWorld.Step((float)deltaTime);
        }

        for (int i = 0; i < PhysicsWorld.RigidBodies.Active; i++)
        {
            if (PhysicsWorld.RigidBodies[i].Tag == null)
                continue;
            if (PhysicsWorld.RigidBodies[i].Tag is PrimitiveComponent primitiveComponent == false)
                continue;

            primitiveComponent.PhysicsUpdateTransform(new Vector3
            {
                X = PhysicsWorld.RigidBodies[i].Position.X,
                Y = PhysicsWorld.RigidBodies[i].Position.Y,
                Z = PhysicsWorld.RigidBodies[i].Position.Z,

            }, Matrix4x4.CreateFromQuaternion(new Quaternion(PhysicsWorld.RigidBodies[i].Orientation.X,
                PhysicsWorld.RigidBodies[i].Orientation.Y, PhysicsWorld.RigidBodies[i].Orientation.Z, PhysicsWorld.RigidBodies[i].Orientation.W)));

        }
    }
}