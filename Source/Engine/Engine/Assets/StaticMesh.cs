using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGLES;

namespace Spark.Engine.Assets;

public class StaticMesh : AssetBase
{

    public List<Element<StaticMeshVertex>> Elements = [];

    public void InitTbn()
    {
        for (int i = 0; i < Elements.Count; i ++)
        {
            InitMeshTbn(i);
        }
    }

    private void InitMeshTbn(int index)
    {
        var vertices = Elements[index].Vertices;
        var indices = Elements[index].Indices;

        for(int i = 0; i < indices.Count - 2; i += 3)
        {

            var p1 = vertices[(int)indices[i]];
            var p2 = vertices[(int)indices[i + 1]];
            var p3 = vertices[(int)indices[i + 2]];

            Vector3 edge1 = p2.Location - p1.Location;
            Vector3 edge2 = p3.Location - p1.Location;
            Vector2 deltaUv1 = p2.TexCoord - p1.TexCoord;
            Vector2 deltaUv2 = p3.TexCoord - p1.TexCoord;

            float f = 1.0f / (deltaUv1.X * deltaUv2.Y - deltaUv2.X * deltaUv1.Y);

            Vector3 tangent1;
            Vector3 bitangent1;

            tangent1.X = f * (deltaUv2.Y * edge1.X - deltaUv1.Y * edge2.X);
            tangent1.Y = f * (deltaUv2.Y * edge1.Y - deltaUv1.Y * edge2.Y);
            tangent1.Z = f * (deltaUv2.Y * edge1.Z - deltaUv1.Y * edge2.Z);
            tangent1 = Vector3.Normalize(tangent1);

            bitangent1.X = f * (-deltaUv2.X * edge1.X + deltaUv1.X * edge2.X);
            bitangent1.Y = f * (-deltaUv2.X * edge1.Y + deltaUv1.X * edge2.Y);
            bitangent1.Z = f * (-deltaUv2.X * edge1.Z + deltaUv1.X * edge2.Z);
            bitangent1 = Vector3.Normalize(bitangent1);

            p1.Tangent = tangent1;
            p2.Tangent = tangent1;
            p3.Tangent = tangent1;


            p1.BitTangent = bitangent1;
            p2.BitTangent = bitangent1;
            p3.BitTangent = bitangent1;

            vertices[(int)indices[i]] = p1;
            vertices[(int)indices[i + 1]] = p2;
            vertices[(int)indices[i + 2]] = p3;

        }

    }
    public unsafe void InitRender(GL gl)
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
            Elements[index].VertexArrayObjectIndex = vao;
            Elements[index].VertexBufferObjectIndex = vbo;
            Elements[index].ElementBufferObjectIndex = ebo;
        }
    }
}

public class Element<T>  where T  : struct
{
    public required List<T> Vertices;
    public required List<uint> Indices;
    public required Material Material;
    public uint VertexArrayObjectIndex;
    public uint VertexBufferObjectIndex;
    public uint ElementBufferObjectIndex;
    public uint IndicesLen;
}

public struct StaticMeshVertex
{
    public Vector3 Location;

    public Vector3 Normal;

    public Vector3 Tangent;

    public Vector3 BitTangent;

    public Vector3 Color;

    public Vector2 TexCoord;

}
