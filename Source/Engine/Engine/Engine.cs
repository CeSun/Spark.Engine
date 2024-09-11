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

        WindowSize = new Point(View.Size.X, View.Size.Y);

        MainWorld = new World(this);

        MainWorld.WorldMainRenderTarget = MainWorld.SceneRenderer.CreateRenderTargetByFrameBufferId(WindowSize.X, WindowSize.Y, Platform.DefaultFrameBufferId);

        Worlds.Add(MainWorld);

        if (View is IWindow window)
        {
            window.FileDrop += OnFileDrop.Invoke;
        }

    }

    public List<World> Worlds = [];
  

    public event Action<string[]> OnFileDrop = _ => { };
   
    
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
    public IView View => Platform.View;

    public IKeyboard? MainKeyBoard
    {
        get => Input.Keyboards.FirstOrDefault();
    }

    public IMouse? MainMouse
    {
        get => Input.Mice.FirstOrDefault();
    }
}


public static class GlExternFunctions
{
    static bool _supportDebugGroup = true;
    public static void PushGroup(this GL gl, string debugInfo)
    {
#if DEBUG
        if (_supportDebugGroup == false)
            return;
        try
        {
            gl.PushDebugGroup(DebugSource.DebugSourceApplication, 1, (uint)debugInfo.Length, debugInfo);
        }
        catch
        {
            _supportDebugGroup = false;
        }
#endif
    }

    public static void PopGroup(this GL gl)
    {
#if DEBUG
        if (_supportDebugGroup == false)
            return;
        try
        {
            gl.PopDebugGroup();
        }
        catch
        {
            _supportDebugGroup = false;
        }
#endif
    }

}