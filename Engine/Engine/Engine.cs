using Silk.NET.OpenGL;
using Spark.Util;
using Spark.Engine.Core;
using System.Numerics;
using System.Drawing;
using Spark.Engine.Core.Render;
using Silk.NET.Input;

namespace Spark.Engine;

public class Engine : Singleton<Engine>
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
    public GL? Gl { get; set; }
    public IInputContext? Input { get; set; }
    List<World> Worlds = new List<World>();
    public void InitEngine(string[] args, Dictionary<string, object> objects)
    {
        Gl = (GL)objects["OpenGL"];
        WindowSize = (Point)objects["WindowSize"];
        Input = (IInputContext)objects["InputContext"];
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
        Gl?.Viewport(new Rectangle(0, 0, Width, Height));
        WindowSize = new(Width, Height);
    }
    
    public Point WindowSize { get; private set; }
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


}