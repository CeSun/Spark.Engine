﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
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
using Jitter.Collision.Shapes;
using Jitter.Dynamics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Spark.Engine;

public partial class Level
{
    PhyWorld PhyWorld;
    CollisionSystem CollisionSystem;
    public World CurrentWorld { private set; get; }
    public Level(World world)
    {
        CurrentWorld = world;
        UpdateManager = new UpdateManager();
        CollisionSystem = new CollisionSystemSAP();
        PhyWorld = new PhyWorld(CollisionSystem);

    }
    public UpdateManager UpdateManager { private set; get; }

    Actor? RobotActor;
    CameraComponent? CameraComponent;

    Vector2 MoveData = default;
    Vector2 MoveData2 = default;
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

    public Actor MinCube;
    public RigidBody MinCubeRigidBody;
    public void BeginPlay()
    {
        // InitGrass();
        //Test();
        MainMouse.MouseMove += OnMouseMove;
        MainMouse.MouseDown += OnMouseKeyDown;
        /*
        // 定义一个actor和并挂载静态网格体组件
        var RobotActor = new Actor(this);
        var RobotMeshComp = new StaticMeshComponent(RobotActor);
        RobotMeshComp.StaticMesh = new StaticMesh("/StaticMesh/untitled.glb");
        RobotActor.RootComponent = RobotMeshComp;
        RobotMeshComp.WorldScale = new Vector3(5, 5, 5);
        RobotMeshComp.WorldRotation = Quaternion.CreateFromYawPitchRoll(180F.DegreeToRadians(), 0, 0);
        RobotMeshComp.WorldLocation -= RobotMeshComp.UpVector * 2;
        this.RobotActor = RobotActor;
        */
        // 相机actor
        var CameraActor = new Actor(this);
        CameraComponent = new CameraComponent(CameraActor);
        CameraActor.RootComponent = CameraComponent;
        CameraComponent.NearPlaneDistance = 10;
        CameraComponent.FarPlaneDistance =  1000f;
        CameraComponent.ProjectionType = ProjectionType.Perspective;
        CameraComponent.RelativeLocation += (new Vector3(0, 10f, 2) - CameraComponent.ForwardVector * 10);
        CameraComponent.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0F.DegreeToRadians(), -10f.DegreeToRadians(), 0);

        // 加载个cube作为地板
        var CubeActor = new Actor(this);
        var CubeMeshComp = new StaticMeshComponent(CubeActor);
        CubeMeshComp.StaticMesh = new StaticMesh("/StaticMesh/cube2.glb");
        CubeActor.RootComponent = CubeMeshComp;
        CubeMeshComp.RelativeScale = new Vector3(1000, 1, 1000);
        // CubeMeshComp.StaticMesh.Materials[0].IsReflection = 1;
        var skybox = new SkyboxComponent(CubeActor);
        skybox.SkyboxCube = new TextureCube("/Skybox/pm");

        Shape shape = new BoxShape(2000, 2, 2000);
        RigidBody body = new RigidBody(shape);
        body.Position = new Jitter.LinearMath.JVector(CubeMeshComp.WorldLocation.X, CubeMeshComp.WorldLocation.Y, CubeMeshComp.WorldLocation.Z);
        body.IsStatic = true;
        PhyWorld.AddBody(body);


        MinCube = new Actor(this);
        var CubeMeshComp2 = new StaticMeshComponent(MinCube);
        MinCube.RootComponent = CubeMeshComp2;
        CubeMeshComp2.StaticMesh = CubeMeshComp.StaticMesh;
        CubeMeshComp2.RelativeScale = new Vector3(1, 1, 1);
        CubeMeshComp2.RelativeLocation += CubeMeshComp.UpVector * 20F;




        Shape shape2 = new BoxShape(2, 2, 2);
        MinCubeRigidBody = new RigidBody(shape2);
        MinCubeRigidBody.Position = new Jitter.LinearMath.JVector(CubeMeshComp2.WorldLocation.X, CubeMeshComp2.WorldLocation.Y, CubeMeshComp2.WorldLocation.Z);
        MinCubeRigidBody.IsStatic = false;

