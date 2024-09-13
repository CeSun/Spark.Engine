using Silk.NET.OpenGLES;
using Spark.Engine.Assets;

namespace Spark.Engine.Render;

public interface IRenderer
{
    GL gl { get; set; }
    T? GetProxy<T>(object obj) where T: class;
    RenderProxy? GetProxy(object obj);
    void AddNeedRebuildRenderResourceProxy(RenderProxy proxy);
    void Render();

}
