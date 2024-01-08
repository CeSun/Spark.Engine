﻿using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Manager;
using PhyWorld = Jitter2.World;
using Spark.Engine.Physics;
using Spark.Engine.GUI;
using Spark.Engine.Attributes;
using Spark.Engine.Assets;
using Spark.Util;
using IniParser.Model.Formatting;
using System;

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
        RenderObjectOctree = new Octree();
        ImGuiWarp = new ImGuiSystem(this);
        PhysicsWorld = new();
    }


    public Octree RenderObjectOctree { get; private set; }

    public UpdateManager UpdateManager { private set; get; }

 
    public void Destory() 
    {
        EndPlay();
    }
    public void Update(double DeltaTime)
    {
        ActorUpdate(DeltaTime);
        UpdateManager.Update(DeltaTime);
    }


    public void Render(double DeltaTime)
    {
        foreach (var camera in CameraComponents)
        {
            camera.RenderScene(DeltaTime);
        }
        ImGuiWarp.Render(DeltaTime);
    }

    public void Serialize(BinaryWriter Writer, Engine engine)
    {
        Writer.WriteInt32(MagicCode.Asset);
        Writer.WriteInt32(MagicCode.Level);
        Writer.WriteString2(Name);
        var actors = new List<Actor>();
        foreach(var actor in Actors)
        {
            if (_DelActors.Contains(actor))
                continue;
            if (actor.IsEditorActor == true)
                continue;
            actors.Add(actor);
        }
        foreach (var actor in _AddActors)
        {
            if (_DelActors.Contains(actor))
                continue;
            actors.Add(actor);
        }

        Writer.WriteInt32(actors.Count);
        actors.ForEach(actor => actor.Serialize(Writer, engine));
        
    }

    public void Deserialize(BinaryReader Reader, Engine engine)
    {
        if (Reader.ReadInt32() != MagicCode.Asset)
            throw new Exception("");
        if (Reader.ReadInt32() != MagicCode.Level)
            throw new Exception("");
        Name = Reader.ReadString2();
        var actorNum = Reader.ReadInt32();
        for (int i = 0; i < actorNum; i++)
        {
            var typename = Reader.ReadString2();
            var type = AssemblyHelper.GetType(typename);
            var actor = (Actor)Activator.CreateInstance(type, new object[] { this, "" });
            actor.Deserialize(Reader, Engine);
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
        PhysicsUpdate(DeltaTime);
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
    public void EndPlay()
    {
        Engine.OnEndPlay?.Invoke(this);
        ImGuiWarp.Fini();
    }
    public void BeginPlay()
    {
        Engine.OnBeginPlay?.Invoke(this);
        ImGuiWarp.Init();
    }


    public void CreateLevel()
    {
        var (mesh, skeletal, anims) = SkeletalMesh.ImportFromGLB("/StaticMesh/Soldier.glb");
        mesh.Path = "Soldier.Asset";
        skeletal.Path = "Soldier.Skelton.Asset";
        anims[0].Path = "idle.Asset";

        var Camera = new CameraActor(this);
        Camera.FarPlaneDistance = 1000;
        Camera.NearPlaneDistance = 1;
        using (var sw = new StreamWriter(skeletal.Path))
        {
            var bw = new BinaryWriter(sw.BaseStream);
            skeletal.Serialize(bw, Engine);
        }
        int i = 0;
        foreach (var element in mesh.Elements)
        {

            foreach (var texture in element.Material.Textures)
            {
                if (texture == null)
                    continue;
                texture.Path = "Texture." + (i++) + ".asset";
                using (var sw = new StreamWriter(texture.Path))
                {
                    texture.Serialize(new BinaryWriter(sw.BaseStream), Engine);
                }
            }
            element.Material.Path = "Material." + (i++) + ".asset";
            using (var sw = new StreamWriter(element.Material.Path))
            {
                element.Material.Serialize(new BinaryWriter(sw.BaseStream), Engine);
            }
        }
        using (var sw = new StreamWriter(mesh.Path))
        {
            var bw = new BinaryWriter(sw.BaseStream);
            mesh.Serialize(bw, Engine);
        }

        using (var sw = new StreamWriter(anims[0].Path))
        {
            var bw = new BinaryWriter(sw.BaseStream);
            anims[0].Serialize(bw, Engine);
        }

        var sma = new SkeletalMeshActor(this, "test");
        sma.SkeletalMesh = mesh;
        sma.AnimSequence = anims[0];
        sma.WorldLocation = Camera.ForwardVector * 100;
        sma.WorldRotation = Quaternion.CreateFromYawPitchRoll(90f.DegreeToRadians(), 0, 0);
        using (var sw = new StreamWriter("testactor.asset"))
        {
            var bw = new BinaryWriter(sw.BaseStream);
            sma.Serialize(bw, Engine);
        }

        using (var sr = new StreamReader("testactor.asset"))
        {
            var br = new BinaryReader(sr.BaseStream);
            var type = AssemblyHelper.GetType(br.ReadString2());
            var actor = (Actor)Activator.CreateInstance(type, [this, ""]);
            actor.Deserialize(br, Engine);
        }




    }
    public ImGuiSystem ImGuiWarp { get; private set; }

    private void PhysicsUpdate(double DeltaTime)
    {
        while (DeltaTime > 0.03)
        {
            PhysicsWorld.Step(0.03f);
            DeltaTime -= 0.03f;
        }
        if (DeltaTime > 0)
        {
            PhysicsWorld.Step((float)DeltaTime);
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

            }, new Matrix4x4
            {
                M11 = PhysicsWorld.RigidBodies[i].Orientation.M11,
                M12 = PhysicsWorld.RigidBodies[i].Orientation.M12,
                M13 = PhysicsWorld.RigidBodies[i].Orientation.M13,
                M14 = 0,

                M21 = PhysicsWorld.RigidBodies[i].Orientation.M21,
                M22 = PhysicsWorld.RigidBodies[i].Orientation.M22,
                M23 = PhysicsWorld.RigidBodies[i].Orientation.M23,
                M24 = 0,


                M31 = PhysicsWorld.RigidBodies[i].Orientation.M31,
                M32 = PhysicsWorld.RigidBodies[i].Orientation.M32,
                M33 = PhysicsWorld.RigidBodies[i].Orientation.M33,
                M34 = 0,

                M41 = 0,
                M42 = 0,
                M43 = 0,
                M44 = 1,

            });

        }
    }
}