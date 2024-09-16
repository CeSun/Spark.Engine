﻿using Silk.NET.OpenGLES;
using Spark.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Render;

public abstract class BaseRenderer : IRenderer
{
    public BaseRenderer(GL GraphicsApi)
    {
        gl = GraphicsApi;
    }

    public GL gl { get; set; }


    public virtual void Render(RenderWorld renderWorld)
    {
        CheckNullWeakGCHandle();
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


    public T? GetProxy<T>(AssetBase obj) where T : class
    {
        return GetProxy(obj) as T;
    }

    public RenderProxy? GetProxy(AssetBase obj)
    {
        if (obj == null)
            return null;
        if (ProxyDictonary.TryGetValue(obj.WeakGCHandle, out var proxy))
        {
            return proxy;
        }
        return null;
    }

    public void AddProxy(AssetBase obj, RenderProxy renderProxy)
    {
        if (obj == null)
            return;
        if (ProxyDictonary.ContainsKey(obj.WeakGCHandle))
            return;
        ProxyDictonary.Add(obj.WeakGCHandle, renderProxy);
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

    private HashSet<RenderProxy> NeedRebuildProxies = [];

    private Dictionary<GCHandle, RenderProxy> ProxyDictonary = [];

    private List<Action<IRenderer>> Actions = new List<Action<IRenderer>>();

    private List<Action<IRenderer>> TempActions = new List<Action<IRenderer>>();

    public void AddRunOnRendererAction(Action<IRenderer> action)
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