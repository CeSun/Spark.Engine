using Silk.NET.OpenGLES;
using Spark.Core.Actors;
using Spark.Core.Assets;
using Spark.Core.Components;
using Spark.Util;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class RenderWorld
{
    private Dictionary<GCHandle, PrimitiveComponentProxy> _primitiveComponentProxyDictionary = [];

    public IReadOnlyDictionary<GCHandle, PrimitiveComponentProxy> PrimitiveComponentProxyDictionary => _primitiveComponentProxyDictionary;

    public List<nint> RenderPropertiesQueue { get; private set; } = [];

    
    public void UpdateComponentProxies(BaseRenderer renderer)
    {
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
                }
            }
            else
            {
                if (_primitiveComponentProxyDictionary.TryGetValue(componentProperties.ComponentWeakGChandle, out var proxy) == false)
                {
                    unsafe
                    {
                        delegate* unmanaged[Cdecl]<GCHandle>  p = (delegate* unmanaged[Cdecl]<GCHandle>)componentProperties.CreateProxyObject;
                        if (p != null)
                        {
                            var proxyGcHandle = p();
                            proxy = proxyGcHandle.Target as PrimitiveComponentProxy;
                            proxyGcHandle.Free();
                            if (proxy != null)
                            {
                                _primitiveComponentProxyDictionary.Add(componentProperties.ComponentWeakGChandle, proxy);
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
            UnsafeHelper.Free(ref ptr);
        }
        RenderPropertiesQueue.Clear();
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
    public bool Hidden {  get; set; }
    public bool CastShadow { get; set; }
    public Matrix4x4 Trasnform { get; set; }
    public virtual void UpdateProperties(in PrimitiveComponentProperties properties, IRenderer renderer)
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
    public virtual void UpdateSubComponentProxy(IntPtr pointer, IRenderer renderer)
    {

    }


    public virtual void ReBuild(GL gl)
    {

    }

    public virtual void Destory(GL gl)
    {

    }
}

