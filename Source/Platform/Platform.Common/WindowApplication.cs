

using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Drawing;

namespace Spark.Platform.Common;

public class WindowApplication
{

    public Engine Engine { get; set; }

    public WindowApplication(Engine engine)
    {
        Engine = engine;
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



    public void Run()
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
            int i = 0;
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
