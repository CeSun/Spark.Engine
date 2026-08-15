using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Builder;
using Spark.Engine.Components;
using Spark.Engine.Desktop;
using Spark.Engine.Render;
using Spark.Engine.Worlds;

var builder = EngineBuilder.Create(args);

builder.InitializeWebGPU();

builder.UseDesktop();

var game = builder.Build();

// 游戏初始化逻辑全部写在初始化回调里（Run 时、窗口创建后、主循环前执行）
game.InitializeCallback = app =>
{
    // 创建世界
    var world = new World();
    app.WorldContext.CurrentWorld = world;
    world.Scene.MeshLibrary = app.MeshLibrary;

    // 创建相机 Actor，绑定主视口
    var cameraActor = new Actor();
    var camera = new CameraComponent();
    cameraActor.AddOwnedComponent(camera);
    world.AddActor(cameraActor);

    var viewport = app.WindowManager.GetViewport(app.WindowManager.MainWindow);
    camera.RenderTarget = viewport;

    // 创建三角形网格（顶点：位置 + 颜色）
    var mesh = new StaticMesh(
        new[]
        {
            new StaticMeshVertex(new Vector3(-0.5f, -0.5f, -2f), new Vector3(1f, 0f, 0f)),
            new StaticMeshVertex(new Vector3(0.5f, -0.5f, -2f), new Vector3(0f, 1f, 0f)),
            new StaticMeshVertex(new Vector3(0f, 0.5f, -2f), new Vector3(0f, 0f, 1f)),
        },
        new uint[] { 0, 1, 2 });

    // 创建网格 Actor（StaticMeshComponent；网格在 BeginPlay 时经 MeshLibrary 自动上传）
    var meshActor = new Actor();
    meshActor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh });
    world.AddActor(meshActor);

    // 创建点光源 Actor（LightComponent）
    var lightActor = new Actor();
    lightActor.AddOwnedComponent(new LightComponent
    {
        Type = LightType.Point,
        Intensity = 5f,
    });
    world.AddActor(lightActor);
};

game.Run();
