using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public abstract class BaseRenderer
{
    public HashSet<WorldProxy> RenderWorlds = [];

    public BaseRenderer(GL GraphicsApi)
    {
        gl = GraphicsApi;
    }

    public GL gl { get; set; }

    public virtual void Render()
    {
        CheckNullWeakGCHandle();
        PreRender();
        foreach(var world in RenderWorlds)
        {
            world.UpdateComponentProxies(this);
            foreach(var camera in world.CameraComponentProxies)
            {
                if (camera.RenderTarget == null)
                    return;
                using (camera.RenderTarget)
                {
                    ClearBufferMask mask = ClearBufferMask.None;
                    if ((camera.ClearFlag & CameraClearFlag.Depth) != 0)
                    {
                        mask |= ClearBufferMask.DepthBufferBit;
                    }
                    if ((camera.ClearFlag & CameraClearFlag.Color) != 0)
                    {
                        mask |= ClearBufferMask.ColorBufferBit;
                        gl.ClearColor(camera.ClearColor.X, camera.ClearColor.Y, camera.ClearColor.Z, camera.ClearColor.W);
                    }
                    gl.Clear(mask);
                }
                RendererWorld(camera);
            }
        }
    }

    public abstract void RendererWorld(CameraComponentProxy camera);


    public void PreRender()
    {
        var list = Actions;
        Actions = TempActions;
        TempActions = list;
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


    public T? GetProxy<T>(AssetBase obj) where T : class
    {
        return GetProxy(obj) as T;
    }

    public T? GetProxy<T>(GCHandle gchandle) where T : class
    {
        return GetProxy(gchandle) as T;
    }
    public AssetRenderProxy? GetProxy(GCHandle gchandle)
    {
        if (gchandle == default)
            return null;
        if (ProxyDictonary.TryGetValue(gchandle, out var proxy))
        {
            return proxy;
        }
        return null;
    }

    public AssetRenderProxy? GetProxy(AssetBase obj)
    {
        if (obj == null)
            return null;
        if (ProxyDictonary.TryGetValue(obj.WeakGCHandle, out var proxy))
        {
            return proxy;
        }
        return null;
    }

    public void AddProxy(AssetBase obj, AssetRenderProxy renderProxy)
    {
        if (obj == null)
            return;
        if (ProxyDictonary.ContainsKey(obj.WeakGCHandle))
            return;
        ProxyDictonary.Add(obj.WeakGCHandle, renderProxy);
    }
    public void AddNeedRebuildRenderResourceProxy(AssetRenderProxy proxy)
    {
        if (proxy == null)
            return;
        if (NeedRebuildProxies.Contains(proxy) == false)
        {
            NeedRebuildProxies.Add(proxy);
        }
    }

    private void CheckNullWeakGCHandle()
    {
        List<GCHandle> GCHandles = [];
        foreach (var (gchandle, proxy) in ProxyDictonary)
        {
            if (gchandle.Target != null)
                continue;
            GCHandles.Add(gchandle);
            proxy.DestoryGpuResource(gl);
        }
        GCHandles.ForEach(gchandle => ProxyDictonary.Remove(gchandle));
    }

    private HashSet<AssetRenderProxy> NeedRebuildProxies = [];

    private Dictionary<GCHandle, AssetRenderProxy> ProxyDictonary = [];

    private List<Action<BaseRenderer>> Actions = new List<Action<BaseRenderer>>();

    private List<Action<BaseRenderer>> TempActions = new List<Action<BaseRenderer>>();

    public void AddRunOnRendererAction(Action<BaseRenderer> action)
    {
        Actions.Add(action);
    }

    public void Destory()
    {
        foreach (var (gchandle, proxy) in ProxyDictonary)
        {
            proxy.DestoryGpuResource(gl);
        }
        ProxyDictonary.Clear();
    }

}
