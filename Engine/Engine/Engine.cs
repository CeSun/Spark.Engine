using Silk.NET.OpenGL;
using Spark.Util;
using Spark.Engine.Core;
using System.Numerics;
using System.Drawing;
using Spark.Engine.Core.Render;

namespace Spark.Engine;

public class Engine : Singleton<Engine>
{
    public RenderTarget WindowRenderTarget { 
        get 
        { 
            if (_WindowRenderTarget == null)
            {
                throw new Exception("当前无RenderTarget");
            }
            return _WindowRenderTarget;
        } 
    }

    private RenderTarget? _WindowRenderTarget;
    public GL? Gl { get; set; }
    List<World> Worlds = new List<World>();
    public void InitEngine(string[] args, Dictionary<string, object> objects)
    {
        Gl = (GL)objects["OpenGL"];
        WindowSize = (Point)objects["WindowSize"];
        Worlds.Add(new World());
        _WindowRenderTarget = new RenderTarget(WindowSize.X, WindowSize.Y);
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
        Gl.Viewport(new Rectangle(0, 0, Width, Height));
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


}