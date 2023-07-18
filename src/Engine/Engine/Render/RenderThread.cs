using Silk.NET.Input;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Spark.Engine.Util;
using System.Collections.Concurrent;

namespace Spark.Engine.Render;

public delegate void RenderCommand(RenderThread renderThread);
public class RenderThread
{
    List<RenderCommand> RenderCommands { get; set; }
    List<RenderCommand> TempRenderCommands { get; set; }
    public IView View { get; private set; }
    public GL gl { get; private set; }
    public IInputContext InputContext { get; private set; }

    private Engine Engine;

    private LocakFrame LocakFrame;

    public RenderScene Scene { private set; get; } = new RenderScene();
    public void AddCommand(RenderCommand command)
    {
        lock(RenderCommands)
        {
            RenderCommands.Add(command);
        }
    }

    public RenderThread(Func<IView> CreateView, Engine engine)
    {
        RenderCommands = new List<RenderCommand>();
        TempRenderCommands = new List<RenderCommand>();
        Engine = engine;
        View = CreateView();
        View.Initialize();
        gl = View.CreateOpenGLES();
        InputContext = View.CreateInput();
        LocakFrame = new LocakFrame(Engine.FrameTime);
        View.Render += _ => Render(LocakFrame.Wait());
        View.Closing += Closing;
    }
    public void Run()
    {
        View.Run();
    }

    void Render(double deltaTime)
    {
        Engine.WaitForGameThread.WaitOne();
        TempRenderCommands.Clear();
        lock (RenderCommands)
        {
            TempRenderCommands.AddRange(RenderCommands);
            RenderCommands.Clear();
        }
        TempRenderCommands.ForEach(command => command(this));
        Scene.Render(deltaTime);
        Engine.WaitForRenderThread.Set();
        Engine.WaitForGameThread.Reset();
    }

    void Closing()
    {
        Engine.WaitForGameThread.Set();
        Engine.WaitForRenderThread.Set();
    }


}
