using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Core.Shapes;
using Spark.Util;

namespace Spark.Core.Assets;

public class StaticMesh(bool allowMuiltUpLoad = false) : AssetBase(allowMuiltUpLoad)
{
    public Box Box { get; set; }
    private List<StaticMeshLod> _staticMeshLods = [];
    public List<StaticMeshLod> StaticMeshLods 
    {
        get => _staticMeshLods;
        set => ChangeProperty(ref _staticMeshLods, value);
    }

    protected unsafe override int assetPropertiesSize => sizeof(StaticMeshProxyProperties);
    public override nint CreateProperties()
    {
        var ptr = base.CreateProperties();
        ref var properties = ref UnsafeHelper.AsRef<StaticMeshProxyProperties>(ptr);
        properties.StaticMeshLoads.Resize(StaticMeshLods.Count);
        properties.Box = Box;
        for (int i = 0; i < _staticMeshLods.Count; i++)
        {
            properties.StaticMeshLoads.GetRefByIndex(i).Elements.Resize(StaticMeshLods[i].Elements.Count);
            for (int j = 0; j <_staticMeshLods[i].Elements.Count; j++)
            {
                properties.StaticMeshLoads.GetRefByIndex(i).Elements[j] = new ElementProxyProperties<StaticMeshVertex>
                {
                    Vertices = new(StaticMeshLods[i].Elements[j].Vertices),
                    Indices = new(StaticMeshLods[i].Elements[j].Indices),
                    Material = StaticMeshLods[i].Elements[j].Material == null ? default : StaticMeshLods[i].Elements[j].Material!.WeakGCHandle
                };
            }
        }
        
        return ptr;
    }

    public override void PostProxyToRenderer(RenderDevice renderer)
    {
        foreach (var lod in StaticMeshLods)
        {
            foreach(var element in lod.Elements)
            {
                element.Material?.PostProxyToRenderer(renderer);
            }
        }
        base.PostProxyToRenderer(renderer);
    }
    public unsafe override nint GetCreateProxyFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<GCHandle>)&CreateProxy;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static GCHandle CreateProxy() => GCHandle.Alloc(new StaticMeshProxy(), GCHandleType.Normal);
    public unsafe override nint GetPropertiesDestoryFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&DestoryProperties;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void DestoryProperties(IntPtr ptr)
    {
        ref var properties = ref UnsafeHelper.AsRef<StaticMeshProxyProperties>(ptr);
        for (int i = 0; i < properties.StaticMeshLoads.Count; i++)
        {
            for (int j = 0; j < properties.StaticMeshLoads[i].Elements.Count; j++)
            {
                properties.StaticMeshLoads[i].Elements[j].Vertices.Dispose();
                properties.StaticMeshLoads[i].Elements[j].Indices.Dispose();
            }
            properties.StaticMeshLoads[i].Elements.Dispose();
        }
        properties.StaticMeshLoads.Dispose();
    }


    protected override void ReleaseAssetMemory()
    {
        base.ReleaseAssetMemory();
        foreach(var lod in _staticMeshLods)
        {
            foreach (var element in lod.Elements)
            {
                element.Vertices = [];
                element.Indices = [];
            }
        }
    }

}

public class StaticMeshLod
{
    public List<Element<StaticMeshVertex>> Elements = [];

}
public class StaticMeshProxy : AssetRenderProxy
{
    public List<StaticMeshLodProxy> StaticMeshLods = [];
    public Box Box;
    public unsafe override void UpdatePropertiesAndRebuildGPUResource(RenderDevice renderer, IntPtr propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);
        var gl = renderer.gl;
        ref var properties = ref UnsafeHelper.AsRef<StaticMeshProxyProperties>(propertiesPtr);
        Box = properties.Box;
        for (var i = 0; i < properties.StaticMeshLoads.Length; i++)
        {
            var staticMeshLod = new StaticMeshLodProxy();
            for (var j = 0; j < properties.StaticMeshLoads[i].Elements.Count; j++)
            {
                uint vao = gl.GenVertexArray();
                uint vbo = gl.GenBuffer();
                uint ebo = gl.GenBuffer();
                gl.BindVertexArray(vao);
                Span<StaticMeshVertex> test = new Span<StaticMeshVertex>(properties.StaticMeshLoads[i].Elements[j].Vertices.Ptr, properties.StaticMeshLoads[i].Elements[j].Vertices.Length);
                Span<uint> test2 = new Span<uint>(properties.StaticMeshLoads[i].Elements[j].Indices.Ptr, properties.StaticMeshLoads[i].Elements[j].Indices.Length);
                gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(properties.StaticMeshLoads[i].Elements[j].Vertices.Length * sizeof(StaticMeshVertex)), properties.StaticMeshLoads[i].Elements[j].Vertices.Ptr, GLEnum.StaticDraw);

                gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(properties.StaticMeshLoads[i].Elements[j].Indices.Length * sizeof(uint)), properties.StaticMeshLoads[i].Elements[j].Indices.Ptr, GLEnum.StaticDraw);


