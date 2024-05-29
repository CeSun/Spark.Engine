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
    public List<Action<GL>> NextRenderFrame { get; private set; } = [];

    public List<Action<double>> NextFrame { get; private set; } = [];
    private List<string> Args { get; } = [];

    public World? MainWorld;

    public List<BaseSubSystem> SubSystems = [];

    public Dictionary<string, bool> SubsystemConfigs= [];
    public Engine(string[] args, IPlatform platform)
    {
        SyncContext = new SingleThreadSyncContext();
        SynchronizationContext.SetSynchronizationContext(SyncContext);

        AssetMgr = new AssetMgr{ Engine = this };

        Args.AddRange(args);
        Platform = platform;

        IFileSystem.Init(platform.FileSystem);

        WindowSize = new Point(View.Size.X, View.Size.Y);
        MainWorld = new World(this);
        MainWorld.WorldMainRenderTarget = MainWorld.SceneRenderer.CreateRenderTarget(this.WindowSize.X, this.WindowSize.Y);
        Worlds.Add(MainWorld);
        GameAssemblyLoadContext.InitInstance(this);

        if (View is IWindow window)
        {
            window.FileDrop += paths => OnFileDrop.Invoke(paths);
        }
        ProcessArgs();
        LoadGameDll();
        LoadSetting();
        LoadSubsystem();

    }


    public List<World> Worlds = [];
  

    public event Action<string[]> OnFileDrop = _ => { };
   
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
            if (obj is BaseSubSystem subsystem)
            {
                SubSystems.Add(subsystem);
            }
        }


    }
    public void LoadGameDll()
    {
        using var stream =  FileSystem.GetStreamReader($"{GameName}/{GameName}.dll");
        GameAssemblyLoadContext.Instance.LoadFromStream(stream.BaseStream);
    }
    public void LoadSetting()
    {
        var text = FileSystem.LoadText($"{GameName}/DefaultGame.ini");
        var parser =new IniDataParser();
        var ini = parser.Parse(text);
        var defaultGameMode = ini["Game"]["DefaultGameMode"];
        var gameConfig = new GameConfig
        {
            DefaultGameModeClass = typeof(GameMode),
        };

        if (string.IsNullOrEmpty(defaultGameMode) == false)
        {
            gameConfig.DefaultGameModeClass = AssemblyHelper.GetType(defaultGameMode);
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

        foreach(var subsystem in SubSystems)
        {
            if (subsystem.ReceiveUpdate)
            {
                subsystem.Update(deltaTime);
            }
        }
    }

    public void Render(double deltaTime)
    {
        var count = NextRenderFrame.Count;
        for (int i = 0; i < count; i++)
        {
            NextRenderFrame[i](GraphicsApi);
        }
        NextRenderFrame.Clear();
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