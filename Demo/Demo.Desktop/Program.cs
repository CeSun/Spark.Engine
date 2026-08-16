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

    // 基础材质：Lit + 基础色纹理（P1：着色模型 + 纹理采样）
    var material = new Material
    {
        ShadingModel = ShadingModel.Lit,
        BaseColorTexture = texture,
        Roughness = 0.4f,
    };

    // 材质实例：引用基础材质，覆写 roughness 与底色 tint（P3：实例参数覆写）
    var materialInstance = new MaterialInstance { Parent = material };
    materialInstance.SetScalar(MaterialParam.Roughness, 0.9f);
    materialInstance.SetVector(MaterialParam.BaseColor, new Vector4(1f, 0.5f, 0.5f, 1f));

    // 左三角：基础材质
    var meshActorLeft = new Actor();
    meshActorLeft.AddOwnedComponent(new StaticMeshComponent { Mesh = meshLeft, Material = material });
    world.AddActor(meshActorLeft);

    // 右三角：材质实例（同 shader、异参数）
    var meshActorRight = new Actor();
    meshActorRight.AddOwnedComponent(new StaticMeshComponent { Mesh = meshRight, Material = materialInstance });
    world.AddActor(meshActorRight);

    // 创建点光源 Actor（LightComponent；位置来自 Actor 世界变换）
    var lightActor = new Actor();
    lightActor.AddOwnedComponent(new LightComponent
    {
        Type = LightType.Point,
        Color = Vector3.One,
        Intensity = 8f,
        Range = 10f,
    });
    world.AddActor(lightActor);
};

game.Run();
