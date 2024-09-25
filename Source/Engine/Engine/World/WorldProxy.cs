using Spark.Core.Actors;
using Spark.Core.Components;
using Spark.Core.Render;
using Spark.Util;
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

    private List<SkeletalMeshComponentProxy> skeletalMeshComponentProxies = new List<SkeletalMeshComponentProxy>();
    public List<SkeletalMeshComponentProxy> SkeletalComponentProxies => skeletalMeshComponentProxies;

    private List<DirectionalLightComponentProxy> directionalLightComponentProxies = new List<DirectionalLightComponentProxy>();
    public IReadOnlyList<DirectionalLightComponentProxy> DirectionalLightComponentProxies => directionalLightComponentProxies;

    private List<PointLightComponentProxy> pointLightComponentProxies = new List<PointLightComponentProxy>();
    public IReadOnlyList<PointLightComponentProxy> PointLightComponentProxies => pointLightComponentProxies;

    private List<SpotLightComponentProxy> spotLightComponentProxies = new List<SpotLightComponentProxy>();
    public IReadOnlyList<SpotLightComponentProxy> SpotLightComponentProxies => spotLightComponentProxies;

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
                    switch (proxy)
                    {
                        case CameraComponentProxy cameraComponentProxy:
                            cameraComponentProxies.Remove(cameraComponentProxy);
                            isAddCameraComponent = true;
                            break;
                        case StaticMeshComponentProxy staticMeshComponentProxy:
                            staticMeshComponentProxies.Remove(staticMeshComponentProxy);
                            break;
                        case SkeletalMeshComponentProxy skeletalMeshComponentProxy:
                            skeletalMeshComponentProxies.Remove(skeletalMeshComponentProxy);
                            break;
                        case DirectionalLightComponentProxy directionalLightComponentProxy:
                            directionalLightComponentProxies.Remove(directionalLightComponentProxy);
                            break;
                        case PointLightComponentProxy pointLightComponentProxy:
                            pointLightComponentProxies.Remove(pointLightComponentProxy);
                            break;
                        case SpotLightComponentProxy spotLightComponentProxy:
                            spotLightComponentProxies.Remove(spotLightComponentProxy);
                            break;
                        default:
                            break;
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
                                switch (proxy)
                                {
                                    case CameraComponentProxy cameraComponentProxy:
                                        cameraComponentProxies.Add(cameraComponentProxy);
                                        isAddCameraComponent = true;
                                        break;
                                    case StaticMeshComponentProxy staticMeshComponentProxy:
                                        staticMeshComponentProxies.Add(staticMeshComponentProxy);
                                        break;
                                    case SkeletalMeshComponentProxy skeletalMeshComponentProxy:
                                        skeletalMeshComponentProxies.Add(skeletalMeshComponentProxy);
                                        break;
                                    case DirectionalLightComponentProxy directionalLightComponentProxy:
                                        directionalLightComponentProxies.Add(directionalLightComponentProxy);
                                        break;
                                    case PointLightComponentProxy pointLightComponentProxy:
                                        pointLightComponentProxies.Add(pointLightComponentProxy);
                                        break;
                                    case SpotLightComponentProxy spotLightComponentProxy:
                                        spotLightComponentProxies.Add(spotLightComponentProxy);
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
                if (proxy != null)
                {
                    proxy.UpdateProperties(ptr, renderer);
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




