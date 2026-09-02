using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Render.Common;
using Spark.Engine.Resources;
using Spark.Engine.UI;
using Spark.Engine.Worlds;

namespace Demo;

/// <summary>
/// 演示内容：编辑器 MVP 第一步——编辑器布局（菜单栏 + 场景层级面板 + 检查器 + 状态栏）
/// 叠在 3D 场景之上（左右面板遮挡、中间透明露出场景），场景沿用原 Demo：
/// 两个三角形 + 两堵砖墙（有/无法线贴图）+ 投影聚光灯 + 骨骼手臂 + 摆墙动画 + UIRenderView 画中画。
/// </summary>
public static class DemoApp
{
    private static StaticMeshComponent? _leftWall;
    private static StaticMeshComponent? _rightWall;
    private static SkeletalMeshComponent? _arm;

    /// <summary>搭建演示场景（作为 <see cref="EngineApplication.InitializeCallback"/> 使用）。</summary>
    public static void Initialize(EngineApplication app)
    {
        // 资源目录：随入口程序输出目录拷贝的 Assets 文件夹（AppContext.BaseDirectory = 入口程序输出目录）
        var assetsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets");

        // 加载图片 → RGBA8 Texture2D（ImageSharp 解码 jpg）
        Texture2D LoadTexture(string fileName)
        {
            var path = System.IO.Path.Combine(assetsDir, fileName);
            using var image = Image.Load<Rgba32>(path);
            var rgba = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(rgba);
            return new Texture2D((uint)image.Width, (uint)image.Height, rgba);
        }

        // 创建世界
        var world = new World(app.ResourceManager);
        app.WorldContext.CurrentWorld = world;

        // ———— 3D 场景（沿用原 Demo 内容） ————
        var mainWindow = app.WindowManager.MainWindow;
        var mainViewport = app.WindowManager.GetViewport(mainWindow)!;

        AddCamera(world, mainViewport, eye: new Vector3(0f, 0f, 1.5f), lookAt: new Vector3(0f, 0f, -2f));

        // 2x2 纹理：红 / 绿 / 蓝 / 白
        var texture = new Texture2D(2, 2, new byte[]
        {
            255, 0, 0, 255,
            0, 255, 0, 255,
            0, 0, 255, 255,
            255, 255, 255, 255,
        });

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

        var material = new Material
        {
            ShadingModel = ShadingModel.Unlit,
            BaseColorTexture = texture,
        };
        var materialRight = new Material
        {
            ShadingModel = ShadingModel.Unlit,
            BaseColor = new Vector4(0f, 0f, 1f, 1f),
        };

        AddMeshActor(world, meshLeft, material);
        AddMeshActor(world, meshRight, materialRight);

        // 背景墙（左右两墙共享 mesh）
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

        var wallColorTexture = LoadTexture("brickwall.jpg");
        var wallNormalTexture = LoadTexture("brickwall_normal.jpg");

        var wallWithNormal = new Material
        {
            ShadingModel = ShadingModel.Lit,
            BaseColor = Vector4.One,
            Roughness = 0.9f,
            BaseColorTexture = wallColorTexture,
            NormalTexture = wallNormalTexture,
        };
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
        _leftWall = leftWall;
        _rightWall = rightWall;

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
        _arm = armComponent;
        world.AddActor(new SkeletalAnimator(armComponent));
        world.AddActor(new WallSwinger(leftWall, rightWall));
    }

