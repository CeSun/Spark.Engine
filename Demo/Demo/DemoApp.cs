using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Render.Common;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;

namespace Demo;

/// <summary>
/// 演示内容：构建世界、两个相机（两个窗口不同视角观察同一场景）、两个三角形 + 两堵砖墙 + 投影聚光灯，
/// 并让墙左右摆动。平台无关——只依赖 Spark.Engine 核心；各平台入口（桌面/编辑器）在 InitializeCallback 里调用
/// <see cref="Initialize"/>。
/// </summary>
public static class DemoApp
{
    /// <summary>搭建演示场景（作为 <see cref="EngineApplication.InitializeCallback"/> 使用）。</summary>
    public static void Initialize(EngineApplication app)
    {
        // 资源目录：随入口程序输出目录拷贝的 Assets 文件夹（AppContext.BaseDirectory = 入口程序输出目录）
        var assetsDir = Path.Combine(AppContext.BaseDirectory, "Assets");

        // 加载图片 → RGBA8 Texture2D（ImageSharp 解码 jpg）
        Texture2D LoadTexture(string fileName)
        {
            var path = Path.Combine(assetsDir, fileName);
            using var image = Image.Load<Rgba32>(path);
            var rgba = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(rgba);
            return new Texture2D((uint)image.Width, (uint)image.Height, rgba);
        }

        // 创建世界
        var world = new World();
        app.WorldContext.CurrentWorld = world;
        world.Scene.ResourceManager = app.ResourceManager;

        // 两个窗口、两个不同视角，观察同一场景（验证 RenderGraph 帧级 acquire/present 收口）
        var mainWindow = app.WindowManager.MainWindow;
        var mainViewport = app.WindowManager.GetViewport(mainWindow)!;

        // 第二个窗口：右侧上方俯看同一场景
        var secondWindow = app.WindowManager.CreateWindow("Spark Engine — 侧面视角", 800, 600);
        var secondViewport = app.WindowManager.GetViewport(secondWindow)!;

        AddCamera(world, mainViewport, eye: new Vector3(0f, 0f, 1.5f), lookAt: new Vector3(0f, 0f, -2f));
        AddCamera(world, secondViewport, eye: new Vector3(3.5f, 1.5f, 1.5f), lookAt: new Vector3(0f, 0f, -2.5f));

        // P6 验证：自适应布局 + 裁剪 + 焦点导航（切换回原 Demo 改为 UIDemoOverlay.Build()）
        var uiCanvas = app.UIManager.GetOrCreateCanvas(mainViewport.Id);
        uiCanvas.Root = P6VerifyOverlay.Build();

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

        // 背景墙 mesh（左右两墙共享）：局部坐标（中心在原点），由 RelativeLocation 定位到 z=-4
        var wallNormal = new Vector3(0f, 0f, 1f);
        var wallMesh = new StaticMesh(
            new[]
            {
                new StaticMeshVertex(new Vector3(-1.5f, -2f, 0f), Vector3.One, new Vector2(0f, 0f), wallNormal),
                new StaticMeshVertex(new Vector3(1.5f, -2f, 0f), Vector3.One, new Vector2(1f, 0f), wallNormal),
                new StaticMeshVertex(new Vector3(1.5f, 2f, 0f), Vector3.One, new Vector2(1f, 1f), wallNormal),
                new StaticMeshVertex(new Vector3(-1.5f, 2f, 0f), Vector3.One, new Vector2(0f, 1f), wallNormal),
            },
            new uint[] { 0, 1, 2, 0, 2, 3 });

        // 加载真实砖墙贴图：颜色贴图 + 法线贴图（jpg → RGBA8）
        var wallColorTexture = LoadTexture("brickwall.jpg");
        var wallNormalTexture = LoadTexture("brickwall_normal.jpg");

        // 左墙：有法线贴图
        var wallWithNormal = new Material
        {
            ShadingModel = ShadingModel.Lit,
            BaseColor = Vector4.One,
            Roughness = 0.9f,
            BaseColorTexture = wallColorTexture,
            NormalTexture = wallNormalTexture,
        };

        // 右墙：无法线贴图（对照）
        var wallWithoutNormal = new Material
        {
            ShadingModel = ShadingModel.Lit,
            BaseColor = Vector4.One,
            Roughness = 0.9f,
            BaseColorTexture = wallColorTexture,
        };

        var leftWall = new StaticMeshComponent
        {
            Mesh = wallMesh,
            Material = wallWithNormal,
            CastShadow = false,
            RelativeLocation = new Vector3(-2f, 0f, -4f),
        };
        var leftWallActor = new Actor();
        leftWallActor.AddOwnedComponent(leftWall);
        world.AddActor(leftWallActor);

        var rightWall = new StaticMeshComponent
        {
            Mesh = wallMesh,
            Material = wallWithoutNormal,
            CastShadow = false,
            RelativeLocation = new Vector3(2f, 0f, -4f),
        };
        var rightWallActor = new Actor();
        rightWallActor.AddOwnedComponent(rightWall);
        world.AddActor(rightWallActor);

        // 聚光光源（CastShadow）：朝 -Z 照射，把两个三角形投到背景墙上；固定偏移到 (0.5, 0, 0)
        // （光源与相机错开，阴影才会投到三角形侧面可见的位置）
        var spotLight = new SpotLightComponent
        {
            RelativeLocation = new Vector3(0.5f, 0f, 0f),
            Color = Vector3.One,
            Intensity = 1.5f,
            Range = 20f,
            InnerConeAngle = 0.5f,
            OuterConeAngle = 1.1f,
            CastShadow = true,
        };
        var lightActor = new Actor();
        lightActor.AddOwnedComponent(spotLight);
        world.AddActor(lightActor);

        // 骨骼网格：两段"手臂"条带，关节在原点，绕 Z 轴弯曲（GPU 蒙皮）
        var armComponent = new SkeletalMeshComponent
        {
            Mesh = CreateSkeletalArm(),
            Material = new Material
            {
                ShadingModel = ShadingModel.Lit,
                BaseColor = new Vector4(1f, 0.4f, 0.2f, 1f),
                Roughness = 0.6f,
            },
            RelativeLocation = new Vector3(0f, 0f, -2f),
        };
        var armActor = new Actor();
        armActor.AddOwnedComponent(armComponent);
        world.AddActor(armActor);
        world.AddActor(new SkeletalAnimator(armComponent));

        // 让两堵墙一起绕自身中心（Y 轴）左右摆动：墙面法线方向持续变化，观察不同方向受光
        world.AddActor(new WallSwinger(leftWall, rightWall));
    }

