using Silk.NET.OpenGLES;
using Silk.NET.Input;
using Spark.Core.Platform;
using Silk.NET.Windowing;
using Spark.Core.Render;
using Spark.Util;

namespace Spark.Core;

public partial class Engine
{
    public Engine(IPlatform platform)
    {
        SyncContext = SingleThreadSyncContext.Initialize();

        Platform = platform;

        if (GraphicsApi != null)
        {
            SceneRenderer = new DeferredRenderer(GraphicsApi);
        }

        MainWorld = new World(this);

        Worlds.Add(MainWorld);
    }
    public SingleThreadSyncContext? SyncContext { get; private set; }

    public HashSet<World> Worlds = [];

    public HashSet<RenderWorld> RenderWorlds = [];

    public World MainWorld;

    public BaseRenderer? SceneRenderer;

    public bool WantClose { get; private set; } = false;
    public void RequestClose()
    {
        if (WantClose == false)
        {
            WantClose = true;
        }
    }

    public void Update(double deltaTime)
    {
        SyncContext?.Tick();
        foreach (var world in Worlds)
        {
            world.Update(deltaTime);
        }
    }

    public void Start()
    {
        foreach (var world in Worlds)
        {
            world.BeginPlay();
        }
    }

    public void Stop()
    {
        foreach (var world in Worlds)
        {
            world.Destory();
        }
    }

    public void Render()
    {
        if (SceneRenderer != null)
        {
            SceneRenderer.Update();
            foreach (var renderWorld in RenderWorlds)
            {
                SceneRenderer.Render(renderWorld);
            }
        }
    }

    public void RenderDestory()
    {
        if (SceneRenderer != null)
        {
            SceneRenderer.Destory();
        }
    }


    public void Resize(int width, int height)
    {
        MainWorld?.WorldMainRenderTarget?.Resize(width, height);
    }

    public Action<int, int>? OnWindowResize;
}


public partial class Engine
{
    public IPlatform Platform { get; private set; }
    public GL? GraphicsApi => Platform.GraphicsApi;
    public IInputContext? Input => Platform.InputContext;
    public IFileSystem FileSystem => Platform.FileSystem;
    public IView? MainView => Platform.View;

    public IKeyboard? MainKeyBoard
    {
        get => Input?.Keyboards.FirstOrDefault();
    }

    public IMouse? MainMouse
    {
        get => Input?.Mice.FirstOrDefault();
    }
}