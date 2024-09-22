using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGLES;
using Spark.Core.Render;

namespace Spark.Core.Assets;

public class StaticMesh : AssetBase
{

    private IReadOnlyList<Element<StaticMeshVertex>> _elements = [];
    public IReadOnlyList<Element<StaticMeshVertex>> Elements 
    {
        get => _elements; 
        set
        {
            _elements = value;
            var list = _elements.ToList();
            RunOnRenderer(renderer =>
            {
                var staticMeshProxy = renderer.GetProxy<StaticMeshProxy>(this);
                if (staticMeshProxy != null)
                {
                    staticMeshProxy.Elements = list;
                    RequestRendererRebuildGpuResource();
                }
            });
        }
    }

    public override void PostProxyToRenderer(BaseRenderer renderer)
    {
        foreach (var element in _elements)
        {
            if (element.Material == null)
                continue;
            element.Material.PostProxyToRenderer(renderer);
        }
        base.PostProxyToRenderer(renderer);
    }
    public override Func<BaseRenderer, RenderProxy>? GetGenerateProxyDelegate()
    {
        var elements = Elements.ToList();

        return renderer => new StaticMeshProxy
        {
            Elements = elements
        }; 
    }
}
public class StaticMeshProxy : RenderProxy
{
    public List<Element<StaticMeshVertex>> Elements = [];

    public List<uint> VertexArrayObjectIndexes = [];
    public List<uint> VertexBufferObjectIndexes = [];
    public List<uint> ElementBufferObjectIndexes = [];
    public unsafe override void RebuildGpuResource(GL gl)
    {
        DestoryGpuResource(gl);
        VertexArrayObjectIndexes.ForEach(gl.DeleteVertexArray);
        VertexBufferObjectIndexes.ForEach(gl.DeleteBuffer);
        ElementBufferObjectIndexes.ForEach(gl.DeleteBuffer);

        VertexArrayObjectIndexes = new List<uint>();
        VertexBufferObjectIndexes = new List<uint>();
        ElementBufferObjectIndexes = new List<uint>();

        for (var index = 0; index < Elements.Count; index++)
        {
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ebo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (StaticMeshVertex* p = CollectionsMarshal.AsSpan(Elements[index].Vertices))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(Elements[index].Vertices.Count * sizeof(StaticMeshVertex)), p, GLEnum.StaticDraw);
            }
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            fixed (uint* p = CollectionsMarshal.AsSpan(Elements[index].Indices))
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(Elements[index].Indices.Count * sizeof(uint)), p, GLEnum.StaticDraw);
            }

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

            // Color
            gl.EnableVertexAttribArray(4);
            gl.VertexAttribPointer(4, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(4 * sizeof(Vector3)));
            // TexCoord
            gl.EnableVertexAttribArray(5);
            gl.VertexAttribPointer(5, 2, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(5 * sizeof(Vector3)));
            gl.BindVertexArray(0);
            VertexArrayObjectIndexes.Add (vao);
            VertexBufferObjectIndexes.Add(vbo);
            ElementBufferObjectIndexes.Add(ebo);
        }

    }
}
public class Element<T>  where T  : struct
{
    public List<T> Vertices = [];
    public List<uint> Indices = [];
    public Material? Material;
}

public struct StaticMeshVertex : IVertex
{
    public Vector3 Location { get; set; }
    public Vector3 Normal { get; set; }
    public Vector3 Tangent { get; set; }
    public Vector3 BitTangent { get; set; }
    public Vector3 Color { get; set; }
    public Vector2 TexCoord { get; set; }

}
