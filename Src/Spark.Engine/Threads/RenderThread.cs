using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spark.Engine.Render;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Spark.Engine.Threads;

public class RenderThread
{
    private readonly EngineApplication _engineApplication;

    public ServiceProvider ServiceProvider => _engineApplication.ServiceProvider;

    private bool IsClosing => _engineApplication.IsClosing;

    private readonly Thread _thread;
    private readonly ILogger<RenderThread> _logger;

    public RenderThread(EngineApplication engineApplication)
    {
        _engineApplication = engineApplication;

        _logger = engineApplication.ServiceProvider.GetRequiredService<ILogger<RenderThread>>();

        _thread = new Thread(Run);
    }

    public void Start()
    {
        _thread.Start();
    }

    public void WaitForExit()
    {
        _thread.Join();
    }

    private void Run()
    {
        while (IsClosing == false)
        {
            try
            {
                var buffer = _engineApplication.DualFrameBuffer.GetReadyBuffer();
                Render(buffer);
                _engineApplication.DualFrameBuffer.ReturnEmpty();
            }
            catch (Exception e)
            {
                if (!IsClosing)
                {
                    _logger.LogError(e, "RenderThread run error");
                }
            }
        }
    }

    private void Render(FrameData? frame)
    {
        if (frame == null)
            return;
    }
}
