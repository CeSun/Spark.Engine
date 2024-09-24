using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using Spark.Util;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public abstract class BaseRenderer
{
    public GL gl { get; set; }

    public HashSet<WorldProxy> RenderWorlds = [];
    public Dictionary<GCHandle, nint> AddRenderPropertiesDictionary { get; private set; } = [];
    public Dictionary<GCHandle, nint> SwapAddRenderPropertiesDictionary { get; private set; } = [];

    private Dictionary<GCHandle, AssetRenderProxy> ProxyDictonary = [];

    private List<Action<BaseRenderer>> Actions = [];

    private List<Action<BaseRenderer>> SwapActions = [];

    protected List<Pass> RenderPass = new List<Pass>();
    public BaseRenderer(GL GraphicsApi)
    {
        gl = GraphicsApi;
    }

    public void UpdateAssetProxy(IntPtr ptr)
    {
        ref var properties = ref  UnsafeHelper.AsRef<AssetProperties>(ptr);
        lock(AddRenderPropertiesDictionary)
        {
            if (AddRenderPropertiesDictionary.TryGetValue(properties.AssetWeakGCHandle, out var oldPtr))
            {
                ref var oldProperties = ref UnsafeHelper.AsRef<AssetProperties>(oldPtr);
                if (oldProperties.DestoryPointer != IntPtr.Zero)
                {
                    unsafe
                    {
                        ((delegate* unmanaged[Cdecl]<nint, void>)oldProperties.DestoryPointer)(oldPtr);
                    }
                }
                Marshal.FreeHGlobal(oldPtr);
            }
            AddRenderPropertiesDictionary[properties.AssetWeakGCHandle] = ptr;
        }
    }

    public virtual void Render()
    {
        CheckNullWeakGCHandle();
        PreRender();
        foreach(var world in RenderWorlds)
        {
            world.UpdateComponentProxies(this);
            RendererWorld(world);
        }
    }

    public abstract void RendererWorld(WorldProxy camera);

    public void PreRender()
    {
        lock (AddRenderPropertiesDictionary)
        {
            (AddRenderPropertiesDictionary, SwapAddRenderPropertiesDictionary) = (SwapAddRenderPropertiesDictionary, AddRenderPropertiesDictionary);
        }
        foreach (var (gchandle, ptr) in SwapAddRenderPropertiesDictionary)
        {
            ref var properties = ref UnsafeHelper.AsRef<AssetProperties>(ptr);
            if (GetProxy<AssetRenderProxy>(properties.AssetWeakGCHandle) == null)
            {
                if (properties.CreateProxyPointer == IntPtr.Zero)
                {
                    continue;
                }
                unsafe
                {
                    var proxyGCHandle = ((delegate* unmanaged[Cdecl]<GCHandle>)properties.CreateProxyPointer)();
                    var proxy = proxyGCHandle.Target;
                    proxyGCHandle.Free();
                    if (proxy is AssetRenderProxy assetRenderProxy)
                    {
                        ProxyDictonary.Add(properties.AssetWeakGCHandle, assetRenderProxy);
                    }
                }
            }

        }
        foreach (var (gchandle, ptr) in SwapAddRenderPropertiesDictionary)
        {
            UpdatePropertiesToProxy(ptr);
        }

        SwapAddRenderPropertiesDictionary.Clear();
        lock (Actions)
        {
            (Actions, SwapActions) = (SwapActions, Actions);
        }
        foreach (var action in SwapActions)
        {
            action(this);
        }
        SwapActions.Clear();
    }
    private void UpdatePropertiesToProxy(IntPtr ptr)
    {
        ref var  properties = ref UnsafeHelper.AsRef<AssetProperties>(ptr);
        var proxy = GetProxy<AssetRenderProxy>(properties.AssetWeakGCHandle);
        if (proxy == null)
            return;
        proxy.DestoryGpuResource(this);
        proxy.UpdatePropertiesAndRebuildGPUResource(this, ptr);
        if (properties.DestoryPointer == IntPtr.Zero) 
            Marshal.FreeHGlobal(ptr);
        unsafe
        {
            ((delegate* unmanaged[Cdecl]<IntPtr, void>)properties.DestoryPointer)(ptr);
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
            proxy.DestoryGpuResource(this);
        }
        GCHandles.ForEach(gchandle => ProxyDictonary.Remove(gchandle));
    }

    public T? GetProxy<T>(GCHandle gchandle) where T : class
    {
        if (gchandle == default)
            return null;
        if (ProxyDictonary.TryGetValue(gchandle, out var proxy))
        {
            return proxy as T;
        }
        return null;
    }

    public void AddRunOnRendererAction(Action<BaseRenderer> action)
    {
        lock (Actions)
        {
            Actions.Add(action);
        }
    }

    public void Destory()
    {
        foreach (var (gchandle, proxy) in ProxyDictonary)
        {
            proxy.DestoryGpuResource(this);
        }
        ProxyDictonary.Clear();
    }

}
