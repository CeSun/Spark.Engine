using System.Numerics;
using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.CompilerServices;


namespace Spark.Core.Assets;

public partial class SkeletalMesh(bool allowMuiltUpLoad = false) : AssetBase(allowMuiltUpLoad)
{
    public IReadOnlyList<Element<SkeletalMeshVertex>> _elements = [];
    public IReadOnlyList<Element<SkeletalMeshVertex>> Elements 
    {
        get => _elements;
        set => ChangeProperty(ref _elements, value);
    }
    public Skeleton? Skeleton { get; set; }
    public override void PostProxyToRenderer(BaseRenderer renderer)
    {
        foreach (var element in Elements)
        {
            element.Material?.PostProxyToRenderer(renderer);
        }
        base.PostProxyToRenderer(renderer);
    }
    protected unsafe override int assetPropertiesSize => sizeof(SkeletalMeshProxyProperties);
    public override nint CreateProperties()
    {
        var ptr = base.CreateProperties();
        ref var properties = ref UnsafeHelper.AsRef<SkeletalMeshProxyProperties>(ptr);
        properties.Elements.Resize(Elements.Count);
        for (int i = 0; i < Elements.Count; i++)
        {
            properties.Elements[i] = new ElementProxyProperties<SkeletalMeshVertex>
            {
                Vertices = new(Elements[i].Vertices),
                Indices = new(Elements[i].Indices),
                Material = Elements[i].Material == null ? default : Elements[i].Material!.WeakGCHandle
            };
        }
        return ptr;
    }

    public unsafe override nint GetCreateProxyFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<GCHandle>)&CreateProxy;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static GCHandle CreateProxy() => GCHandle.Alloc(new SkeletalMeshProxy(), GCHandleType.Normal);
    public unsafe override nint GetPropertiesDestoryFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&DestoryProperties;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void DestoryProperties(IntPtr ptr)
    {
        ref var properties = ref UnsafeHelper.AsRef<SkeletalMeshProxyProperties>(ptr);
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
        _elements = [];
    }
}

public class SkeletalMeshProxy : AssetRenderProxy
{
    public List<uint> VertexArrayObjectIndexes = [];

    public List<uint> VertexBufferObjectIndexes = [];

    public List<uint> ElementBufferObjectIndexes = [];

    public List<int> IndicesLengths = [];

    public unsafe override void UpdatePropertiesAndRebuildGPUResource(BaseRenderer renderer, IntPtr propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);
        var gl = renderer.gl;
        ref var properties = ref UnsafeHelper.AsRef<SkeletalMeshProxyProperties>(propertiesPtr);

        for (var index = 0; index < properties.Elements.Count; index++)
        {
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ebo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(properties.Elements[index].Vertices.Count * sizeof(SkeletalMeshVertex)), properties.Elements[index].Vertices.Ptr, GLEnum.StaticDraw);
            
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(properties.Elements[index].Indices.Count * sizeof(uint)), properties.Elements[index].Indices.Ptr, GLEnum.StaticDraw);

            // Location
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)0);
            // Normal
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)sizeof(Vector3));


            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 3, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(2 * sizeof(Vector3)));


            gl.EnableVertexAttribArray(3);
            gl.VertexAttribPointer(3, 3, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(3 * sizeof(Vector3)));
            // TexCoord
            gl.EnableVertexAttribArray(4);
            gl.VertexAttribPointer(4, 2, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(4 * sizeof(Vector3)));
            // BoneId
            gl.EnableVertexAttribArray(5);
            gl.VertexAttribPointer(5, 4, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(4 * sizeof(Vector3) + sizeof(Vector2)));
            // BoneWeight
            gl.EnableVertexAttribArray(6);
            gl.VertexAttribPointer(6, 4, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(4 * sizeof(Vector3) + sizeof(Vector2) + sizeof(Vector4)));
            gl.BindVertexArray(0);
            IndicesLengths.Add(properties.Elements[index].Indices.Length);
            VertexArrayObjectIndexes.Add(vao);
            VertexBufferObjectIndexes.Add(vbo);
            ElementBufferObjectIndexes.Add(ebo);
        }
    }



    public override void DestoryGpuResource(BaseRenderer renderer)
    {
        base.DestoryGpuResource(renderer);
        var gl = renderer.gl;
        VertexArrayObjectIndexes.ForEach(gl.DeleteVertexArray);
        VertexArrayObjectIndexes.Clear();
        VertexBufferObjectIndexes.ForEach(gl.DeleteBuffer);
        VertexBufferObjectIndexes.Clear();
        ElementBufferObjectIndexes.ForEach(gl.DeleteBuffer);
        ElementBufferObjectIndexes.Clear();
    }
}
public interface IVertex
{
    public Vector3 Location { get; set; }

    public Vector3 Normal { get; set; }

    public Vector3 Tangent { get; set; }

    public Vector3 BitTangent { get; set; }

    public Vector2 TexCoord { get; set; }
}
public struct SkeletalMeshVertex : IVertex
{
    public StaticMeshVertex Base;

    public Vector4 BoneIds;

    public Vector4 BoneWeights;
    public Vector3 Location
    {
        get => Base.Location;
        set => Base.Location = value;
    }
    public Vector3 Normal
    {
        get => Base.Normal;
        set => Base.Normal = value;
    }
    public Vector3 Tangent
    {
        get => Base.Tangent;
        set => Base.Tangent = value;
    }
    public Vector3 BitTangent
    {
        get => Base.BitTangent;
        set => Base.BitTangent = value;
    }
    public Vector2 TexCoord
    {
        get => Base.TexCoord;
        set => Base.TexCoord = value;
    }
}

public struct SkeletalMeshProxyProperties
{
    public AssetProperties Base;
    public UnmanagedArray<ElementProxyProperties<SkeletalMeshVertex>> Elements;
}