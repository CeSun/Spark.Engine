using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine;

public class RenderEngine
{
    Thread RenderThread;
    IView View;
    Func<IView> FunCreateView;
    public RenderEngine(Func<IView> CreateView)
    {
        FunCreateView = CreateView;
        RenderThread = new Thread(Run);
        RenderThread.Start();
    }

     
    private void Run()
    {
         
    }
}
