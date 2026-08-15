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
    world.Scene.ResourceManager = app.ResourceManager;

    // 创建相机 Actor，绑定主视口
    var cameraActor = new Actor();
    var camera = new CameraComponent();
    cameraActor.AddOwnedComponent(camera);
    world.AddActor(cameraActor);

    var viewport = app.WindowManager.GetViewport(app.WindowManager.MainWindow);
    camera.RenderTarget = viewport;

    // 创建三角形网格（顶点：位置 + 颜色 + UV）
    var mesh = new StaticMesh(
        new[]
        {
            new StaticMeshVertex(new Vector3(-0.5f, -0.5f, -2f), Vector3.One, new Vector2(0f, 0f)),
            new StaticMeshVertex(new Vector3(0.5f, -0.5f, -2f), Vector3.One, new Vector2(1f, 0f)),
            new StaticMeshVertex(new Vector3(0f, 0.5f, -2f), Vector3.One, new Vector2(0.5f, 1f)),
        },
        new uint[] { 0, 1, 2 });

    // 2x2 纹理：红 / 绿 / 蓝 / 白（RGBA8）
    var texture = new Texture2D(2, 2, new byte[]
    {
        255, 0, 0, 255,       // (0,0) 红
        0, 255, 0, 255,       // (1,0) 绿
        0, 0, 255, 255,       // (0,1) 蓝
        255, 255, 255, 255,   // (1,1) 白
    });

    // 创建网格 Actor（StaticMeshComponent；网格/纹理在 BeginPlay 时经 ResourceManager 自动上传）
    var meshActor = new Actor();
    meshActor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh, Texture = texture });
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
