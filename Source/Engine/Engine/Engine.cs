using Silk.NET.OpenGLES;
using Silk.NET.Input;
using Spark.Core.Platform;
using Silk.NET.Windowing;
using Spark.Core.Render;
using Spark.Util;

namespace Spark.Core;

public partial class Engine
{
    private IGameConfig _gameConfig;
    public Engine(IPlatform platform, IGameConfig gameConfig)
    {
        SyncContext = SingleThreadSyncContext.Initialize();

        Platform = platform;

        _gameConfig= gameConfig;

        if (GraphicsApi != null)
        {
            SceneRenderer = _gameConfig.CreateRenderer(GraphicsApi);
        }

        Game = _gameConfig.CreateGame();

        MainWorld = new World(this);

        Worlds.Add(MainWorld);
    }

    public IGame? Game;
    public SingleThreadSyncContext? SyncContext { get; private set; }

    public HashSet<World> Worlds = [];

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
            Game?.Update(world, deltaTime);
            world.Update(deltaTime);
        }
    }

    public void Start()
    {
        foreach (var world in Worlds)
        {
            world.BeginPlay();
            Game?.BeginPlay(world);
        }
    }

    public void Stop()
    {
        foreach (var world in Worlds)
        {
            world.Destory();
            Game?.EndPlay(world);
        }
    }

    public void Render()
    {
        if (SceneRenderer != null)
        {
            SceneRenderer.Render();
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