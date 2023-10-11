using Silk.NET.OpenGLES;
using Spark.Util;
using System.Numerics;
using System.Drawing;
using Silk.NET.Input;
using Spark.Engine.Platform;
using Spark.Engine.Render;
using Spark.Engine.GUI;
using Silk.NET.Windowing;
using Silk.NET.OpenGLES.Extensions.EXT;

namespace Spark.Engine;

public partial class Engine : Singleton<Engine>
{
    public RenderTarget? _GlobalRenderTarget;
    SingleThreadSyncContext SyncContext;

    public bool IsMobile { private set; get; } = false;
    public RenderTarget ViewportRenderTarget
    {
        get
        {
            if (_GlobalRenderTarget == null)
                throw new Exception("rt 为空");
            return _GlobalRenderTarget;
        }
    }
    public Engine()
    {
        SyncContext = new SingleThreadSyncContext();
        SynchronizationContext.SetSynchronizationContext(SyncContext);

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
        Gl = (GL)objects["OpenGL"];
        if (Gl != null)
        {
            var versionstr = Gl.GetStringS(GLEnum.Version);
            if (versionstr != null && versionstr.IndexOf("ES") >= 0)
            {
                GLType = GLType.ES;
            }
            else
            {
                GLType = GLType.Desktop;
            }
        }
        WindowSize = (Point)objects["WindowSize"];
        Input = (IInputContext)objects["InputContext"];
        FileSystem = (FileSystem)objects["FileSystem"];
        IsMobile = (bool)objects["IsMobile"];
        _view = (IView)objects["View"];
        _GlobalRenderTarget = new RenderTarget(WindowSize.X, WindowSize.Y, true);
        Worlds.Add(new World());


    }
    public void Update(double DeltaTime)
    {
        SyncContext.Tick();
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
        ViewportRenderTarget.Width = Width;
        ViewportRenderTarget.Height = Height;
        WindowSize = new(Width, Height);
    }

    public Point WindowSize { get; private set; }
}


public partial class Engine : Singleton<Engine>
{
    public GL? Gl { get; set; }
    public IInputContext? Input { get; set; }

    public Platform.FileSystem? FileSystem { get; set; }
}

public enum GLType
{
    Desktop,
    ES,
    Web
}
public partial class Engine : Singleton<Engine>
{
    public GLType GLType { get; internal set; }
}
public class StaticEngine
{
    public static GL gl
    {
        get
        {
            if (Engine.Instance.Gl == null)
            {
                throw new Exception("no gl context");
            }
            return Engine.Instance.Gl;
        }
    }

    private static ExtBaseInstance? _ExtBaseInstance;
    public static ExtBaseInstance? ExtBaseInstance 
    {
        get
        {
            if (_ExtBaseInstance == null)
            {
                gl.TryGetExtension<ExtBaseInstance>(out _ExtBaseInstance);
            }
            return _ExtBaseInstance;
        }
    }


    public static IInputContext Input
    {
        get
        {
            if (Engine.Instance.Input == null)
            {
                throw new Exception("no Input context");
            }
            return Engine.Instance.Input;
        }
    }

    public static IKeyboard MainKeyBoard
    {
        get
        {
            var kb = Input.Keyboards.FirstOrDefault();
            if (kb == null)
            {
                throw new Exception("no keyboard");
            }
            return kb;
        }
    }


    public static IMouse MainMouse
    {
        get
        {
            var mouse = Input.Mice.FirstOrDefault();
            if (mouse == null)
            {
                throw new Exception("no mouse");
            }
            return mouse;
        }
    }

    public static Platform.FileSystem FileSystem
    { 
        get
        {
            var fileSystem = Engine.Instance.FileSystem;
            if (fileSystem == null)
            {
                throw new Exception("no fileSystem");
            }
            return fileSystem;
        }
    }


}

public static class GLExternFunctions
{
    public static void PushDebugGroup(this GL gl, string DebugInfo)
    {
        gl.PushDebugGroup(DebugSource.DebugSourceApplication,1, (uint)DebugInfo.Length,  DebugInfo);
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