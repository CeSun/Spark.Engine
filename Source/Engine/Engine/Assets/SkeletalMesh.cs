using System.Numerics;
using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;


namespace Spark.Engine.Assets;

public partial class SkeletalMesh : AssetBase
{
    public List<Element<SkeletalMeshVertex>> Elements = [];

    public Skeleton? Skeleton { get; set; }

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

public partial class SkeletalMesh
{
    public void InitTbn()
    {
        for (int i = 0; i < Elements.Count; i++)
        {
            InitMeshTbn(i);
        }
    }
    private void InitMeshTbn(int index)
    {
        var vertices = Elements[index].Vertices;
        var indices = Elements[index].Indices;

        for (int i = 0; i < indices.Count; i += 3)
        {
            var p1 = vertices[(int)indices[i]];
            var p2 = vertices[(int)indices[i + 1]];
            var p3 = vertices[(int)indices[i + 2]];

            Vector3 edge1 = p2.Location - p1.Location;
            Vector3 edge2 = p3.Location - p1.Location;
            Vector2 deltaUv1 = p2.TexCoord - p1.TexCoord;
            Vector2 deltaUv2 = p3.TexCoord - p1.TexCoord;

            var f = 1.0f / (deltaUv1.X * deltaUv2.Y - deltaUv2.X * deltaUv1.Y);

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


    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.SkeletalMesh);
        bw.WriteInt32(Elements.Count);
        foreach (var element in Elements)
        {
            element.Serialize(bw, engine);
        }
        ISerializable.AssetSerialize(Skeleton, bw, engine);
    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        var assetMagicCode = br.ReadInt32();
        if (assetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var textureMagicCode = br.ReadInt32();
        if (textureMagicCode != MagicCode.SkeletalMesh)
            throw new Exception("");
        var count = br.ReadInt32();
        for (var i = 0; i < count; i++)
        {
            var element = new Element<SkeletalMeshVertex>()
            {
                Vertices = new List<SkeletalMeshVertex>(),
                Indices = new List<uint>(),
                Material = new Material()
            };
            element.Deserialize(br, engine);
            Elements.Add(element);
        }
        Skeleton = ISerializable.AssetDeserialize<Skeleton>(br, engine);

        engine.NextRenderFrame.Add(InitRender);

    }

}

public struct SkeletalMeshVertex : ISerializable
{
    public Vector3 Location;

    public Vector3 Normal;

    public Vector3 Tangent;

    public Vector3 BitTangent;

    public Vector3 Color;

    public Vector2 TexCoord;

    public Vector4 BoneIds;

    public Vector4 BoneWeights;

    public void Deserialize(BinaryReader br, Engine engine)
    {
        Location = br.ReadVector3();
        Normal = br.ReadVector3();
        Tangent = br.ReadVector3();
        BitTangent = br.ReadVector3();
        Color = br.ReadVector3();
        TexCoord = br.ReadVector2();
        BoneIds = br.ReadVector4();
        BoneWeights = br.ReadVector4();
    }

    public void Serialize(BinaryWriter bw, Engine engine)
    {

        bw.Write(Location);
        bw.Write(Normal);
        bw.Write(Tangent);
        bw.Write(BitTangent);
        bw.Write(Color);
        bw.Write(TexCoord);
        bw.Write(BoneIds);
        bw.Write(BoneWeights);




    }
}