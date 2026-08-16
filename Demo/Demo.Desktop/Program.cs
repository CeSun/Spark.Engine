using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Builder;
using Spark.Engine.Components;
using Spark.Engine.Desktop;
using Spark.Engine.Render;
using Spark.Engine.Render.Pipeline.Forward;
using Spark.Engine.Render.Resources;
using Spark.Engine.Worlds;

var builder = EngineBuilder.Create(args);

builder.InitializeWebGPU();

builder.UseDesktop();

builder.UseForward();

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

    // 2x2 纹理：红 / 绿 / 蓝 / 白（RGBA8）
    var texture = new Texture2D(2, 2, new byte[]
    {
        255, 0, 0, 255,       // (0,0) 红
        0, 255, 0, 255,       // (1,0) 绿
        0, 0, 255, 255,       // (0,1) 蓝
        255, 255, 255, 255,   // (1,1) 白
    });

    // 两个三角形（顶点：位置 + 颜色 + UV + 法线，法线朝 +Z 面向相机/光源）
    var normal = new Vector3(0f, 0f, 1f);
    var meshLeft = new StaticMesh(
        new[]
        {
            new StaticMeshVertex(new Vector3(-1.2f, -0.5f, -2f), Vector3.One, new Vector2(0f, 0f), normal),
            new StaticMeshVertex(new Vector3(-0.2f, -0.5f, -2f), Vector3.One, new Vector2(1f, 0f), normal),
            new StaticMeshVertex(new Vector3(-0.7f, 0.5f, -2f), Vector3.One, new Vector2(0.5f, 1f), normal),
        },
        new uint[] { 0, 1, 2 });

    var meshRight = new StaticMesh(
        new[]
        {
            new StaticMeshVertex(new Vector3(0.2f, -0.5f, -2f), Vector3.One, new Vector2(0f, 0f), normal),
            new StaticMeshVertex(new Vector3(1.2f, -0.5f, -2f), Vector3.One, new Vector2(1f, 0f), normal),
            new StaticMeshVertex(new Vector3(0.7f, 0.5f, -2f), Vector3.One, new Vector2(0.5f, 1f), normal),
        },
        new uint[] { 0, 1, 2 });

    // 左三角：Unlit + 纹理（测试纹理采样，无光照）
    var material = new Material
    {
        ShadingModel = ShadingModel.Unlit,
        BaseColorTexture = texture,
    };

    // 右三角：Unlit + 纯蓝（测试 base color，无光照）
    var materialRight = new Material
    {
        ShadingModel = ShadingModel.Unlit,
        BaseColor = new Vector4(0f, 0f, 1f, 1f),
    };

    // 左三角
    var meshActorLeft = new Actor();
    meshActorLeft.AddOwnedComponent(new StaticMeshComponent { Mesh = meshLeft, Material = material });
    world.AddActor(meshActorLeft);

    // 右三角
    var meshActorRight = new Actor();
    meshActorRight.AddOwnedComponent(new StaticMeshComponent { Mesh = meshRight, Material = materialRight });
    world.AddActor(meshActorRight);

    // 背景墙（接收阴影、不投射）：z=-4 的大四边形，朝 +Z
    var wallNormal = new Vector3(0f, 0f, 1f);
    var wallMesh = new StaticMesh(
        new[]
        {
            new StaticMeshVertex(new Vector3(-3f, -2f, -4f), Vector3.One, new Vector2(0f, 0f), wallNormal),
            new StaticMeshVertex(new Vector3(3f, -2f, -4f), Vector3.One, new Vector2(1f, 0f), wallNormal),
            new StaticMeshVertex(new Vector3(3f, 2f, -4f), Vector3.One, new Vector2(1f, 1f), wallNormal),
            new StaticMeshVertex(new Vector3(-3f, 2f, -4f), Vector3.One, new Vector2(0f, 1f), wallNormal),
        },
        new uint[] { 0, 1, 2, 0, 2, 3 });

    var wallMaterial = new Material
    {
        ShadingModel = ShadingModel.Lit,
        BaseColor = new Vector4(0.6f, 0.6f, 0.6f, 1f),
        Roughness = 0.9f,
    };

    var wallActor = new Actor();
    wallActor.AddOwnedComponent(new StaticMeshComponent { Mesh = wallMesh, Material = wallMaterial, CastShadow = false });
    world.AddActor(wallActor);

    // 聚光光源（CastShadow）：偏移到 (0.5, 0, 0)、朝 -Z 照射，把两个三角形投到背景墙上
    // （光源与相机错开，阴影才会投到三角形侧面可见的位置）
    var lightActor = new Actor();
    lightActor.AddOwnedComponent(new SpotLightComponent
    {
        RelativeLocation = new Vector3(0.5f, 0f, 0f),
        Color = Vector3.One,
        Intensity = 1.5f,
        Range = 20f,
        InnerConeAngle = 0.5f,
        OuterConeAngle = 1.1f,
        CastShadow = true,
    });
    world.AddActor(lightActor);
};

game.Run();
