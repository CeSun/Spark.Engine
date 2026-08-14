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

// 创建世界
var world = new World();
game.WorldContext.CurrentWorld = world;

// 创建相机 Actor，绑定主视口
var cameraActor = new Actor();
var camera = new CameraComponent();
cameraActor.AddOwnedComponent(camera);
world.AddActor(cameraActor);

var viewport = game.WindowManager.GetViewport(game.WindowManager.MainWindow);
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

// 提交网格到渲染线程（创建 GPU 资源并上传）
game.UploadMesh(mesh);

// 创建网格 Actor（StaticMeshComponent）
var meshActor = new Actor();
meshActor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh });
world.AddActor(meshActor);

game.Run();
