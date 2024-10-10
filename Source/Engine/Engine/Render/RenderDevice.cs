using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using Spark.Core.Shapes;
using Spark.Util;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Spark.Core.Render;

public class RenderDevice
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

    private List<Action<RenderDevice>> Actions = [];

    private List<Action<RenderDevice>> SwapActions = [];

    protected List<Pass> RenderPass = new List<Pass>();
    public RenderDevice(Engine engine)
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
            world.Render(this);
        }
    }

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

    public void AddRunOnRendererAction(Action<RenderDevice> action)
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


public class RectangleMesh
{
    public uint vao;
    public uint vbo;
    public uint ebo;
    public unsafe void InitRender(RenderDevice renderer)
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
}

public static class DrawMeshHelper
{

    public static unsafe void Draw(this GL gl, RectangleMesh mesh)
    {
        gl.BindVertexArray(mesh.vao);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
    }


    public static void DrawElementDepth(this GL gl, ShaderTemplate shader, ElementProxy element, Matrix4x4 Model, Matrix4x4 View, Matrix4x4 Projection)
    {
        if (element.Material == null)
            return;
        shader.SetMatrix("model", Model);
        shader.SetMatrix("view", View);
        shader.SetMatrix("projection", Projection);
        if (element.Material.BlendMode == BlendMode.Masked)
        {
            if (element.Material.Textures.TryGetValue("BaseColor", out var texture))
                shader.SetTexture("Texture_BaseColor", 1, texture);
        }
        gl.Draw(element);
    }


    public static void DrawElement(this GL gl, ShaderTemplate shader, ElementProxy element, Matrix4x4 Model, Matrix4x4 View, Matrix4x4 Projection)
    {
        if (element.Material == null)
            return;
        shader.SetMatrix("model", Model);
        shader.SetMatrix("view", View);
        shader.SetMatrix("projection", Projection);
        if (element.Material.Textures.TryGetValue("BaseColor", out var textureBaseColor))
            shader.SetTexture("Texture_BaseColor", 1, textureBaseColor);
        if (element.Material.Textures.TryGetValue("Normal", out var textureNormal))
            shader.SetTexture("Texture_Normal", 2, textureNormal);
        if (element.Material.Textures.TryGetValue("Metalness", out var textureMetalness))
            shader.SetTexture("Texture_Metalness", 3, textureMetalness);
        if (element.Material.Textures.TryGetValue("Roughness", out var textureRoughness))
            shader.SetTexture("Texture_Roughness", 4, textureRoughness);

        if (element.Material.Textures.TryGetValue("Occlusion", out var textureOcclusion))
            shader.SetTexture("Texture_Occlusion", 5, textureOcclusion);
        gl.Draw(element);
    }


    public static void BatchDrawStaticMesh(this GL gl, Span<StaticMeshComponentProxy> staticMeshComponentProxies, Matrix4x4 View, Matrix4x4 Projection, bool OnlyDepth, bool ingoreMasked = false)
    {
        Span<string> Macros = ["_NOTHING_"];
        Span<string> MaskedMacros = ["_BLENDMODE_MASKED_"];
        if (OnlyDepth)
        {
            MaskedMacros = [.. MaskedMacros, "_DEPTH_ONLY_"];
            Macros = ["_DEPTH_ONLY_"];
        }

        Span<Vector3> Points = stackalloc Vector3[8];
        foreach (var staticmesh in staticMeshComponentProxies)
        {
            if (staticmesh.StaticMeshProxy == null)
                continue;
            if (staticmesh.Hidden)
                continue;
            if (staticmesh.StaticMeshProxy.StaticMeshLods.Count <= 0)
                continue;
            StaticMeshLodProxy lod = staticmesh.StaticMeshProxy.StaticMeshLods[0];
            // 计算lod
            if (staticmesh.StaticMeshProxy.StaticMeshLods.Count > 1)
            {
                var mat = staticmesh.Trasnform * View * Projection;
                staticmesh.StaticMeshProxy.Box.GetPoints(Points);
                Box clipSpaceBox = new Box();
                bool first = true;
                foreach(var point in Points)
                {
                    var p = Vector4.Transform(point, mat);
                    if (p.X > p.W)
                        p.X = p.W;
                    if (p.X < - p.W)
                        p.X = - p.W;
                    if (p.Y > p.W)
                        p.Y = p.W;
                    if (p.Y < -p.W)
                        p.Y = -p.W;
                    if (p.Z > p.W)
                        p.Z = p.W;
                    if (p.Z < -p.W)
                        p.Z = -p.W;

                    p = p / p.W;
                    var clipSpacePoint = new Vector3(p.X, p.Y, p.Z);
                    if (first)
                    {
                        clipSpaceBox.Max = clipSpacePoint;
                        clipSpaceBox.Min = clipSpaceBox.Max;
                        first = false;
                    }
                    else
                    {
                        clipSpaceBox += clipSpacePoint;
                    }
                }
                var delta = clipSpaceBox.Max - clipSpaceBox.Min;
                var area = delta.X * delta.Y;
                var scale = Math.Clamp(area / 4, 0, 1);
                if (scale == 0)
                    continue;
                var lodIndex = (int)(scale / (1.0f / staticmesh.StaticMeshProxy.StaticMeshLods.Count));
                if (lodIndex >= staticmesh.StaticMeshProxy.StaticMeshLods.Count)
                    lodIndex = staticmesh.StaticMeshProxy.StaticMeshLods.Count - 1;
                lodIndex = 3 - lodIndex;
                lod = staticmesh.StaticMeshProxy.StaticMeshLods[lodIndex];
                Console.WriteLine(lodIndex);
            }

            foreach (var mesh in lod.Elements)
            {
                if (mesh.Material == null)
                    continue;
                if (mesh.Material.ShaderTemplate == null)
                    continue;
                var shader = mesh.Material.ShaderTemplate;
                if (ingoreMasked == false && mesh.Material.BlendMode == BlendMode.Masked)
                    shader.Use(gl, MaskedMacros);
                else
                    shader.Use(gl, Macros);
                if (OnlyDepth)
                    gl.DrawElementDepth(shader, mesh, staticmesh.Trasnform, View, Projection);
                else
                    gl.DrawElement(shader, mesh, staticmesh.Trasnform, View, Projection);
                shader.Dispose();
            }
        }
    }

