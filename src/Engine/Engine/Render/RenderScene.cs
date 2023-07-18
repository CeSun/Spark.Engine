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

        public void AddPrimitive(PrimitiveProxy primitiveProxy)
        {
            if (primitiveProxy is StaticMeshProxy staticMeshProxy)
            {
                StaticMeshProxys.Add(staticMeshProxy);
            }
            else
            {
                PrimitiveProxys.Add(primitiveProxy);
            }

               
        }

        public void RemovePrimitive(PrimitiveProxy primitiveProxy)
        {
            if (primitiveProxy is StaticMeshProxy staticMeshProxy)
            {
                StaticMeshProxys.Remove(staticMeshProxy);
            }
            else
            {
                PrimitiveProxys.Remove(primitiveProxy);
            }
        }

        private List<StaticMeshProxy> StaticMeshProxys = new List<StaticMeshProxy>();
        private List<PrimitiveProxy> PrimitiveProxys = new List<PrimitiveProxy>();
        public void Render(double DeltaTime)
        {
            RenderThread.gl.ClearColor(Color.DeepPink);
            RenderThread.gl.Clear(ClearBufferMask.ColorBufferBit);

        }
    }
}
