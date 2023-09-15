using System;
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
using System.ComponentModel;

namespace Spark.Engine;

public partial class Level
{
    public PhyWorld PhyWorld { get; private set; }
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
    bool NeedPrintFPS = false;
    public void BeginPlay()
    {
        // InitGrass();
        // Test();
        MainMouse.MouseMove += OnMouseMove;
        MainMouse.MouseDown += OnMouseKeyDown;

        MainKeyBoard.KeyDown += (_, key, _) =>
        {
            if (key == Key.H)
            {
                CreateHISM();
            }
            if (key == Key.I)
            {
                CreateISM();
            }

            if (key == Key.K)
            {
                Delete();
            }
            if (key == Key.F)
            {
                NeedPrintFPS = true;
            }
            if (key == Key.C)
            {
                CreateCubes();
            }
            if (key == Key.M)
            {
                foreach(var actor in temp)
                {
                    if (actor.RootComponent is HierarchicalInstancedStaticMeshComponent hism)
                    {
                        var x = Random.Shared.Next(-100, 100);
                        var y = Random.Shared.Next(-100, 100);
                        hism.AddComponent(new SubInstancedStaticMeshComponent(actor)
                        {
                            WorldLocation = new Vector3 (x, 40, y)
                        });
                        hism.ReBuild();
                    }
                }
            }
        };
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
        CameraComponent.NearPlaneDistance = 1;
        CameraComponent.FarPlaneDistance =  1000f;
        CameraComponent.ProjectionType = ProjectionType.Perspective;
        CameraComponent.WorldLocation += (new Vector3(0, 20, 0) - CameraComponent.ForwardVector * 10);
        CameraComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(0F.DegreeToRadians(), -10f.DegreeToRadians(), 0);

        // 加载个cube作为地板
        var CubeActor = new Actor(this);
        var CubeMeshComp = new StaticMeshComponent(CubeActor);
        CubeActor.RootComponent = CubeMeshComp;
        CubeMeshComp.StaticMesh = new StaticMesh("/StaticMesh/cube2.glb");
        CubeMeshComp.IsStatic = true;
        CubeMeshComp.WorldScale = new Vector3(100, 1, 100);
        CubeMeshComp.WorldLocation = new Vector3(0, 0, 0);

   

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
        PointLightComp.WorldLocation += PointLightComp.UpVector * 50 - PointLightComp.RightVector * 2;



        var spotLight = new Actor(this);
        var SpotLightComponent = new SpotLightComponent(spotLight);
        spotLight.RootComponent = SpotLightComponent;
        SpotLightComponent.Color = Color.Purple;
        SpotLightComponent.LightStrength = 1f;
        SpotLightComponent.WorldLocation += SpotLightComponent.UpVector * 80;
        SpotLightComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, -45f.DegreeToRadians(), 0);

