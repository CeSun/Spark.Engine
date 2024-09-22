using Silk.NET.OpenGLES;
using Spark.Core.Actors;
using Spark.Core.Components;
using Spark.Core.Render;
using Spark.Util;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Spark.Core;

public class WorldProxy
{
    private Dictionary<GCHandle, PrimitiveComponentProxy> _primitiveComponentProxyDictionary = [];
    public IReadOnlyDictionary<GCHandle, PrimitiveComponentProxy> PrimitiveComponentProxyDictionary => _primitiveComponentProxyDictionary;
    public List<nint> RenderPropertiesQueue { get; private set; } = [];

    private List<CameraComponentProxy> cameraComponentProxies = [];
    public IReadOnlyList<CameraComponentProxy> CameraComponentProxies => cameraComponentProxies;

    public void UpdateComponentProxies(BaseRenderer renderer)
    {
        bool isAddCameraComponent = false;
        for (var i = 0; i < RenderPropertiesQueue.Count; i++)
        {
            var ptr = RenderPropertiesQueue[i];
            var componentProperties = UnsafeHelper.AsRef<PrimitiveComponentProperties>(ptr);
            if (componentProperties.ComponentState != WorldObjectState.Began && componentProperties.ComponentState != WorldObjectState.Registered)
            {
                if (_primitiveComponentProxyDictionary.TryGetValue(componentProperties.ComponentWeakGChandle, out var proxy) == true)
                {
                    _primitiveComponentProxyDictionary.Remove(componentProperties.ComponentWeakGChandle);
                    proxy.Destory(renderer.gl);
                    if (proxy is CameraComponentProxy cameraComponentProxy)
                    {
                        cameraComponentProxies.Remove(cameraComponentProxy);
                    }
                }
            }
            else
            {
                if (_primitiveComponentProxyDictionary.TryGetValue(componentProperties.ComponentWeakGChandle, out var proxy) == false)
                {
                    unsafe
                    {
                        delegate* unmanaged[Cdecl]<GCHandle> p = (delegate* unmanaged[Cdecl]<GCHandle>)componentProperties.CreateProxyObject;
                        if (p != null)
                        {
                            var proxyGcHandle = p();
                            proxy = proxyGcHandle.Target as PrimitiveComponentProxy;
                            proxyGcHandle.Free();
                            if (proxy != null)
                            {
                                _primitiveComponentProxyDictionary.Add(componentProperties.ComponentWeakGChandle, proxy);
                                if (proxy is  CameraComponentProxy cameraComponentProxy)
                                {
                                    cameraComponentProxies.Add(cameraComponentProxy);
                                    isAddCameraComponent = true;
                                }
                            }
                        }
                    }
                    if (proxy != null)
                    {
                        proxy.UpdateProperties(ptr, renderer);
                        proxy.ReBuild(renderer.gl);
                    }
                }
            }
            Marshal.FreeHGlobal(ptr);
        }
        RenderPropertiesQueue.Clear();
        if (isAddCameraComponent)
        {
            cameraComponentProxies.Sort();
        }
    }

    public void Destory(BaseRenderer renderer)
    {
        foreach(var item in RenderPropertiesQueue)
        {
            Marshal.FreeHGlobal(item);
        }
        RenderPropertiesQueue.Clear();
        foreach(var (_, proxy) in _primitiveComponentProxyDictionary)
        {
            proxy.Destory(renderer.gl);
        }
        _primitiveComponentProxyDictionary.Clear();
        cameraComponentProxies.Clear();

    }
}




