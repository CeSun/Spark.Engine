using Silk.NET.OpenGLES;
using Spark.Engine.Render;

namespace Spark.Engine.Assets;

public abstract class AssetBase
{
    public event Action<Action<IRenderer>>? OnAssetModify;

    public void RequestRendererRebuild()
    {
        RunOnRenderer(renderer => renderer.AddNeedRebuildRenderResourceProxy(renderer.GetProxy(this)!));
    }
    public void RunOnRenderer(Action<IRenderer> action)
    {
        OnAssetModify?.Invoke(action);
    }

    public virtual void PostProxyToRenderer()
    {

    }
}


public class RenderProxy
{

    public virtual void RebuildGpuResource(GL gl)
    {

    }

}