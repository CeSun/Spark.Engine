using Silk.NET.OpenGLES;
using Spark.Util;
using System.Drawing;
using Silk.NET.Input;
using Spark.Engine.Platform;
using Silk.NET.Windowing;
using System.Reflection;
using Spark.Engine.Assets;
using IniParser.Parser;
using Spark.Engine.Actors;
using Spark.Engine.Assembly;
using Spark.Engine.Attributes;

namespace Spark.Engine;

public partial class Engine
{
    public AssetMgr AssetMgr { get; private set; }
    public SingleThreadSyncContext? SyncContext { get; private set; }
    public List<Action> NextFrame { get; private set; }
    private List<string> Args { get; } = [];
    public bool IsMobile { private set; get; }

    public bool IsDS = false;

    public World? MainWorld;
    public uint DefaultFBOID ;
    public List<BaseSubSystem> SubSystems = new List<BaseSubSystem>();

    public Dictionary<string, bool> SubsystemConfigs= new();
    public Engine()
    {
        if (SynchronizationContext.Current == null)
        {
            SyncContext = new SingleThreadSyncContext();
            SynchronizationContext.SetSynchronizationContext(SyncContext);
        }
        NextFrame = new List<Action>();
        AssetMgr = new AssetMgr() { Engine = this };
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
    public List<World> Worlds = new List<World>();
    public void InitEngine(string[] args, Dictionary<string, object> objects)
    {
        Args.AddRange(args);
        Gl = (GL)objects["OpenGL"];
        WindowSize = (Point)objects["WindowSize"];
        Input = (IInputContext)objects["InputContext"];
        IsMobile = (bool)objects["IsMobile"];
        DefaultFBOID = (uint)(int)objects["DefaultFBOID"];
        _view = (IView)objects["View"];
        MainWorld = new World(this);
        MainWorld.WorldMainRenderTarget = MainWorld.SceneRenderer.CreateRenderTarget(this.WindowSize.X, this.WindowSize.Y);
        Worlds.Add(MainWorld);
        GameAssemblyLoadContext.InitInstance(this);

        if (_view is IWindow window)
        {
            window.FileDrop += paths => OnFileDrog.Invoke(paths);
        }
        ProcessArgs();
        LoadGameDll();
        LoadSetting();
        LoadSubsystem();
    }

    public event Action<string[]> OnFileDrog = paths => { };
   
    public void ProcessArgs()
    {
        for(int i = 0; i < Args.Count; i++)
        {
            if (i < Args.Count - 1)
            {
                if (Args[i] == "-game")
                    GameName = Args[i + 1];
            }
        }

    }

    public T? GetSubSystem<T>() where T : BaseSubSystem
    {
        foreach(var subsytem in SubSystems)
        {
            if (subsytem is T t)
                return t;
        }
        return null;
    }
    private void LoadSubsystem()
    {
        foreach(var type in AssemblyHelper.GetAllType())
        {
            if (type.IsSubclassOf(typeof(BaseSubSystem)) == false)
                continue;
            if (type.FullName == null || SubsystemConfigs.Keys.Contains(type.FullName))
                continue;
            var att = type.GetCustomAttribute<SubsystemAttribute>();
            if (att == null || att.Enable == false)
            {
                SubsystemConfigs.Add(type.FullName, false);
            }    
            else
            {
                SubsystemConfigs.Add(type.FullName, true);
            }
            
        }

        foreach(var (k, v) in SubsystemConfigs.ToDictionary())
        {
            var type = AssemblyHelper.GetType(k);
            if (type == null || type.IsSubclassOf(typeof(BaseSubSystem)) == false)
            {
                SubsystemConfigs.Remove(k);
                continue;
            }
            if (v == false)
                continue;
            var obj = Activator.CreateInstance(type, [this]);
            if (obj != null && obj is BaseSubSystem subsystem)
            {
                SubSystems.Add(subsystem);
            }
        }


    }
    public void LoadGameDll()
    {
        using(var stream =  FileSystem.Instance.GetStreamReader($"{GameName}/{GameName}.dll"))
        {
            GameAssemblyLoadContext.Instance.LoadFromStream(stream.BaseStream);
        }
    }
    public void LoadSetting()
    {
        var text = FileSystem.Instance.LoadText($"{GameName}/DefaultGame.ini");
        var parser =new IniDataParser();
        var ini = parser.Parse(text);
        var DefaultGameMode = ini["Game"]["DefaultGameMode"];
        GameConfig gameConfig = new GameConfig
        {
            DefaultGameModeClass = typeof(GameMode),
        };

        if (string.IsNullOrEmpty(DefaultGameMode) == false)
        {
            gameConfig.DefaultGameModeClass = AssemblyHelper.GetType(DefaultGameMode);
        }
        gameConfig.DefaultLevel = ini["Game"]["DefaultLevel"];
        GameConfig = gameConfig;

        foreach (var item in ini["Subsystem"])
        {
            if (SubsystemConfigs.Keys.Contains(item.KeyName))
                continue;
            if (item.Value.ToLower() == "true")
            {
                SubsystemConfigs.Add(item.KeyName, true);
            }
            if (item.Value.ToLower() == "false")
            {
                SubsystemConfigs.Add(item.KeyName, false);
            }
        }
    }

    public GameConfig GameConfig { get; private set; }

    public string GameName { get; private set; } = string.Empty;
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

        foreach(var subsystem in SubSystems)
        {
            if (subsystem.ReceiveUpdate == true)
            {
                subsystem.Update(DeltaTime);
            }
        }
    }

    public void Render(double deltaTime)
    {
        Worlds.ForEach(world => world.Render(deltaTime));
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

public struct GameConfig
{
    public Type? DefaultGameModeClass;
    public string DefaultLevel = string.Empty;

    public GameConfig()
    {
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