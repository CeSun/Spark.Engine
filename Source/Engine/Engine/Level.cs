﻿using System;
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

using static Spark.Engine.StaticEngine;
using Texture = Spark.Engine.Assets.Texture;
using Spark.Engine.Manager;
using Jitter.Collision;
using PhyWorld = Jitter.World;
using System.ComponentModel;
using Spark.Engine.GUI;
using ImGuiNET;

namespace Spark.Engine;

public partial class Level
{
    public PhyWorld PhyWorld { get; private set; }
    CollisionSystem CollisionSystem;
    public World CurrentWorld { private set; get; }
    ImGuiWarp imgui;
    public Level(World world)
    {
        CurrentWorld = world;
        UpdateManager = new UpdateManager();
        CollisionSystem = new CollisionSystemSAP();
        PhyWorld = new PhyWorld(CollisionSystem);
        imgui = new ImGuiWarp(this);

    }
    public UpdateManager UpdateManager { private set; get; }

    Actor? RobotActor;
    CameraComponent? CameraComponent;

    Vector2 MoveData = default;
    Vector2 LastPosition;

    public void OnMouseMove(IMouse mouse,  Vector2 position)
    {
        if (MainMouse.IsButtonPressed(MouseButton.Left))
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

    public void OnMouseKeyDown(IMouse mouse, MouseButton key)
    {
        if (key == MouseButton.Left)
        {
            LastPosition = mouse.Position;
        }
    }
    bool NeedPrintFPS = false;
    public void BeginPlay()
    {
        imgui.Init();
        // InitGrass();
        // Test();
        MainMouse.MouseMove += OnMouseMove;
        MainMouse.MouseDown += OnMouseKeyDown;

        MainKeyBoard.KeyDown += (_, key, _) =>
        {

            if (key == Key.F)
            {
                NeedPrintFPS = true;
            }
            if (key == Key.M)
            {
                foreach (var actor in temp)
                {
                    if (actor.RootComponent is HierarchicalInstancedStaticMeshComponent hism)
                    {
                        var x = Random.Shared.Next(-100, 100);
                        var y = Random.Shared.Next(-100, 100);
                        hism.AddComponent(new SubInstancedStaticMeshComponent(actor)
                        {
                            WorldLocation = new Vector3(x, 40, y)
                        });
                        hism.ReBuild();
                    }
                }
            }
        };

        var (skm, sk, anim) = SkeletalMesh.ImportFromGLB("/StaticMesh/untitled.glb");
        var SkeletalActor = new Actor(this, "Skeletal Mesh");
        var Comp = new SkeletalMeshComponent(SkeletalActor);
        Comp.SkeletalMesh = skm;
        Comp.AnimSequence = anim[2];
        Comp.WorldScale = new Vector3(5, 5, 5);
        Comp.WorldLocation = new Vector3(0, 1, 0);
        Comp.WorldRotation = Quaternion.CreateFromYawPitchRoll(180f.DegreeToRadians(), 0, 0);
        // 定义一个actor和并挂载静态网格体组件
        var RobotActor = new Actor(this, "Robot Actor");
        var RobotMeshComp = new StaticMeshComponent(RobotActor);
        RobotMeshComp.StaticMesh = new StaticMesh("/StaticMesh/untitled.glb");
        RobotActor.RootComponent = RobotMeshComp;
        RobotActor.WorldScale = new Vector3(5, 5, 5);
        RobotMeshComp.IsStatic = true;
        RobotActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(0F.DegreeToRadians(), 90F.DegreeToRadians(), 0F.DegreeToRadians());
        RobotActor.WorldLocation = new Vector3(0, 1.8f, 10);
        this.RobotActor = RobotActor;
        


        // 相机actor
        var CameraActor = new Actor(this, "Camera Actor");
        CameraComponent = new CameraComponent(CameraActor);
        CameraActor.RootComponent = CameraComponent;
        CameraComponent.NearPlaneDistance = 1;
        CameraComponent.FarPlaneDistance = 1000f;
        CameraComponent.FieldOfView = 75;
        CameraComponent.ProjectionType = ProjectionType.Perspective;
        CameraComponent.WorldLocation += (new Vector3(0, 20, 0) - CameraComponent.ForwardVector * 10);
        CameraComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(0F.DegreeToRadians(), -10f.DegreeToRadians(), 0);

        // 加载个cube作为地板
        var CubeActor = new Actor(this, "Plane Actor");
        var CubeMeshComp = new StaticMeshComponent(CubeActor);
        CubeActor.RootComponent = CubeMeshComp;
        CubeMeshComp.StaticMesh = new StaticMesh("/StaticMesh/cube2.glb");
        CubeMeshComp.IsStatic = true;
        CubeMeshComp.WorldScale = new Vector3(100F, 1F, 100F);
        CubeMeshComp.WorldLocation = new Vector3(0, 0, 0);

        var DecalActor = new Actor(this, "DecalActor");
        var DecalComponent = new DecalComponent(DecalActor);
        DecalActor.RootComponent = DecalComponent;
        DecalActor.WorldScale = new Vector3(1, 1, 1);
        DecalActor.WorldLocation = new Vector3(0, 0.9F, 0);
        DecalComponent.Material = new Assets.Material()
        {
            Diffuse = new Texture("/Texture/bear.png")
        };
        DecalActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(180F.DegreeToRadians(), 90F.DegreeToRadians(), 90F.DegreeToRadians());

        /*
        // 视差贴图

        var CubeActor2 = new Actor(this);
        var CubeMeshComp2 = new StaticMeshComponent(CubeActor2);
        CubeMeshComp2.StaticMesh = new StaticMesh("/StaticMesh/cube2.glb");
        CubeActor2.RootComponent = CubeMeshComp2;
        CubeMeshComp2.WorldScale = new Vector3(2, 2, 2);
        CubeActor2.WorldLocation += CubeMeshComp2.UpVector * 2F + CubeMeshComp2.RightVector * 2;
        var texture = new Texture("/StaticMesh/bricks2_disp.jpg");
        foreach(var material in CubeMeshComp2.StaticMesh.Materials)
        {
            // material.Parallax = texture;
        }
        */
        //CubeMeshComp2.StaticMesh.Materials

        
        // 创建定向光源
        var DirectionActor = new Actor(this, "Direction Actor");
        var DirectionComp = new DirectionLightComponent(DirectionActor);
        DirectionActor.RootComponent = DirectionComp;
        DirectionComp.Color = Color.White;
        DirectionComp.WorldRotation = Quaternion.CreateFromYawPitchRoll(70f.DegreeToRadians(), -45f.DegreeToRadians(), 0f);
        DirectionComp.LightStrength = 0.6f;
        DirectionComp.WorldLocation += DirectionComp.ForwardVector * -30;
      
        var PointLight = new Actor(this, "PointLight Actor");
        var PointLightComp = new PointLightComponent(PointLight);
        PointLight.RootComponent = PointLightComp;
        PointLightComp.Color = Color.YellowGreen;
        PointLightComp.LightStrength = 0.7f;
        PointLightComp.WorldLocation += PointLightComp.UpVector * 10 ;
         
        var spotLight = new Actor(this, "SpotLight Actor");
        var SpotLightComponent = new SpotLightComponent(spotLight);
        spotLight.RootComponent = SpotLightComponent;
        SpotLightComponent.Color = Color.SteelBlue;
        SpotLightComponent.LightStrength = 0.7f;
        SpotLightComponent.WorldLocation += SpotLightComponent.UpVector * 20;
        SpotLightComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, -90f.DegreeToRadians(), 0);
        SpotLightComponent.InnerAngle = 90;
        SpotLightComponent.OuterAngle = 110;
        var SkyBoxActor = new Actor(this, "SkyBox Actor");
        var skybox = new SkyboxComponent(SkyBoxActor);
        skybox.SkyboxCube = new TextureCube("/Skybox/pm");
    }



