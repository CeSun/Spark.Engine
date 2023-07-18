using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spark.Engine.Render.Proxy;
using System.Drawing;
using Silk.NET.OpenGLES;
namespace Spark.Engine.Render
{
    public class RenderScene
    {
        RenderThread RenderThread;
        public RenderScene(RenderThread renderThread)
        {
            RenderThread = renderThread;
        }
        public List<StaticMeshProxy> StaticMeshProxy = new List<StaticMeshProxy>();

        public void Render(double DeltaTime)
        {
            RenderThread.gl.ClearColor(Color.DeepPink);
            RenderThread.gl.Clear(ClearBufferMask.ColorBufferBit);

        }
    }
}
