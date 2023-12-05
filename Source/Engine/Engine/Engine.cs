using Silk.NET.OpenGLES;
using Spark.Util;
using System.Numerics;
using System.Drawing;
using Silk.NET.Input;
using Spark.Engine.Platform;
using Spark.Engine.Render;
using Silk.NET.Windowing;
using Silk.NET.OpenGLES.Extensions.EXT;
using Spark.Engine.GamePlay;
using System.Reflection;
using System.Runtime.Loader;

namespace Spark.Engine;

public partial class Engine
{
    public SingleThreadSyncContext? SyncContext { get; private set; }

    public List<Action> NextFrame { get; private set; }

    private List<string> Args { get; set; } = new List<string>();
    public bool IsMobile { private set; get; } = false;

    public uint DefaultFBOID ;
    public Engine()
    {
        if (SynchronizationContext.Current == null)
        {
            SyncContext = new SingleThreadSyncContext();
            SynchronizationContext.SetSynchronizationContext(SyncContext);
        }
        NextFrame = new List<Action>();
       
    }
    private IView? _view;
    public IView View
    {
        get
        {
            if (_view == null)
                throw new Exception();
            return _view;
        }
    }
    List<World> Worlds = new List<World>();
    public void InitEngine(string[] args, Dictionary<string, object> objects)
    {
        Args.AddRange(args);
        Gl = (GL)objects["OpenGL"];
        WindowSize = (Point)objects["WindowSize"];
        Input = (IInputContext)objects["InputContext"];
        IsMobile = (bool)objects["IsMobile"];
        DefaultFBOID = (uint)(int)objects["DefaultFBOID"];
        _view = (IView)objects["View"];

        InitGameDll();
        Worlds.Add(new World(this));


    }
    public IGame? Game { get; private set; }
    public void InitGameDll()
    {
        string? GameDllName = null;
        for (int i = 0; i < Args.Count; i++)
        {
            if (Args[i] == "-game" && i + 1 < Args.Count)
            {
                GameDllName = Args[i + 1];
                break;
            }
        }
        if (GameDllName == null)
            throw new Exception("no game dll");
        using var sr = FileSystem.Instance.GetStreamReader(GameDllName + ".dll");
        var asm = AssemblyLoadContext.Default.LoadFromStream(sr.BaseStream);

        foreach(var assembly in AssemblyLoadContext.Default.Assemblies)
        {
            foreach(var type in assembly.GetTypes())
            {
                if (type == null)
                    continue;
                if (type.GetInterface(typeof(IGame).FullName??"") != null)
                {
                    var obj = assembly.CreateInstance(type.FullName??"");
                    Game = obj as IGame;
                    return;
                }
            }
        }
    }

    public Action<Level>? OnBeginPlay;
    public Action<Level>? OnEndPlay;

    public void Update(double DeltaTime)
    {
        var count = NextFrame.Count;
        for(int i = 0; i < count; i++)
        {
            NextFrame[i]?.Invoke();
        }
        NextFrame.Clear();
        SyncContext?.Tick();
        Worlds.ForEach(world => world.Update(DeltaTime));
    }

    public void Render(double DeltaTime)
    {
        Worlds.ForEach(world => world.Render(DeltaTime));
    }

    public void Start()
    {
        Worlds.ForEach(world => world.BeginPlay());
    }

    public void Stop()
    {
        Worlds.ForEach(world => world.Destory());
    }

    public void Resize(int Width, int Height)
    {
        WindowSize = new(Width, Height);
        OnWindowResize?.Invoke(Width, Height);
    }

    public Action<int, int>? OnWindowResize;

    public Point WindowSize { get; private set; }
}


public partial class Engine
{
    public GL? Gl { get; set; }
    public IInputContext? Input { get; set; }






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


public static class GLExternFunctions
{
    static bool SupportDebugGroup = true;
    public static void PushGroup(this GL gl, string DebugInfo)
    {
#if DEBUG
        if (SupportDebugGroup == false)
            return;
        try
        {
            gl.PushDebugGroup(DebugSource.DebugSourceApplication, 1, (uint)DebugInfo.Length, DebugInfo);
        }
        catch
        {
            SupportDebugGroup = false;
        }
#endif
    }

    public static void PopGroup(this GL gl)
    {
#if DEBUG
        if (SupportDebugGroup == false)
            return;
        try
        {
            gl.PopDebugGroup();
        }
        catch
        {
            SupportDebugGroup = false;
        }
#endif
    }

    static OpenGLStates? States = null;
    public static void PushAttribute(this GL gl)
    {
        if (States == null)
            States = new OpenGLStates();
        else
        {
            var newStates = new OpenGLStates();
            newStates.Next = States;
            States = newStates;
        }
        States.Blend = gl.GetInteger(GLEnum.Blend);
        States.BlendDstAlpha = gl.GetInteger(GLEnum.BlendDstAlpha);
        States.BlendDstRGB = gl.GetInteger(GLEnum.BlendDstRgb);
        States.BlendEquationAlpha = gl.GetInteger(GLEnum.BlendEquationAlpha);
        States.BlendEquationRgb = gl.GetInteger(GLEnum.BlendEquationRgb);
        States.BlendSrcAlpha = gl.GetInteger(GLEnum.BlendSrcAlpha);
        States.BlendSrcRGB = gl.GetInteger(GLEnum.BlendSrcRgb);

        States.DepthTest = gl.GetInteger(GLEnum.DepthTest);
        States.DepthFunc = gl.GetInteger(GLEnum.DepthFunc);
        States.DepthWriteMask = gl.GetInteger(GLEnum.DepthWritemask);
        States.CullFace = gl.GetInteger(GLEnum.CullFace);
    }

    public static void PopAttribute(this GL gl)
    {
        if (States == null)
            return;
        gl.BlendEquationSeparate((GLEnum)States.BlendEquationRgb, (GLEnum)States.BlendEquationAlpha);
        gl.BlendFuncSeparate((GLEnum)States.BlendSrcRGB, (GLEnum)States.BlendDstRGB, (GLEnum)States.BlendSrcAlpha, (GLEnum)States.BlendDstAlpha);
        gl.Enable(GLEnum.Blend, (uint)States.Blend);
        gl.Enable(GLEnum.DepthTest, (uint)States.DepthTest);
        gl.DepthFunc((GLEnum)States.DepthFunc);
        gl.DepthMask(States.DepthWriteMask == 1);
        gl.Enable(GLEnum.CullFace, (uint)States.CullFace);

        States = States.Next;

    }

}

class OpenGLStates
{

    public int Blend;
    public int BlendDstAlpha;
    public int BlendDstRGB;
    public int BlendEquationAlpha;
    public int BlendEquationRgb;
    public int BlendSrcAlpha;
    public int BlendSrcRGB;

    public int DepthTest;
    public int DepthFunc;
    public int DepthWriteMask;
    public int CullFace;

    public OpenGLStates? Next;
}