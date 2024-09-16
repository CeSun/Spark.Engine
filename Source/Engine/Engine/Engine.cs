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

    public List<World> Worlds = [];

    public World MainWorld;

    public IRenderer? SceneRenderer;

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
        Worlds.ForEach(world => world.Update(deltaTime));
    }

    public void Start()
    {
        Worlds.ForEach(world => world.BeginPlay());
    }

    public void Stop()
    {
        Worlds.ForEach(world => world.Destory());
    }

    public void Render()
    {
        if (SceneRenderer != null)
        {
            foreach (var world in Worlds)
            {
                if (world.RenderWorld == null)
                    continue;
                SceneRenderer.Render(world.RenderWorld);
            }
        }
    }

    public void RenderDestory()
    {
        if (SceneRenderer != null)
        {
            foreach (var world in Worlds)
            {
                if (world.RenderWorld == null)
                    continue;
                SceneRenderer.Destory();
            }
        }
    }


    public void Resize(int width, int height)
    {
        MainWorld?.Resize(width, height);
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