        PhyWorld.AddBody(MinCubeRigidBody);
        /*
        // 时差贴图

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
        var DirectionActor = new Actor(this);
        var DirectionComp = new DirectionLightComponent(DirectionActor);
        DirectionActor.RootComponent = DirectionComp;
        DirectionComp.Color = Color.White;
        DirectionComp.WorldRotation = Quaternion.CreateFromYawPitchRoll(70f.DegreeToRadians(), -45f.DegreeToRadians(), 0f);
        DirectionComp.LightStrength = 0.7f;
        DirectionComp.WorldLocation += DirectionComp.ForwardVector * -30;
        
        var PointLight = new Actor(this);
        var PointLightComp = new PointLightComponent(PointLight);
        PointLight.RootComponent = PointLightComp;
        PointLightComp.Color = Color.White;
        PointLightComp.LightStrength =1f;
        PointLightComp.WorldLocation += PointLightComp.UpVector * 10 - PointLightComp.RightVector * 2;






        var spotLight = new Actor(this);
        var SpotLightComponent = new SpotLightComponent(spotLight);
        spotLight.RootComponent = SpotLightComponent;
        SpotLightComponent.Color = Color.Purple;
        SpotLightComponent.LightStrength = 1f;
        SpotLightComponent.WorldLocation += SpotLightComponent.UpVector * 10;
        SpotLightComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, -45f.DegreeToRadians(), 0);

    }


    public void InitGrass()
    {
        int grassLen = 1;
        int len = (int)Math.Sqrt(grassLen);
        for (int i = 0; i < grassLen; i++)
        {
            var GrassActor = new Actor(this);
            var GrassComponent = new StaticMeshComponent(GrassActor);
            GrassComponent.StaticMesh = new StaticMesh("/StaticMesh/flower.glb");
            GrassActor.RootComponent = GrassComponent;
            GrassComponent.WorldLocation = new Vector3((i / len - len / 2) * 5f, -3, (i % len - len /2 ) * 5f);
        }
    }


    public async void InitHISM(string model, int num, Vector2 area, float scale = 1)
    {
        int grassLen = num;
        int len = (int)Math.Sqrt(grassLen);
        var hismactor = new Actor(this);
        var hismcomponent = new HierarchicalInstancedStaticMeshComponent(hismactor);
        hismcomponent.StaticMesh = new StaticMesh(model);

        hismcomponent.WorldLocation = new Vector3(0, 0, 0);
        for (int i = 0; i < grassLen; i++)
        {
            if (i % 10 == 0)
                await Task.Yield();
            var GrassComponent = new SubHierarchicalInstancedStaticMeshComponent(hismactor);
            hismcomponent.AddComponent(GrassComponent);
            GrassComponent.ParentComponent = hismcomponent;
            var x = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var y = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var yaw = Random.Shared.Next(0, 180);
            GrassComponent.RelativeRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
            GrassComponent.RelativeLocation = new Vector3(x, -3, y);
            GrassComponent.RelativeScale = new Vector3(scale, scale, scale);
        }

        hismcomponent.RefreshTree();

    }



    async void Test()
    {
        int num = 100000;
        int len = (int)Math.Sqrt(num);
        InitHISM("/StaticMesh/flower.glb", 100000, new Vector2((-len / 2) * 15f, (len / 2) * 15f));
        InitHISM("/StaticMesh/grass.glb", 100000, new Vector2((-len / 2) * 15f, (len / 2) * 15f));
    }
    public void Destory() 
    { 
    }
    float a = 0;
    public void Update(double DeltaTime)
    {
        PhyWorld.Step((float)DeltaTime, false);


        unsafe
        {
            var rotationM = new Matrix4x4
            {
                M11 = MinCubeRigidBody.Orientation.M11,
                M12 = MinCubeRigidBody.Orientation.M12,
                M13 = MinCubeRigidBody.Orientation.M13,
                M14 = 0,
                M21 = MinCubeRigidBody.Orientation.M21,
                M22 = MinCubeRigidBody.Orientation.M22,
                M23 = MinCubeRigidBody.Orientation.M23,
                M24 = 0,
                M31 = MinCubeRigidBody.Orientation.M31,
                M32 = MinCubeRigidBody.Orientation.M32,
                M33 = MinCubeRigidBody.Orientation.M33,
                M34 = 0,
                M41 = 0,
                M42 = 0,
                M43 = 0,
                M44 = 1,
            };

            MinCube.WorldRotation = rotationM.Rotation();
            MinCube.WorldLocation = new Vector3(MinCubeRigidBody.Position.X, MinCubeRigidBody.Position.Y, MinCubeRigidBody.Position.Z);
        }

        CameraMove(DeltaTime);
        RobotMove(DeltaTime);
        ActorUpdate(DeltaTime);
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
    private List<PointLightComponent> _PointLightComponents = new List<PointLightComponent>();
    private List<SpotLightComponent> _SpotLightComponents = new List<SpotLightComponent>();
    private List<HierarchicalInstancedStaticMeshComponent> _HISMComponents = new List<HierarchicalInstancedStaticMeshComponent>();

    public IReadOnlyList<CameraComponent> CameraComponents => _CameraComponents;
    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;
    public IReadOnlyList<DirectionLightComponent> DirectionLightComponents => _DirectionLightComponents;
    public IReadOnlyList<PointLightComponent> PointLightComponents => _PointLightComponents;
    public IReadOnlyList<SpotLightComponent> SpotLightComponents => _SpotLightComponents;
    public IReadOnlyList<HierarchicalInstancedStaticMeshComponent> HISMComponents => _HISMComponents;

    public SkyboxComponent?  CurrentSkybox { get; private set; }
    public void RegistComponent(PrimitiveComponent component)
    {
        if (PrimitiveComponents.Contains(component))
        {
            return;
        }
        if (component is SubHierarchicalInstancedStaticMeshComponent == false)
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
        else if (component is HierarchicalInstancedStaticMeshComponent hierarchicalInstancedStaticMeshComponent)
        {
            if (!_HISMComponents.Contains(hierarchicalInstancedStaticMeshComponent))
            {
                _HISMComponents.Add(hierarchicalInstancedStaticMeshComponent);  
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
        if (component is SubHierarchicalInstancedStaticMeshComponent == false)
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
        else if (component is HierarchicalInstancedStaticMeshComponent hierarchicalInstancedStaticMeshComponent) 
        {
            if (_HISMComponents.Contains(hierarchicalInstancedStaticMeshComponent))
            {
                _HISMComponents.Remove(hierarchicalInstancedStaticMeshComponent);
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