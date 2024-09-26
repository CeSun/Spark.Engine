using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Util;

namespace Spark.Core.Assets;

public class StaticMesh(bool allowMuiltUpLoad = false) : AssetBase(allowMuiltUpLoad)
{

    private List<Element<StaticMeshVertex>> _elements = [];
    public List<Element<StaticMeshVertex>> Elements 
    {
        get => _elements;
        set => ChangeProperty(ref _elements, value);
    }

    protected unsafe override int assetPropertiesSize => sizeof(StaticMeshProxyProperties);
    public override nint CreateProperties()
    {
        var ptr = base.CreateProperties();
        ref var properties = ref UnsafeHelper.AsRef<StaticMeshProxyProperties>(ptr);
        properties.Elements.Resize(Elements.Count);
        for(int i = 0; i < Elements.Count; i ++)
        {
            properties.Elements[i] = new ElementProxyProperties<StaticMeshVertex>
            {
                Vertices = new(Elements[i].Vertices),
                Indices = new(Elements[i].Indices),
                Material = Elements[i].Material == null ? default : Elements[i].Material!.WeakGCHandle
            };
        }
        return ptr;
    }

    public override void PostProxyToRenderer(BaseRenderer renderer)
    {
        foreach (var element in Elements)
        {
            element.Material?.PostProxyToRenderer(renderer);
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
        for (int i = 0; i < properties.Elements.Count; i++)
        {
            properties.Elements[i].Vertices.Dispose();
            properties.Elements[i].Indices.Dispose();
        }
        properties.Elements.Dispose();
    }


    protected override void ReleaseAssetMemory()
    {
        base.ReleaseAssetMemory();
        foreach(var element in _elements)
        {
            element.Vertices = [];
            element.Indices = [];
        }
    }

}
public class StaticMeshProxy : AssetRenderProxy
{
    public List<ElementProxy> Elements = [];
    public unsafe override void UpdatePropertiesAndRebuildGPUResource(BaseRenderer renderer, IntPtr propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);
        var gl = renderer.gl;
        ref var properties = ref UnsafeHelper.AsRef<StaticMeshProxyProperties>(propertiesPtr);

        for (var index = 0; index < properties.Elements.Length; index++)
        {
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ebo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            Span<StaticMeshVertex> test = new Span<StaticMeshVertex>(properties.Elements[index].Vertices.Ptr, properties.Elements[index].Vertices.Length);
            Span<uint> test2 = new Span<uint>(properties.Elements[index].Indices.Ptr, properties.Elements[index].Indices.Length);
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(properties.Elements[index].Vertices.Length * sizeof(StaticMeshVertex)), properties.Elements[index].Vertices.Ptr, GLEnum.StaticDraw);
        
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(properties.Elements[index].Indices.Length * sizeof(uint)), properties.Elements[index].Indices.Ptr, GLEnum.StaticDraw);
            

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

            Elements.Add(new ElementProxy
            {
                VertexArrayObjectIndex = vao,
                VertexBufferObjectIndex = vbo,
                ElementBufferObjectIndex = ebo,
                IndicesLength = properties.Elements[index].Indices.Length,
                Material = renderer.GetProxy<MaterialProxy>(properties.Elements[index].Material)
            });
        }

    }


    public override void DestoryGpuResource(BaseRenderer renderer)
    {
        base.DestoryGpuResource(renderer);
        var gl = renderer.gl;
        Elements.ForEach(element =>
        {
            gl.DeleteBuffer(element.VertexBufferObjectIndex);
            gl.DeleteBuffer(element.ElementBufferObjectIndex);
            gl.DeleteVertexArray(element.VertexArrayObjectIndex);
        });
        Elements.Clear();
    }


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
    public UnmanagedArray<ElementProxyProperties<StaticMeshVertex>> Elements;
}

public struct ElementProxyProperties<T> where T : unmanaged
{
    public UnmanagedArray<T> Vertices;
    public UnmanagedArray<uint> Indices;
    public GCHandle Material;
}