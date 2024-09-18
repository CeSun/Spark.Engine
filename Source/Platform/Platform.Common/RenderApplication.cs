using Silk.NET.Windowing;
using Spark.Core;
using System.Diagnostics;

namespace Spark.Platform.Common;

public class RenderApplication : BaseApplication
{

    private AutoResetEvent RenderWaitEvent = new AutoResetEvent(false);

    private AutoResetEvent UpdateWaitEvent = new AutoResetEvent(true);

    private ManualResetEvent CloseWindowWaitEvent = new ManualResetEvent(false);

    private float FramesPerSecond = 1000 / 60.0F;
    public RenderApplication(Engine engine) : base(engine)
    {

    }

    private void Configure()
    {
        if (Engine.MainView != null)
        {
            Engine.MainView.Closing += () =>
            {
                Engine.RequestClose();
                RenderWaitEvent.Set();
                CloseWindowWaitEvent.WaitOne();
            };
            Engine.MainView.Resize += size => Engine.Resize(size.X, size.Y);

            Engine.MainView.ClearContext();
        }
    }

    public override void Run()
    {
        Configure();
        Engine.Start();
        var stopwatch = Stopwatch.StartNew();
        RunRender();
        while (Engine.WantClose == false)
        {
            if (stopwatch.ElapsedMilliseconds < FramesPerSecond)
            {
                Wait(FramesPerSecond - stopwatch.ElapsedMilliseconds);
            }
            var deltaTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            Engine.Platform.View?.DoEvents();
            Engine.Update(deltaTime);
            UpdateWaitEvent.WaitOne();
            RenderWaitEvent.Set();
        }
        stopwatch.Stop();
        Engine.Stop();
    }
    private void RunRender()
    {
        new Thread(() =>
        {
            if (Engine.MainView != null)
            {
                Engine.MainView.MakeCurrent();
            }
            while (Engine.WantClose == false)
            {
                RenderWaitEvent.WaitOne();
                Engine.Render();
                Engine.MainView?.SwapBuffers();
                UpdateWaitEvent.Set();
            }
            Engine.RenderDestory();
            CloseWindowWaitEvent.Set();
        }).Start();
    }

}
