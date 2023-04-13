using Silk.NET.OpenGL;
using Spark.Util;
using Spark.Engine.Core;
using System.Numerics;
using System.Drawing;
using Spark.Engine.Core.Render;
using Silk.NET.Input;
using Spark.Engine.Platform;

namespace Spark.Engine;

public partial class Engine : Singleton<Engine>
{
    public RenderTarget? _GlobalRenderTarget;
    
    public RenderTarget ViewportRenderTarget
    {
        get
        {
            if (_GlobalRenderTarget == null)
                throw new Exception("rt 为空");
            return _GlobalRenderTarget;
        }
    }
    List<World> Worlds = new List<World>();
    public void InitEngine(string[] args, Dictionary<string, object> objects)
    {
        Gl = (GL)objects["OpenGL"];
        if (Gl != null)
        {
            var versionstr = Gl.GetStringS(GLEnum.Version);
            if (versionstr.IndexOf("ES") >= 0)
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
        _GlobalRenderTarget = new RenderTarget(WindowSize.X, WindowSize.Y, true);
        Worlds.Add(new World());
    }
    public void Update(double DeltaTime)
    {
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