                // Location
                gl.EnableVertexAttribArray(0);
                gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)0);
                // Normal
                gl.EnableVertexAttribArray(1);
                gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)sizeof(Vector3));

                gl.EnableVertexAttribArray(2);
                gl.VertexAttribPointer(2, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(2 * sizeof(Vector3)));

                gl.EnableVertexAttribArray(3);
                gl.VertexAttribPointer(3, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(3 * sizeof(Vector3)));

                gl.EnableVertexAttribArray(4);
                gl.VertexAttribPointer(4, 2, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(4 * sizeof(Vector3)));
                gl.BindVertexArray(0);

                staticMeshLod.Elements.Add(new ElementProxy
                {
                    VertexArrayObjectIndex = vao,
                    VertexBufferObjectIndex = vbo,
                    ElementBufferObjectIndex = ebo,
                    IndicesLength = properties.StaticMeshLoads[i].Elements[j].Indices.Length,
                    Material = renderer.GetProxy<MaterialProxy>(properties.StaticMeshLoads[i].Elements[j].Material)
                });
            }
            StaticMeshLods.Add(staticMeshLod);
        }

    }


    public override void DestoryGpuResource(RenderDevice renderer)
    {
        base.DestoryGpuResource(renderer);
        var gl = renderer.gl;
        foreach (var lod in StaticMeshLods)
        {
            lod.Elements.ForEach(element =>
            {
                gl.DeleteBuffer(element.VertexBufferObjectIndex);
                gl.DeleteBuffer(element.ElementBufferObjectIndex);
                gl.DeleteVertexArray(element.VertexArrayObjectIndex);
            });
        }
        StaticMeshLods.Clear();
    }


}
public class StaticMeshLodProxy
{
    public List<ElementProxy> Elements = [];
}
public class ElementProxy
{
    public uint VertexArrayObjectIndex;
    public uint VertexBufferObjectIndex;
    public uint ElementBufferObjectIndex;
    public int IndicesLength;
    public MaterialProxy? Material;
}
public class Element<T>  where T  : unmanaged
{
    public List<T> Vertices = [];
    public List<uint> Indices = [];
    public Material? Material;
}

public struct StaticMeshVertex : IVertex
{
    public Vector3 _location;
    public Vector3 _normal;
    public Vector3 _tangent;
    public Vector3 _bitTangent;
    public Vector2 _texCoord;

    public Vector3 Location
    {
        get => _location;
        set => _location = value;
    }
    public Vector3 Normal
    {
        get => _normal;
        set => _normal = value;
    }
    public Vector3 Tangent
    {
        get => _tangent;
        set => _tangent = value;
    }
    public Vector3 BitTangent
    {
        get => _bitTangent;
        set => _bitTangent = value;
    }
    public Vector2 TexCoord
    {
        get => _texCoord;
        set => _texCoord = value;
    }
}

public struct StaticMeshProxyProperties
{
    public AssetProperties Base;
    public UnmanagedArray<StaticMeshLodProxyProperties> StaticMeshLoads;
    public Box Box;
}

public struct StaticMeshLodProxyProperties
{
    public UnmanagedArray<ElementProxyProperties<StaticMeshVertex>> Elements;
}
public struct ElementProxyProperties<T> where T : unmanaged
{
    public UnmanagedArray<T> Vertices;
    public UnmanagedArray<uint> Indices;
    public GCHandle Material;
}