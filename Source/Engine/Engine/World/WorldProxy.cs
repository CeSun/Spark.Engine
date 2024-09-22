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
                        proxy.UpdateProperties(componentProperties, renderer);
                        proxy.ReBuild(renderer.gl);
                    }
                }
            }
            ptr.Free();
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
            var p = item;
            p.Free();
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


public class PrimitiveComponentProxy
{
    public Vector3 Forward;
    public Vector3 Right;
    public Vector3 Up;
    public Quaternion WorldRotation;
    public Vector3 WorldLocation;
    public Vector3 WorldScale;
    public bool Hidden { get; set; }
    public bool CastShadow { get; set; }
    public Matrix4x4 Trasnform { get; set; }
    public virtual void UpdateProperties(in PrimitiveComponentProperties properties, BaseRenderer renderer)
    {
        Hidden = properties.Hidden;
        CastShadow = properties.CastShadow;
        Trasnform = properties.WorldTransform;

        WorldRotation = Trasnform.Rotation();
        WorldLocation = Trasnform.Translation;
        WorldScale = Trasnform.Scale();

        Forward = Vector3.Transform(new Vector3(0, 0, -1), WorldRotation);
        Right = Vector3.Transform(new Vector3(1, 0, 0), WorldRotation);
        Up = Vector3.Transform(new Vector3(0, 1, 0), WorldRotation);

        UpdateSubComponentProxy(properties.CustomProperties, renderer);
    }
    public virtual void UpdateSubComponentProxy(nint pointer, BaseRenderer renderer)
    {

    }


    public virtual void ReBuild(GL gl)
    {

    }

    public virtual void Destory(GL gl)
    {

    }
}