    public void InitGrass()
    {
        int grassLen = 1;
        int len = (int)Math.Sqrt(grassLen);
        for (int i = 0; i < grassLen; i++)
        {
            var GrassActor = new Actor(this, "Grass Actor");
            var GrassComponent = new StaticMeshComponent(GrassActor);
            GrassComponent.StaticMesh = new StaticMesh("/StaticMesh/flower.glb");
            GrassActor.RootComponent = GrassComponent;
            GrassComponent.WorldLocation = new Vector3((i / len - len / 2) * 5f, -3, (i % len - len /2 ) * 5f);
        }
    }


    

    List<Actor> temp = new List<Actor>();
    

    void Delete()
    {
        foreach(var actor in temp)
        {
            actor.Destory();
        }
        temp.Clear();
    }

    public void Destory() 
    {
        imgui.Fini();
    }
    float a = 0;
    public void Update(double DeltaTime)
    {
        PhyWorld.Step((float)DeltaTime, false);
        CameraMove(DeltaTime);
        RobotMove(DeltaTime);
        ActorUpdate(DeltaTime);
        UpdateManager.Update(DeltaTime);
    }

    private void CameraMove(double DeltaTime)
    {
        if (CameraComponent == null)
            return;
        Vector3 MoveDirection = Vector3.Zero;
        if (MainKeyBoard.IsKeyPressed(Key.W))
        {
            MoveDirection.Z = -1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.S))
        {
            MoveDirection.Z = 1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.A))
        {
            MoveDirection.X = -1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.D))
        {
            MoveDirection.X = 1;
        }
        if (MoveDirection.Length() != 0)
        {
            MoveDirection = Vector3.Normalize(MoveDirection);
            MoveDirection = Vector3.Transform(MoveDirection, CameraComponent.WorldRotation);
            CameraComponent.WorldLocation += MoveDirection * 10 * (float)DeltaTime;
        }
    }


    private void RobotMove(double DeltaTime)
    {
        if (RobotActor == null)
            return;
        Vector3 MoveDirection = Vector3.Zero;
        if (MainKeyBoard.IsKeyPressed(Key.W))
        {
            MoveDirection.Z = -1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.S))
        {
            MoveDirection.Z = 1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.A))
        {
            MoveDirection.X = -1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.D))
        {
            MoveDirection.X = 1;
        }
        if (MoveDirection.Length() != 0)
        {
            MoveDirection = Vector3.Normalize(MoveDirection);
            MoveDirection = Vector3.Transform(MoveDirection, RobotActor.WorldRotation);
            // RobotActor.WorldLocation += MoveDirection * 10 * (float)DeltaTime;
        }
    }

    public void Render(double DeltaTime)
    {
        if (NeedPrintFPS == true)
        {
            NeedPrintFPS = false;
            Console.Out.WriteLineAsync((1 / DeltaTime) + "FPS");
        }

        foreach (var camera in CameraComponents)
        {
            camera.RenderScene(DeltaTime);
        }
        imgui.Render(DeltaTime);
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