using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public interface IRenderer
{
    GL gl { get; set; }
    T? GetProxy<T>(AssetBase obj) where T: class;
    T? GetProxy<T>(GCHandle gchandle) where T : class;
    RenderProxy? GetProxy(AssetBase obj);
    RenderProxy? GetProxy(GCHandle gchandle);
    void AddProxy(AssetBase obj, RenderProxy renderProxy);
    void AddNeedRebuildRenderResourceProxy(RenderProxy proxy);
    void AddRunOnRendererAction(Action<IRenderer> action);
    void Render(RenderWorld renderWorld);
    void Destory();

}
