using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Render.Renderer;

public interface IRenderer
{
    void Render(double DeltaTime);

    public RenderTarget CreateRenderTarget(int width, int height, uint GbufferNums);

    public RenderTarget CreateRenderTarget(int width, int height);

    public Shader CreateShader(string Path, List<string> Macros);

}
