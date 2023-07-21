using Silk.NET.Input;
using Silk.NET.Windowing;
using Spark.Engine.Platform;
using Spark.Engine.Render;
using Spark.Engine.Util;
using ManualResetEvent = Spark.Engine.Util.ManualResetEvent;

namespace Spark.Engine;

public class Engine
{
    public IPlatform Platform;
    public ManualResetEvent WaitForRenderThread;
    public ManualResetEvent WaitForGameThread;
    public IView View { get; private set; }
    public IInputContext InputContext { get; private set; }
    public RenderThread RenderThread { get; private set; }
    public float UpdatesPerSecond { get; private set; } = 3000;
    public float FrameTime { get; private set; }

    public SingleThreadSyncContext? SyncContext { get; private set; }
    private LocakFrame LocakFrame;
    public Engine(IPlatform platform)
    {
        WaitForGameThread = new ManualResetEvent(false);
        WaitForRenderThread = new ManualResetEvent(true);
        FrameTime = 1000f / UpdatesPerSecond;
        Platform = platform;
        ManualResetEventWithValue<RenderThread> ViewWaitHandle = new ManualResetEventWithValue<RenderThread>();
        RenderThread = new RenderThread(platform.CreateView, this);
        InputContext = RenderThread.InputContext;
        View = RenderThread.View;
        LocakFrame = new LocakFrame(FrameTime);
        var thread = new Thread(() =>
        {
            SyncContext = new SingleThreadSyncContext();
            Start();
            while (View.IsClosing == false)
            {
                Update(LocakFrame.Wait());
            }
            Stop();
        });
        thread.Start();
    }

    
    public void Run()
    {
        RenderThread.Run();
    }
    private void Update(double deltaTime)
    {
        WaitForRenderThread.WaitOne();
        SyncContext?.Tick();
        Console.WriteLine("GameThread:" + deltaTime);
        WaitForGameThread.Set();
        WaitForRenderThread.Reset();
    }

    private void Start()
    {
    }

    private void Stop()
    {
    }

}
