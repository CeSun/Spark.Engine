using Silk.NET.Input;
using Silk.NET.Windowing;
using Spark.Engine.Platform;
using Spark.Engine.Util;
using System.Diagnostics;

namespace Spark.Engine;

public class Engine
{
    public IPlatform Platform;
    public IView View { get; private set; }
    public IInputContext InputContext { get; private set; }
    public RenderThread RenderThread { get; private set; }
    public float UpdatesPerSecond { get; private set; } = 60;
    public float FrameTime { get; private set; }
    private LocakFrame LocakFrame;
    public Engine(IPlatform platform)
    {
        FrameTime = 1000f / UpdatesPerSecond;
        Platform = platform;
        ManualResetEventWithValue<RenderThread> ViewWaitHandle = new ManualResetEventWithValue<RenderThread>();
        var thread = new Thread (() =>
        {
            var renderThread = new RenderThread(platform.CreateView, this);
            ViewWaitHandle.Set(renderThread);
            renderThread.Run();
        });
        thread.Start();

        RenderThread = ViewWaitHandle.WaitForValue();
        View = RenderThread.View;
        InputContext = View.CreateInput();
        LocakFrame = new LocakFrame(FrameTime);
    }

    public void Run()
    {
        while (View.IsClosing == false)
        {
            Update(LocakFrame.Wait());
        }
    }

    public void Update(double deltaTime)
    {
        Console.WriteLine(deltaTime);
    }
}
