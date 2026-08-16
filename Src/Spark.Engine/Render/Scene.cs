using Spark.Engine.Render.Resources;

namespace Spark.Engine.Render;

/// <summary>
/// 逻辑线程侧的渲染场景注册表（对应 UE 的 FScene）：持有所有需要进入渲染线程的
/// <see cref="SceneProxy"/>，分配稳定 ProxyId，每帧把活跃集合序列化为 <see cref="SceneSnapshot"/>。
/// 与 World 的 Actor 图解耦：任何要渲染/照明的对象在此注册，而非每帧遍历组件临时拼装。
/// </summary>
public sealed class Scene
{
    private int _nextProxyId;
    private readonly Dictionary<int, SceneProxy> _proxies = new();

    public int ProxyCount => _proxies.Count;

    /// <summary>资源管理器（自动上传 + GPU 表示延迟释放），由引擎/组合根接线。</summary>
    public ResourceManager? ResourceManager { get; set; }

    /// <summary>注册代理并分配全局唯一 ProxyId。</summary>
    public T Register<T>(T proxy) where T : SceneProxy
    {
        proxy.ProxyId = ++_nextProxyId;
        _proxies.Add(proxy.ProxyId, proxy);
        return proxy;
    }

    /// <summary>注销代理（渲染线程经生命周期 diff 延迟释放其渲染侧状态）。</summary>
    public void Unregister(int proxyId) => _proxies.Remove(proxyId);

    /// <summary>把全部活跃代理写入本帧快照（逻辑线程独占调用）。</summary>
    public void Capture(SceneSnapshot snapshot)
    {
        foreach (var proxy in _proxies.Values)
            proxy.Capture(snapshot);
    }
}
