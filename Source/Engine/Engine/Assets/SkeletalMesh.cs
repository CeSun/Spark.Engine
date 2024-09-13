using System.Numerics;
using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;


namespace Spark.Engine.Assets;

public partial class SkeletalMesh : AssetBase
{
    public List<Element<SkeletalMeshVertex>> Elements = [];
    public Skeleton? Skeleton { get; set; }
}

public class SkeletalMeshProxy : RenderProxy
{
    public List<ElementProxy<SkeletalMeshVertex>> Elements = [];

    public unsafe override void RebuildGpuResource(GL gl)
    {
        for (var index = 0; index < Elements.Count; index++)
        {
            if (Elements[index].VertexArrayObjectIndex > 0)
                continue;
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ebo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (SkeletalMeshVertex* p = CollectionsMarshal.AsSpan(Elements[index].Vertices))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(Elements[index].Vertices.Count * sizeof(SkeletalMeshVertex)), p, GLEnum.StaticDraw);
            }
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            fixed (uint* p = CollectionsMarshal.AsSpan(Elements[index].Indices))
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(Elements[index].Indices.Count * sizeof(uint)), p, GLEnum.StaticDraw);
            }

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

            // Color
            gl.EnableVertexAttribArray(4);
            gl.VertexAttribPointer(4, 3, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(4 * sizeof(Vector3)));
            // TexCoord
            gl.EnableVertexAttribArray(5);
            gl.VertexAttribPointer(5, 2, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(5 * sizeof(Vector3)));
            // BoneId
            gl.EnableVertexAttribArray(6);
            gl.VertexAttribPointer(6, 4, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(5 * sizeof(Vector3) + sizeof(Vector2)));
            // BoneWeight
            gl.EnableVertexAttribArray(7);
            gl.VertexAttribPointer(7, 4, GLEnum.Float, false, (uint)sizeof(SkeletalMeshVertex), (void*)(5 * sizeof(Vector3) + sizeof(Vector2) + sizeof(Vector4)));
            gl.BindVertexArray(0);
            Elements[index].VertexArrayObjectIndex = vao;
            Elements[index].VertexBufferObjectIndex = vbo;
            Elements[index].ElementBufferObjectIndex = ebo;
        }
    }
}
public interface IVertex
{
    public Vector3 Location { get; set; }

    public Vector3 Normal { get; set; }

    public Vector3 Tangent { get; set; }

    public Vector3 BitTangent { get; set; }

    public Vector3 Color { get; set; }

    public Vector2 TexCoord { get; set; }
}
public struct SkeletalMeshVertex : IVertex
{
    public Vector3 Location { get; set; }
    public Vector3 Normal { get; set; }
    public Vector3 Tangent { get; set; }
    public Vector3 BitTangent { get; set; }
    public Vector3 Color { get; set; }
    public Vector2 TexCoord { get; set; }

    public Vector4 BoneIds;

    public Vector4 BoneWeights;
}