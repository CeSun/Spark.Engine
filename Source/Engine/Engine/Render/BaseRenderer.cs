﻿using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using Spark.Util;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public abstract class BaseRenderer
{
    public GL gl => Engine.GraphicsApi!;
    public Engine Engine { get; private set; }

    public RectangleMesh RectangleMesh { get; private set; }
    public ShaderTemplate? GetShaderTemplate(string path)
    {
        if (_shaderCacheDictonary.TryGetValue(path, out var shaderTemplate))
            return shaderTemplate;
        return null;
    }

    public void SetShaderTemplate(string path, ShaderTemplate shaderTemplate)
    {
        _shaderCacheDictonary[path] = shaderTemplate;
    }

    private Dictionary<string, ShaderTemplate> _shaderCacheDictonary = [];

    public HashSet<WorldProxy> RenderWorlds = [];
    public Dictionary<GCHandle, nint> AddRenderPropertiesDictionary { get; private set; } = [];
    public Dictionary<GCHandle, nint> SwapAddRenderPropertiesDictionary { get; private set; } = [];

    private Dictionary<GCHandle, AssetRenderProxy> ProxyDictonary = [];

    private List<Action<BaseRenderer>> Actions = [];

    private List<Action<BaseRenderer>> SwapActions = [];

    protected List<Pass> RenderPass = new List<Pass>();
    public BaseRenderer(Engine engine)
    {
        Engine = engine;
        RectangleMesh = new RectangleMesh();
        RectangleMesh.InitRender(this);
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


public static class RendererHelper
{
    public static unsafe void Draw(this BaseRenderer renderer, ElementProxy element)
    {
        renderer.gl.BindVertexArray(element.VertexArrayObjectIndex);
        renderer.gl.DrawElements(PrimitiveType.Triangles, (uint)element.IndicesLength, DrawElementsType.UnsignedInt, (void*)0);
    }
}

public class RectangleMesh
{
    uint vao;
    uint vbo;
    uint ebo;
    public unsafe void InitRender(BaseRenderer renderer)
    {
        float[] vertices = [
            -1, 1, 0, 0, 1,
            -1, -1, 0, 0, 0,
            1, -1, 0, 1, 0,
            1, 1, 0, 1, 1];
        uint[] indices =
        [
            0, 1, 2, 2, 3,0
        ];
        var gl = renderer.gl;
        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();
        gl.BindVertexArray(vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
        fixed (float* p = vertices)
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), p, GLEnum.StaticDraw);
        }
        gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
        fixed (uint* p = indices)
        {
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), p, GLEnum.StaticDraw);
        }
        // Location
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(float) * 5, (void*)0);
        // TexCoord
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)sizeof(float) * 5, (void*)sizeof(Vector3));
        gl.BindVertexArray(0);

    }


    public unsafe void Draw(BaseRenderer renderer)
    {
        renderer.gl.BindVertexArray(vao);
        renderer.gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
    }
}