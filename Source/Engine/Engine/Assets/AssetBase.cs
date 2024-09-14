﻿using Silk.NET.OpenGLES;
using Spark.Engine.Render;
using System.Runtime.InteropServices;

namespace Spark.Engine.Assets;

public abstract class AssetBase
{
    public GCHandle WeakGCHandle { get; private set; }
    public AssetBase()
    {
        WeakGCHandle = GCHandle.Alloc(this, GCHandleType.WeakTrackResurrection);
    }


    public HashSet<IRenderer> RenderHashSet = [];

    public void RequestRendererRebuildGpuResource()
    {
        RunOnRenderer(renderer => renderer.AddNeedRebuildRenderResourceProxy(renderer.GetProxy(this)!));
    }
    public void RunOnRenderer(Action<IRenderer> action)
    {
        foreach(var renderer in RenderHashSet)
        {
            renderer.AddRunOnRendererAction(action);
        }
    }
    public virtual Func<IRenderer, RenderProxy>? GetGenerateProxyDelegate()
    {
        return null ;
    }
    public virtual void PostProxyToRenderer(IRenderer renderer)
    {
        var fun = GetGenerateProxyDelegate();
        if (fun == null)
            return;
        if (RenderHashSet.Contains(renderer) == false)
        {
            RenderHashSet.Add(renderer);
            RunOnRenderer(render =>
            {
                var proxy = fun(render);
                render.AddProxy(this, proxy);
            });
        }
    }
}


public class RenderProxy
{

    public virtual void RebuildGpuResource(GL gl)
    {

    }


    public virtual void DestoryGpuResource(GL gl) 
    { 
    }
}