    /// <summary>配置由 <c>UseEditor()</c> 创建的编辑器视口。</summary>
    public static void ConfigureEditor(EngineApplication app, EditorUi editorUi)
    {
        editorUi.SetControlTestWindowLauncher(() => ControlTestWindow.Open(app));

        var world = app.WorldContext.CurrentWorld
            ?? throw new InvalidOperationException("The demo world must be initialized before the editor.");

        editorUi.SetRuntimeWorldInitializer(runtime =>
        {
            var left = FindComponent<StaticMeshComponent>(runtime, _leftWall?.ComponentGuid);
            var right = FindComponent<StaticMeshComponent>(runtime, _rightWall?.ComponentGuid);
            if (left != null && right != null)
                runtime.AddActor(new WallSwinger(left, right));

            var arm = FindComponent<SkeletalMeshComponent>(runtime, _arm?.ComponentGuid);
            if (arm != null)
                runtime.AddActor(new SkeletalAnimator(arm));
        });

        var renderView = app.CreateRenderView(320, 240);
        var renderViewControl = new UIRenderView
        {
            RenderViewId = renderView.Id,
            ResolutionScale = 1.5f,
            MaintainAspectRatio = true,
        };
        CameraComponent? offscreenCamera = null;
        renderViewControl.RenderViewResizeRequested = (oldId, width, height) =>
        {
            var next = app.CreateRenderView(width, height);
            if (offscreenCamera != null)
                offscreenCamera.RenderTarget = next;
            if (app.RenderTargets.TryGet(oldId, out var oldTarget) && oldTarget is TextureRenderTarget oldTex)
                app.DestroyRenderView(oldTex);
            return next.Id;
        };
        offscreenCamera = AddCamera(world, renderView, eye: new Vector3(-3f, 3f, 3f), lookAt: new Vector3(0f, 0f, -2f));

        editorUi.SetPictureInPicture(renderViewControl);
    }

    private static void AddMeshActor(World world, StaticMesh mesh, Material material)
    {
        var actor = new Actor();
        actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh, Material = material });
        world.AddActor(actor);
    }

    private static T? FindComponent<T>(World world, Guid? componentGuid) where T : ActorComponent
    {
        if (componentGuid is not { } guid)
            return null;
        return world.EnumerateActors(includePendingActors: true)
            .SelectMany(actor => actor.Components)
            .OfType<T>()
            .SingleOrDefault(component => component.ComponentGuid == guid);
    }

    /// <summary>两段骨骼"手臂"条带：下段绑 bone0，上段绑 bone1，关节在原点，bind pose 为单位阵。</summary>
    private static SkeletalMesh CreateSkeletalArm()
    {
        var normal = new Vector3(0f, 0f, 1f);
        var vertices = new SkeletalMeshVertex[]
        {
            new SkeletalMeshVertex(new Vector3(-0.15f, -1f, 0f), Vector3.One, new Vector2(0f, 0f), normal, 0u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(0.15f, -1f, 0f), Vector3.One, new Vector2(1f, 0f), normal, 0u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(0.15f, 0f, 0f), Vector3.One, new Vector2(1f, 1f), normal, 0u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(-0.15f, 0f, 0f), Vector3.One, new Vector2(0f, 1f), normal, 0u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(-0.15f, 0f, 0f), Vector3.One, new Vector2(0f, 0f), normal, 1u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(0.15f, 0f, 0f), Vector3.One, new Vector2(1f, 0f), normal, 1u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(0.15f, 1f, 0f), Vector3.One, new Vector2(1f, 1f), normal, 1u, new Vector4(1f, 0f, 0f, 0f)),
            new SkeletalMeshVertex(new Vector3(-0.15f, 1f, 0f), Vector3.One, new Vector2(0f, 1f), normal, 1u, new Vector4(1f, 0f, 0f, 0f)),
        };
        var indices = new uint[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
        return new SkeletalMesh(vertices, indices, new[] { Matrix4x4.Identity, Matrix4x4.Identity });
    }

    /// <summary>创建相机 Actor 并摆到指定视角（WorldTransform = R·T，GetViewMatrix = Invert(WorldTransform)）。</summary>
    private static CameraComponent AddCamera(World world, RenderTarget target, Vector3 eye, Vector3 lookAt)
    {
        var camera = new CameraComponent { RenderTarget = target };

        var view = Matrix4x4.CreateLookAt(eye, lookAt, Vector3.UnitY);
        Matrix4x4.Invert(view, out var cameraWorld);
        camera.RelativeLocation = eye;
        camera.RelativeRotation = System.Numerics.Quaternion.CreateFromRotationMatrix(cameraWorld);

        var actor = new Actor();
        actor.AddOwnedComponent(camera);
        world.AddActor(actor);
        return camera;
    }
}
