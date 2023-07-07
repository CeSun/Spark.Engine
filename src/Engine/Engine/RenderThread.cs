using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Spark.Engine.Util;

namespace Spark.Engine;

public class RenderThread
{
    public IView View { get; private set; }
    public GL gl { get; private set; }

    private Engine Engine;
    private LocakFrame LocakFrame;
    public RenderThread(Func<IView> CreateView, Engine engine)
    {
        Engine = engine;
        View = CreateView();
        View.Initialize();
        gl = View.CreateOpenGLES();
        LocakFrame = new LocakFrame(Engine.FrameTime);
        View.Render += _ => Render(LocakFrame.Wait());
        View.Closing += Closing;
    }
    public void Run()
    {
        Thread.Sleep(500);
        View.Run();
    }

   
    void Render(double deltaTime)
    {
        gl.ClearColor(System.Drawing.Color.Red);
        gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    void Closing()
    {

    }

    
}