        var SkyBoxActor = new Actor(this);
        var skybox = new SkyboxComponent(SkyBoxActor);
        skybox.SkyboxCube = new TextureCube("/Skybox/pm");
    }


    void CreateCubes()
    {
        var SM = new StaticMesh("/StaticMesh/WoodenCrate.glb");
        for (int i = 0; i < 20; i++)
        {

            var CubeActor2 = new Actor(this);
            var CubeMeshComp2 = new StaticMeshComponent(CubeActor2);
            CubeActor2.RootComponent = CubeMeshComp2;
            CubeMeshComp2.StaticMesh = SM;
            CubeMeshComp2.IsStatic = false;
            float Scale = (float)Random.Shared.NextDouble() ;
            CubeMeshComp2.WorldScale = new Vector3(1F, 1F, 1F);
            CubeMeshComp2.WorldRotation = Quaternion.CreateFromYawPitchRoll(Random.Shared.Next(0, 360), Random.Shared.Next(0, 360), Random.Shared.Next(0, 360));
            CubeMeshComp2.WorldLocation = new Vector3(Random.Shared.Next(-10, 10), Random.Shared.Next(50, 60), 0);
            // temp.Add(CubeActor2);

            Task.Delay(3000).Then(CubeActor2.Destory);

            
        }
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


    public async Task<Actor> InitHISM(string model, int num, Vector2 area, float scale = 1)
    {
        int grassLen = num;
        int len = (int)Math.Sqrt(grassLen);
        var hismactor = new Actor(this);
        var hismcomponent = new HierarchicalInstancedStaticMeshComponent(hismactor);
        hismactor.RootComponent = hismcomponent;
        hismcomponent.StaticMesh = new StaticMesh(model);

        hismcomponent.WorldLocation = new Vector3(0, 0, 0);
        for (int i = 0; i < grassLen; i++)
        {
            var GrassComponent = new SubInstancedStaticMeshComponent(hismactor);
            hismcomponent.AddComponent(GrassComponent);
            GrassComponent.ParentComponent = hismcomponent;
            var x = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var y = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var yaw = Random.Shared.Next(0, 180);
            GrassComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
            GrassComponent.WorldLocation = new Vector3(x, -3, y);
            GrassComponent.WorldScale = new Vector3(scale, scale, scale);
        }

        hismcomponent.Build();


        return hismactor;
    }

    public async Task<Actor> InitISM(string model, int num, Vector2 area, float scale = 1)
    {
        int grassLen = num;
        int len = (int)Math.Sqrt(grassLen);
        var ismactor = new Actor(this);
        var ismcomponent = new InstancedStaticMeshComponent(ismactor);
        ismcomponent.StaticMesh = new StaticMesh(model);

        ismcomponent.WorldLocation = new Vector3(0, 0, 0);
        for (int i = 0; i < grassLen; i++)
        {
            if (i % 10 == 0)
                await Task.Yield();
            var GrassComponent = new SubInstancedStaticMeshComponent(ismactor);
            ismcomponent.AddComponent(GrassComponent);
            GrassComponent.ParentComponent = ismcomponent;
            var x = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var y = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var yaw = Random.Shared.Next(0, 180);
            GrassComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
            GrassComponent.WorldLocation = new Vector3(x, -3, y);
            GrassComponent.WorldScale = new Vector3(scale, scale, scale);
        }

        ismcomponent.Build();
        return ismactor;
    }

    List<Actor> temp = new List<Actor>();
    async void CreateHISM()
    {
        await Console.Out.WriteAsync("[HISM]请输入实例数量:");
        var str = await Console.In.ReadLineAsync();
        int num = int.Parse(str);
        await Console.Out.WriteLineAsync("[HISM]正在生成:" + num);
        int len = (int)Math.Sqrt(100000);

        var task1 = InitHISM("/StaticMesh/flower.glb", num, new Vector2((-len / 2) * 15f, (len / 2) * 15f));
        var task2 = InitHISM("/StaticMesh/grass.glb", num, new Vector2((-len / 2) * 15f, (len / 2) * 15f));

        await Task.WhenAll(task1, task2);

        temp.Add(task1.Result);
        temp.Add(task2.Result);
    }
    async void CreateISM()
    {
        await Console.Out.WriteAsync("[ISM]请输入实例数量:");
        var str = await Console.In.ReadLineAsync();
        int num = int.Parse(str);
        await Console.Out.WriteLineAsync("[ISM]正在生成:" + num);
        int len = (int)Math.Sqrt(100000);
        var task1 = InitISM("/StaticMesh/flower.glb", num, new Vector2((-len / 2) * 15f, (len / 2) * 15f));
        var task2 = InitISM("/StaticMesh/grass.glb", num, new Vector2((-len / 2) * 15f, (len / 2) * 15f));

        await Task.WhenAll(task1, task2);
        temp.Add(task1.Result);
        temp.Add(task2.Result);
    }


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
    }
    float a = 0;
    public void Update(double DeltaTime)
    {
        PhyWorld.Step((float)DeltaTime, false);
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
        if (NeedPrintFPS == true)
        {
            NeedPrintFPS = false;
            Console.Out.WriteLineAsync((1 / DeltaTime) + "FPS");
        }

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
    private List<InstancedStaticMeshComponent> _ISMComponents = new List<InstancedStaticMeshComponent>();

    public IReadOnlyList<CameraComponent> CameraComponents => _CameraComponents;
    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;
    public IReadOnlyList<DirectionLightComponent> DirectionLightComponents => _DirectionLightComponents;
    public IReadOnlyList<PointLightComponent> PointLightComponents => _PointLightComponents;
    public IReadOnlyList<SpotLightComponent> SpotLightComponents => _SpotLightComponents;
    public IReadOnlyList<InstancedStaticMeshComponent> ISMComponents => _ISMComponents;

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