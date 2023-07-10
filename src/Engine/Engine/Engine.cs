using Silk.NET.Input;
using Silk.NET.Windowing;
using Spark.Engine.Platform;
using Spark.Engine.Render;
using Spark.Engine.Util;
using System.Diagnostics;

namespace Spark.Engine;

public class Engine
{
    public IPlatform Platform;
    public ManualResetEvent WaitForRenderThread;
    public ManualResetEvent WaitForGameThread;
    public IView View { get; private set; }
    public IInputContext InputContext { get; private set; }
    public RenderThread RenderThread { get; private set; }
    public float UpdatesPerSecond { get; private set; } = 60;
    public float FrameTime { get; private set; }

    public SingleThreadSyncContext SyncContext { get; private set; }
    private LocakFrame LocakFrame;
    public Engine(IPlatform platform)
    {
        WaitForGameThread = new ManualResetEvent(false);
        WaitForRenderThread = new ManualResetEvent(true);
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
        InputContext = RenderThread.InputContext;
        View = RenderThread.View;
        LocakFrame = new LocakFrame(FrameTime);
        SyncContext = new SingleThreadSyncContext();
    }

    public void Run()
    {
        Start();
        while (View.IsClosing == false)
        {
            Update(LocakFrame.Wait());
        }
        Stop();
    }
    private void Update(double deltaTime)
    {
        WaitForRenderThread.WaitOne();
        SyncContext.Tick();
        WaitForGameThread.Set();
        WaitForRenderThread.Reset();
    }

    private void Start()
    {
        Console.WriteLine("Start");
    }

    private void Stop()
    {
        Console.WriteLine("Stop");
    }

}
