using Silk.NET.OpenGLES;
using Spark.Engine.Assets;

namespace Spark.Engine.Render;

public class DeferredRenderer : IRenderer
{
    public GL gl { get; set; }

    public DeferredRenderer(GL GraphicsApi)
    {
        gl = GraphicsApi;
    }


    public void Render()
    {
        PreRender();



    }

    private void PreRender()
    {
        var list = Actions;
        Actions = TempActions;
        TempActions = Actions;
        foreach (var action in TempActions)
        {
            action.Invoke(this);
        }
        TempActions.Clear();
        foreach (var proxy in NeedRebuildProxies)
        {
            proxy.RebuildGpuResource(gl);
        }
        NeedRebuildProxies.Clear();
    }

    
    public T? GetProxy<T>(object obj) where T : class
    {
        if (ProxyDictonary.TryGetValue(obj, out var proxy))
        {
            if (proxy is T t)
                return t;
        }
        return null;
    }

    public RenderProxy? GetProxy(object obj)
    {
        if (ProxyDictonary.TryGetValue(obj, out var proxy))
        {
            return proxy;
        }
        return null;
    }

    public void AddNeedRebuildRenderResourceProxy(RenderProxy proxy)
    {
        if (proxy == null)
            return;
        if (NeedRebuildProxies.Contains(proxy))
        {
            NeedRebuildProxies.Add(proxy);
        }
    }

    private HashSet<RenderProxy> NeedRebuildProxies = [];

    private Dictionary<object, RenderProxy> ProxyDictonary = [];

    private List<Action<IRenderer>> Actions = new List<Action<IRenderer>>();

    private List<Action<IRenderer>> TempActions = new List<Action<IRenderer>>();

    public void AddRunOnRendererAction(Action<IRenderer> action)
    {
        Actions.Add(action);
    }
}
