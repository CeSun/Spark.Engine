using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spark.Engine.Threads;

public class RenderThread
{
    private EngineApplication _engineApplication;
    
    public ServiceProvider ServiceProvider => _engineApplication.ServiceProvider;

    private bool _isClosing => _engineApplication.IsClosing;

    private Thread _thread;

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
            render();
        }
    }

    private void render()
    {

    }
}