    /// <summary>两段骨骼"手臂"条带：下段绑 bone0，上段绑 bone1，关节在原点，bind pose 为单位阵。</summary>
    private static SkeletalMesh CreateSkeletalArm()
    {
        var normal = new Vector3(0f, 0f, 1f);
        var vertices = new SkeletalMeshVertex[]
        {
            // 下段（bone 0）
            new SkeletalMeshVertex(new Vector3(-0.15f, -1f, 0f), Vector3.One, new Vector2(0f, 0f), normal, 0u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(0.15f, -1f, 0f), Vector3.One, new Vector2(1f, 0f), normal, 0u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(0.15f, 0f, 0f), Vector3.One, new Vector2(1f, 1f), normal, 0u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(-0.15f, 0f, 0f), Vector3.One, new Vector2(0f, 1f), normal, 0u, new Vector4(1f, 0f, 0f, 0f)),
            // 上段（bone 1）
            new SkeletalMeshVertex(new Vector3(-0.15f, 0f, 0f), Vector3.One, new Vector2(0f, 0f), normal, 1u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(0.15f, 0f, 0f), Vector3.One, new Vector2(1f, 0f), normal, 1u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(0.15f, 1f, 0f), Vector3.One, new Vector2(1f, 1f), normal, 1u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(-0.15f, 1f, 0f), Vector3.One, new Vector2(0f, 1f), normal, 1u, new Vector4(1f, 0f, 0f, 0f)),
        };
        var indices = new uint[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        return new SkeletalMesh(vertices, indices, new[] { Matrix4x4.Identity, Matrix4x4.Identity });
    }

    /// <summary>创建相机 Actor 并摆到指定视角（WorldTransform = R·T，GetViewMatrix = Invert(WorldTransform)）。</summary>
    private static void AddCamera(World world, RenderTarget target, Vector3 eye, Vector3 lookAt)
    {
        var camera = new CameraComponent { RenderTarget = target };

        // 用 lookAt 的逆反推相机位姿：view = Invert(cameraWorld)，cameraWorld 的旋转即相机朝向、平移即 eye
        var view = Matrix4x4.CreateLookAt(eye, lookAt, Vector3.UnitY);
        Matrix4x4.Invert(view, out var cameraWorld);
        camera.RelativeLocation = eye;
        camera.RelativeRotation = Quaternion.CreateFromRotationMatrix(cameraWorld);

        var actor = new Actor();
        actor.AddOwnedComponent(camera);
        world.AddActor(actor);
    }
}