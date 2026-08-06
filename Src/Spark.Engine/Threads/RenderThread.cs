using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Spark.Engine.Threads;

public class RenderThread
{
    private readonly EngineApplication _engineApplication;

    public ServiceProvider ServiceProvider => _engineApplication.ServiceProvider;

    private bool _isClosing => _engineApplication.IsClosing;

    private readonly Thread _thread;

    public RenderThread(EngineApplication engineApplication)
    {
        _engineApplication = engineApplication;

        _thread = new Thread(run);
    }

    public void Start()
    {
        _thread.Start();
    }

    private void run()
    {
        while (_isClosing == false)
        {
            try
            {
                var buffer = _engineApplication.DualFrameBuffer.GetReadyBuffer();
                render(buffer);
                Console.WriteLine("Render Thread");
                _engineApplication.DualFrameBuffer.ReturnEmpty();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private void render(FrameData? frame)
    {
        if (frame == null)
            return;

        Console.WriteLine("Render frame");
    }
}
