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
using Silk.NET.Input;

using static Spark.Engine.StaticEngine;
using Texture = Spark.Engine.Core.Assets.Texture;

namespace Spark.Engine.Core;

public partial class Level
{

    public World CurrentWorld { private set; get; }
    public Level(World world)
    {
        CurrentWorld = world;
    }

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
            if (RobotActor == null)
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
    public void BeginPlay()
    {
        MainMouse.MouseMove += OnMouseMove;
        MainMouse.MouseDown += OnMouseKeyDown;

        // 定义一个actor和并挂载静态网格体组件
        var RobotActor = new Actor(this);
        var RobotMeshComp = new StaticMeshComponent(RobotActor);
        RobotMeshComp.StaticMesh = new StaticMesh("/StaticMesh/untitled.glb");
        RobotActor.RootComponent = RobotMeshComp;
        RobotMeshComp.WorldScale = new Vector3(5, 5, 5);
        RobotMeshComp.WorldRotation = Quaternion.CreateFromYawPitchRoll(180F.DegreeToRadians(), 0, 0);
        RobotMeshComp.WorldLocation -= RobotMeshComp.UpVector * 2;
        this.RobotActor = RobotActor;

        // 相机actor
        var CameraActor = new Actor(this);
        CameraComponent = new CameraComponent(RobotActor);
        CameraActor.RootComponent = CameraComponent;
        CameraComponent.NearPlaneDistance = 1;
        CameraComponent.FarPlaneDistance =  100f;
        CameraComponent.ProjectionType = ProjectionType.Perspective;
        CameraComponent.RelativeLocation += new Vector3(0, 1.5f, 2);
        CameraComponent.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0F.DegreeToRadians(), -10f.DegreeToRadians(), 0);

        // 加载个cube作为地板
        var CubeActor = new Actor(this);
        var CubeMeshComp = new StaticMeshComponent(RobotActor);
        CubeMeshComp.StaticMesh = new StaticMesh("/StaticMesh/cube2.glb");
        CubeActor.RootComponent = CubeMeshComp;
        CubeMeshComp.WorldScale = new Vector3(30, 1, 30);
        CubeMeshComp.WorldLocation -= CubeMeshComp.UpVector * 4F;
        CubeMeshComp.StaticMesh.Materials[0].IsReflection = 1;
        var skybox = new SkyboxComponent(CubeActor);
        skybox.SkyboxCube = new TextureCube("/Skybox/pm");

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
        PointLightComp.Color = Color.DeepPink;
        PointLightComp.LightStrength =1f;
        PointLightComp.WorldLocation += PointLightComp.UpVector * 10 - PointLightComp.RightVector * 2;



        var PointLight2 = new Actor(this);
        var PointLightComp2 = new PointLightComponent(PointLight2);
        PointLight2.RootComponent = PointLightComp2;
        PointLightComp2.Color = Color.Purple;
        PointLightComp.LightStrength = 1f;
        PointLightComp2.WorldLocation += PointLightComp2.UpVector * 10 + PointLightComp2.RightVector * 2;
       
    }

    public void Destory() 
    { 
    }
    float a = 0;
    public void Update(double DeltaTime)
    {
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
    public IReadOnlyList<CameraComponent> CameraComponents => _CameraComponents;
    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;
    public IReadOnlyList<DirectionLightComponent> DirectionLightComponents => _DirectionLightComponents;
    public IReadOnlyList<PointLightComponent> PointLightComponents => _PointLightComponents;
    public IReadOnlyList<SpotLightComponent> SpotLightComponents => _SpotLightComponents;

    public SkyboxComponent?  CurrentSkybox { get; private set; }
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