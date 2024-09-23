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
    public List<nint> AddRenderPropertiesList { get; private set; } = [];

    private List<CameraComponentProxy> cameraComponentProxies = [];
    public IReadOnlyList<CameraComponentProxy> CameraComponentProxies => cameraComponentProxies;

    private List<StaticMeshComponentProxy> staticMeshComponentProxies = new List<StaticMeshComponentProxy>();

    public List<StaticMeshComponentProxy> StaticMeshComponentProxies => staticMeshComponentProxies;

    public void UpdateComponentProxies(BaseRenderer renderer)
    {
        bool isAddCameraComponent = false;
        for (var i = 0; i < AddRenderPropertiesList.Count; i++)
        {
            var ptr = AddRenderPropertiesList[i];
            var componentProperties = UnsafeHelper.AsRef<PrimitiveComponentProperties>(ptr);
            if (componentProperties.ComponentState != WorldObjectState.Began && componentProperties.ComponentState != WorldObjectState.Registered)
            {
                if (_primitiveComponentProxyDictionary.TryGetValue(componentProperties.ComponentWeakGChandle, out var proxy) == true)
                {
                    _primitiveComponentProxyDictionary.Remove(componentProperties.ComponentWeakGChandle);
                    proxy.DestoryGpuResource(renderer);
                    if (proxy is CameraComponentProxy cameraComponentProxy)
                    {
                        cameraComponentProxies.Remove(cameraComponentProxy);
                    }
                    else if (proxy is StaticMeshComponentProxy staticMeshComponentProxy)
                    {
                        staticMeshComponentProxies.Remove(staticMeshComponentProxy);
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
                                else if (proxy is StaticMeshComponentProxy staticMeshComponentProxy)
                                {
                                    staticMeshComponentProxies.Add(staticMeshComponentProxy);
                                }
                            }
                        }
                    }
                    if (proxy != null)
                    {
                        proxy.UpdateProperties(ptr, renderer);
                        proxy.RebuildGpuResource(renderer);
                    }
                }
            }
            Marshal.FreeHGlobal(ptr);
        }
        AddRenderPropertiesList.Clear();
        if (isAddCameraComponent)
        {
            cameraComponentProxies.Sort();
        }
    }

    public void Destory(BaseRenderer renderer)
    {
        foreach(var item in AddRenderPropertiesList)
        {
            Marshal.FreeHGlobal(item);
        }
        AddRenderPropertiesList.Clear();
        foreach(var (_, proxy) in _primitiveComponentProxyDictionary)
        {
            proxy.DestoryGpuResource(renderer);
        }
        _primitiveComponentProxyDictionary.Clear();
        cameraComponentProxies.Clear();
    }
}




