using Silk.NET.OpenGLES;
using Spark.Util;
using System.Drawing;
using Silk.NET.Input;
using Spark.Engine.Platform;
using Silk.NET.Windowing;

namespace Spark.Engine;

public partial class Engine
{
    public SingleThreadSyncContext? SyncContext { get; private set; }

    public World MainWorld;
    public Engine(IPlatform platform)
    {
        SyncContext = SingleThreadSyncContext.Initialize();

        Platform = platform;

        WindowSize = new Point(MainView.Size.X, MainView.Size.Y);

        MainWorld = new World(this);

        MainWorld.WorldMainRenderTarget = MainWorld.SceneRenderer.CreateRenderTargetByFrameBufferId(WindowSize.X, WindowSize.Y, Platform.DefaultFrameBufferId);

        Worlds.Add(MainWorld);

    }

    public List<World> Worlds = [];
   
    public void Update(double deltaTime)
    {
        SyncContext?.Tick();
        Worlds.ForEach(world => world.Update(deltaTime));
    }

    public void Render(double deltaTime)
    {
        Worlds.ForEach(world => world.Render(deltaTime));
    }

    public void Start()
    {
        Worlds.ForEach(world => world.BeginPlay());
    }

    public void Stop()
    {
        Worlds.ForEach(world => world.Destory());
    }

    public void Resize(int width, int height)
    {
        WindowSize = new(width, height);
        MainWorld?.Resize(width, height);
    }

    public Action<int, int>? OnWindowResize;

    public Point WindowSize { get; private set; }
}


public partial class Engine
{
    public IPlatform Platform { get; private set; }
    public GL GraphicsApi => Platform.GraphicsApi;
    public IInputContext Input => Platform.InputContext;
    public IFileSystem FileSystem => Platform.FileSystem;
    public IView MainView => Platform.View;

    public IKeyboard? MainKeyBoard
    {
        get => Input.Keyboards.FirstOrDefault();
    }

    public IMouse? MainMouse
    {
        get => Input.Mice.FirstOrDefault();
    }
}