using Silk.NET.OpenGLES;
using Spark.Engine.Assets;

namespace Spark.Engine.Render;

public interface IRenderer
{
    GL gl { get; set; }
    T? GetProxy<T>(object obj) where T: class;
    RenderProxy? GetProxy(object obj);
    RenderProxy? AddProxy(object obj, RenderProxy renderProxy);
    void AddNeedRebuildRenderResourceProxy(RenderProxy proxy);
    void AddRunOnRendererAction(Action<IRenderer> action);
    void Render();

}