    public static void BatchDrawSkeletalMesh(this GL gl, Span<SkeletalMeshComponentProxy> skeletalMeshComponentProxes, Matrix4x4 View, Matrix4x4 Projection, bool OnlyDepth, bool ingoreMasked = false)
    {
        Span<string> Macros = ["_SKELETAL_MESH_"];
        Span<string> MaskedMacros = ["_BLENDMODE_MASKED_", .. Macros];
        if (OnlyDepth)
        {
            MaskedMacros = [.. MaskedMacros, "_DEPTH_ONLY_"];
            Macros = [.. Macros, "_DEPTH_ONLY_"];
        }

        foreach (var skeletalMesh in skeletalMeshComponentProxes)
        {
            if (skeletalMesh.SkeletalMeshProxy == null)
                continue;
            if (skeletalMesh.Hidden)
                continue;
            foreach (var mesh in skeletalMesh.SkeletalMeshProxy.Elements)
            {
                if (mesh.Material == null)
                    continue;
                if (mesh.Material.ShaderTemplate == null)
                    continue;
                var shader = mesh.Material.ShaderTemplate;
                if (ingoreMasked == false && mesh.Material.BlendMode == BlendMode.Masked)
                    shader.Use(gl, MaskedMacros);
                else
                    shader.Use(gl, Macros);
                for (int i = 0; i < 100; i++)
                {
                    shader.SetMatrix($"animTransform[{i}]", skeletalMesh.AnimBuffer[i]);
                }
                if (OnlyDepth)
                    gl.DrawElementDepth(shader, mesh, skeletalMesh.Trasnform, View, Projection);
                else
                    gl.DrawElement(shader, mesh, skeletalMesh.Trasnform, View, Projection);
                shader.Dispose();
            }
        }
    }
    public static unsafe void Draw(this GL gl, ElementProxy element)
    {
        gl.BindVertexArray(element.VertexArrayObjectIndex);
        gl.DrawElements(PrimitiveType.Triangles, (uint)element.IndicesLength, DrawElementsType.UnsignedInt, (void*)0);
    }

    public static void ResetPassState(this GL gl, Pass pass)
    {
        if (pass.ZTest)
        {
            gl.Enable(GLEnum.DepthTest);
            gl.DepthFunc(pass.ZTestFunction);
            if (pass.ZWrite)
            {
                gl.DepthMask(true);
            }
            else
            {
                gl.DepthMask(false);
            }
        }
        else
        {
            gl.Disable(GLEnum.DepthTest);
        }
        if (pass.CullFace)
        {
            gl.Enable(GLEnum.CullFace);
            gl.CullFace(pass.CullTriangleFace);
        }
        else
        {
            gl.Enable(GLEnum.CullFace);
        }
        if (pass.AlphaBlend)
        {
            gl.Enable(GLEnum.Blend);
            gl.BlendFunc(pass.AlphaBlendFactors.source, pass.AlphaBlendFactors.destination);
            gl.BlendEquation(pass.AlphaEquation);
        }
        else
            gl.Disable(GLEnum.Blend);
        if ((pass.ClearBufferFlag & ClearBufferMask.ColorBufferBit) > 0)
            gl.ClearColor(pass.ClearColor);
        if ((pass.ClearBufferFlag & ClearBufferMask.DepthBufferBit) > 0)
            gl.ClearDepth(pass.ClearDepth);
        if ((pass.ClearBufferFlag & ClearBufferMask.StencilBufferBit) > 0)
            gl.ClearStencil(pass.ClearStencil);
        gl.Clear(pass.ClearBufferFlag);
    }

}