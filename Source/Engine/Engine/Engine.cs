﻿using Silk.NET.OpenGLES;
using Spark.Util;
using System.Drawing;
using Silk.NET.Input;
using Spark.Engine.Platform;
using Silk.NET.Windowing;
using Spark.Engine;

namespace Spark.Engine;

public partial class Engine
{
    public SingleThreadSyncContext? SyncContext { get; private set; }
    public List<Action<GL>> NextRenderFrame { get; private set; } = [];
    public List<Action<double>> NextFrame { get; private set; } = [];

    public World? MainWorld;

    public List<BaseSubSystem> SubSystems = [];

    public Engine(IPlatform platform)
    {
        SyncContext = SingleThreadSyncContext.Initialize();

        Platform = platform;

        WindowSize = new Point(View.Size.X, View.Size.Y);

        MainWorld = new World(this);

        MainWorld.WorldMainRenderTarget = MainWorld.SceneRenderer.CreateRenderTargetByFrameBufferId(this.WindowSize.X, this.WindowSize.Y);

        Worlds.Add(MainWorld);

        if (View is IWindow window)
        {
            window.FileDrop += OnFileDrop.Invoke;
        }

    }


    public List<World> Worlds = [];
  

    public event Action<string[]> OnFileDrop = _ => { };
   
    
    public T? GetSubSystem<T>() where T : BaseSubSystem
    {
        foreach(var subsystem in SubSystems)
        {
            if (subsystem is T t)
                return t;
        }
        return null;
    }
    public void Update(double deltaTime)
    {
        var count = NextFrame.Count;
        for (int i = 0; i < count; i++)
        {
            NextFrame[i](deltaTime);
        }
        NextFrame.Clear();
        SyncContext?.Tick();
        Worlds.ForEach(world => world.Update(deltaTime));

        foreach (var subsystem in SubSystems.Where(subsystem => subsystem.ReceiveUpdate))
        {
            subsystem.Update(deltaTime);
        }
    }

    public void Render(double deltaTime)
    {
        var count = NextRenderFrame.Count;
        NextRenderFrame.ForEach(render => render(GraphicsApi));
        NextRenderFrame.Clear();
        Worlds.ForEach(world => world.Render(deltaTime));
        foreach (var subsystem in SubSystems.Where(subsystem => subsystem.ReceiveRender))
        {
            subsystem.Render(deltaTime);
        }
    }

    public void Start()
    {
        Worlds.ForEach(world => world.BeginPlay());
        SubSystems.ForEach(subsystem => subsystem.BeginPlay());
    }

    public void Stop()
    {
        SubSystems.ForEach(subsystem => subsystem.EndPlay());
        Worlds.ForEach(world => world.Destory());
    }

    public void Resize(int width, int height)
    {
        WindowSize = new(width, height);
        OnWindowResize?.Invoke(width, height);
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
    public bool IsMobile => Platform.IsMobile;

    public IView View => Platform.View;


    public IKeyboard MainKeyBoard
    {
        get
        {
            if (Input == null)
                throw new Exception("no input");
            var kb = Input.Keyboards.FirstOrDefault();
            if (kb == null)
            {
                throw new Exception("no keyboard");
            }
            return kb;
        }
    }


    public IMouse MainMouse
    {
        get
        {
            if (Input == null)
                throw new Exception("no input");
            var mouse = Input.Mice.FirstOrDefault();
            if (mouse == null)
            {
                throw new Exception("no mouse");
            }
            return mouse;
        }
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

    private static OpenGlStates? _states = null;
    public static void PushAttribute(this GL gl)
    {
        if (_states == null)
            _states = new OpenGlStates();
        else
        {
            var newStates = new OpenGlStates
            {
                Next = _states
            };
            _states = newStates;
        }
        _states.Blend = gl.GetInteger(GLEnum.Blend);
        _states.BlendDstAlpha = gl.GetInteger(GLEnum.BlendDstAlpha);
        _states.BlendDstRgb = gl.GetInteger(GLEnum.BlendDstRgb);
        _states.BlendEquationAlpha = gl.GetInteger(GLEnum.BlendEquationAlpha);
        _states.BlendEquationRgb = gl.GetInteger(GLEnum.BlendEquationRgb);
        _states.BlendSrcAlpha = gl.GetInteger(GLEnum.BlendSrcAlpha);
        _states.BlendSrcRgb = gl.GetInteger(GLEnum.BlendSrcRgb);

        _states.DepthTest = gl.GetInteger(GLEnum.DepthTest);
        _states.DepthFunc = gl.GetInteger(GLEnum.DepthFunc);
        _states.DepthWriteMask = gl.GetInteger(GLEnum.DepthWritemask);
        _states.CullFace = gl.GetInteger(GLEnum.CullFace);
    }

    public static void PopAttribute(this GL gl)
    {
        if (_states == null)
            return;
        gl.BlendEquationSeparate((GLEnum)_states.BlendEquationRgb, (GLEnum)_states.BlendEquationAlpha);
        gl.BlendFuncSeparate((GLEnum)_states.BlendSrcRgb, (GLEnum)_states.BlendDstRgb, (GLEnum)_states.BlendSrcAlpha, (GLEnum)_states.BlendDstAlpha);
        gl.Enable(GLEnum.Blend, (uint)_states.Blend);
        gl.Enable(GLEnum.DepthTest, (uint)_states.DepthTest);
        gl.DepthFunc((GLEnum)_states.DepthFunc);
        gl.DepthMask(_states.DepthWriteMask == 1);
        gl.Enable(GLEnum.CullFace, (uint)_states.CullFace);

        _states = _states.Next;

    }

}

public struct GameConfig
{
    public Type? DefaultGameModeClass;
    public string? DefaultLevel = string.Empty;

    public GameConfig()
    {
    }
}
class OpenGlStates
{

    public int Blend;
    public int BlendDstAlpha;
    public int BlendDstRgb;
    public int BlendEquationAlpha;
    public int BlendEquationRgb;
    public int BlendSrcAlpha;
    public int BlendSrcRgb;

    public int DepthTest;
    public int DepthFunc;
    public int DepthWriteMask;
    public int CullFace;

    public OpenGlStates? Next;
}