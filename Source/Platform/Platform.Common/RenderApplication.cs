using Silk.NET.Windowing;
using Spark.Core;
using System.Diagnostics;

namespace Common;

public class RenderApplication : BaseApplication
{
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
                while (RenderThreadExit == false)
                {
                    Thread.Sleep(1);
                }
            };
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
            UpdateWaitEvent.WaitOne();
            stopwatch.Stop();
            var deltaTime = stopwatch.ElapsedMilliseconds;
            stopwatch.Restart();
            Engine.Platform.View?.DoEvents();
            Engine.Update(deltaTime);
            RenderWaitEvent.Set();
        }
        stopwatch.Stop();
        Engine.Stop();
    }

    private AutoResetEvent RenderWaitEvent = new AutoResetEvent(false);

    private AutoResetEvent UpdateWaitEvent = new AutoResetEvent(true);

    private bool RenderThreadExit = false;
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
            RenderThreadExit = true;
        }).Start();
    }

}
