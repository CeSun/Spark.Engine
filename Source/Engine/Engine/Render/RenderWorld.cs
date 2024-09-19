using Spark.Core.Assets;
using Spark.Core.Components;
using System.Numerics;

namespace Spark.Core.Render;

public class RenderWorld
{
    private Dictionary<PrimitiveComponent, PrimitiveComponentProxy> _primitiveComponentProxyDictionary = [];

    public IReadOnlyDictionary<PrimitiveComponent, PrimitiveComponentProxy> PrimitiveComponentProxyDictionary => _primitiveComponentProxyDictionary;

    public Queue<nint> RenderPropertiesQueue { get; private set; } = [];

    public void AddPrimitiveComponentProxy(PrimitiveComponent component, PrimitiveComponentProxy proxy)
    {
        if (_primitiveComponentProxyDictionary.ContainsKey(component))
            return;
        _primitiveComponentProxyDictionary.Add(component, proxy);
    }

    public void RemovePrimitiveComponentProxy(PrimitiveComponent component)
    {
        if (_primitiveComponentProxyDictionary.ContainsKey(component) == false) 
            return;
        _primitiveComponentProxyDictionary.Remove(component);
    }

    public T? GetProxy<T>(PrimitiveComponent primitiveComponent) where T : PrimitiveComponentProxy
    {
        var proxy = GetProxy(primitiveComponent);
        if (proxy == null) 
            return null;
        return proxy as T;

    }

    public PrimitiveComponentProxy? GetProxy(PrimitiveComponent component)
    {
        if (_primitiveComponentProxyDictionary.TryGetValue(component, out var proxy))
            return proxy;
        return null;
    }
}


public class PrimitiveComponentProxy
{
    public bool Hidden {  get; set; }
    public bool CastShadow { get; set; }
    public Matrix4x4 Trasnform { get; set; }

    public virtual void UpdateProperties(in PrimitiveComponentProperties properties, IRenderer renderer)
    {
        Hidden = properties.Hidden;
        CastShadow = properties.CastShadow;
        Trasnform = properties.WorldTransform;
        UpdateSubComponentProxy(properties.CustomProperties, renderer);
    }

    public virtual void UpdateSubComponentProxy(IntPtr pointer, IRenderer renderer)
    {

    }
}

