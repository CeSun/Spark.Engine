using Silk.NET.OpenGLES;
using Spark.Engine.Render;

namespace Spark.Engine.Assets;

public abstract class AssetBase
{
    public event Action<Action<IRenderer>>? OnAssetModify;

    public void RequestRendererRebuild()
    {
        AssetModify(renderer => renderer.AddNeedRebuildRenderResourceProxy(renderer.GetProxy(this)!));
    }
    public void AssetModify(Action<IRenderer> action)
    {
        OnAssetModify?.Invoke(action);
    }
}


public class RenderProxy
{

    public virtual void RebuildGpuResource(GL gl)
    {

    }

}