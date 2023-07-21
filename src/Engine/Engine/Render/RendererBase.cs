using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Render
{
    public class RendererBase
    {
        private RenderThread RenderThread;

        private RenderScene RenderScene;
        private GL gl;
        public RendererBase(RenderThread rt)
        {
            RenderThread = rt;
            RenderScene = RenderThread.Scene;
            gl = rt.gl;
        }


        public void Render(double deltaTime)
        {
            
        }


    }
}
