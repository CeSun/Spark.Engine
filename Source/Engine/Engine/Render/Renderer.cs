using Silk.NET.OpenGLES;
using Spark.Engine.Assets;

namespace Spark.Engine.Render;

public interface IRenderer
{
    GL gl { get; set; }
    T? GetProxy<T>(AssetBase obj) where T: class;
    RenderProxy? GetProxy(AssetBase obj);
    void AddProxy(AssetBase obj, RenderProxy renderProxy);
    void AddNeedRebuildRenderResourceProxy(RenderProxy proxy);
    void AddRunOnRendererAction(Action<IRenderer> action);
    void Render();

}
