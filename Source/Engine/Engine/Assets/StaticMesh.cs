using SharpGLTF.Schema2;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGLES;
using Spark.Engine.Physics;
using Jitter2.LinearMath;
using System.Collections.ObjectModel;
using Jitter2.Collision.Shapes;
using Spark.Engine.Platform;
using Jitter2.Dynamics;

namespace Spark.Engine.Assets;

public class StaticMesh : AssetBase
{
    public List<Element<StaticMeshVertex>> Elements = new List<Element<StaticMeshVertex>>();
    public List<Shape> Shapes = new List<Shape>();
    public Box Box { get; private set; }
    public List<Box> Boxes { get; private set; } = new List<Box>();
    public StaticMesh()
    {
        Path = string.Empty;
    }

    public StaticMesh(List<Element<StaticMeshVertex>> Elementes)
    {
        Path = string.Empty;
        Elements.AddRange(Elementes);
    }

    public async static Task<StaticMesh> LoadFromGLBAsync(string Path)
    {

        using var sr = FileSystem.Instance.GetStreamReader("Content" + Path);
        return await LoadFromGLBAsync(sr.BaseStream);
    }
    public async static Task<StaticMesh> LoadFromGLBAsync(Stream stream)
    {
        StaticMesh? sm = null;
        await Task.Run(() =>
        {
            ModelRoot model = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix});
            sm = LoadFromGLBInternal(model);
        });
        return sm;
    }


    public static StaticMesh LoadFromGLB(string Path)
    {
    
        using var sr = FileSystem.Instance.GetStreamReader("Content" + Path);
        return LoadFromGLB(sr.BaseStream);
    }
    public static StaticMesh LoadFromGLB(Stream stream)
    {
        var model = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        return LoadFromGLBInternal(model);
    }

    public static StaticMesh LoadFromGLBInternal(ModelRoot model)
    {
        StaticMesh sm = new StaticMesh();
        foreach (var glMesh in model.LogicalMeshes)
        {
            if (glMesh.Name.IndexOf("Physics_") < 0)
            {
                foreach (var glPrimitive in glMesh.Primitives)
                {
                    List<StaticMeshVertex> staticMeshVertices = new List<StaticMeshVertex>();
                    foreach (var kv in glPrimitive.VertexAccessors)
                    {
                        int index = 0;
                        if (kv.Key == "POSITION")
                        {
                            foreach (var v in kv.Value.AsVector3Array())
                            {
                                var Vertex = new StaticMeshVertex();
                                if (staticMeshVertices.Count > index)
                                {
                                    Vertex = staticMeshVertices[index];
                                }
                                else
                                {
                                    staticMeshVertices.Add(Vertex);
                                }
                                Vertex.Location = new Vector3 { X = v.X, Y = v.Y, Z = v.Z };
                                if (staticMeshVertices.Count > index)
                                {
                                }
                                staticMeshVertices[index] = Vertex;
                                index++;
                            }
                        }
                        if (kv.Key == "NORMAL")
                        {
                            foreach (var v in kv.Value.AsVector3Array())
                            {
                                var Vertex = new StaticMeshVertex();
                                if (staticMeshVertices.Count > index)
                                {
                                    Vertex = staticMeshVertices[index];
                                }
                                else
                                {
                                    staticMeshVertices.Add(Vertex);
                                }
                                Vertex.Normal = new Vector3 { X = v.X, Y = v.Y, Z = v.Z };
                                staticMeshVertices[index] = Vertex;
                                index++;
                            }
                        }
                        if (kv.Key == "TEXCOORD_0")
                        {
                            foreach (var v in kv.Value.AsVector2Array())
                            {

                                var Vertex = new StaticMeshVertex();
                                if (staticMeshVertices.Count > index)
                                {
                                    Vertex = staticMeshVertices[index];
                                }
                                else
                                {
                                    staticMeshVertices.Add(Vertex);
                                }
                                Vertex.TexCoord = new Vector2 { X = v.X, Y = v.Y };
                                staticMeshVertices[index] = Vertex;
                                index++;
                            }
                        }
                    }
                    Box box = new Box();
                    if (staticMeshVertices.Count > 0)
                    {
                        box.MaxPoint = box.MinPoint = staticMeshVertices[0].Location;
                    }
                    foreach (var Vertex in staticMeshVertices)
                    {
                        box += Vertex.Location;
                    }
                    sm.Boxes.Add(box);
                    List<uint> Indices = [.. glPrimitive.IndexAccessor.AsIndicesArray()];
                    var Material = new Material();
                    byte[]? MetallicRoughness = null;
                    byte[]? AmbientOcclusion = null;
                    byte[]? Parallax = null;

                    foreach (var glChannel in glPrimitive.Material.Channels)
                    {
                        if (glChannel.Texture == null)
                            continue;

                        if (glChannel.Key == "MetallicRoughness")
                        {
                            MetallicRoughness = glChannel.Texture.PrimaryImage.Content.Content.ToArray();
                            continue;
                        }
                        if (glChannel.Key == "AmbientOcclusion")
                        {
                            AmbientOcclusion = glChannel.Texture.PrimaryImage.Content.Content.ToArray();
                            continue;
                        }
                        if (glChannel.Key == "Parallax")
                        {

                            Parallax = glChannel.Texture.PrimaryImage.Content.Content.ToArray();
                            continue;
                        }

                        var texture = Texture.LoadFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray());
                        if (glChannel.Key == "BaseColor" || glChannel.Key == "Diffuse")
                        {
                            Material.BaseColor = texture;
                        }
                        if (glChannel.Key == "Normal")
                        {
                            Material.Normal = texture;
                        }

                    }
                    Texture Arm = Texture.LoadPBRTexture(MetallicRoughness, AmbientOcclusion);
                    Material.Arm = Arm;
                    sm.Elements.Add(new Element<StaticMeshVertex>
                    {
                        Vertices = staticMeshVertices,
                        Material = Material,
                        Indices = Indices,
                        IndicesLen = (uint)Indices.Count
                    });
                }
            }
            else
            {
                foreach (var glPrimitive in glMesh.Primitives)
                {
                    List<JTriangle> ShapeSource = new List<JTriangle>();
                    var locations = glPrimitive.GetVertices("POSITION").AsVector3Array();
                    for (int i = 0; i < glPrimitive.GetIndices().Count; i += 3)
                    {
                        int index1 = (int)glPrimitive.GetIndices()[i];
                        int index2 = (int)glPrimitive.GetIndices()[i + 1];
                        int index3 = (int)glPrimitive.GetIndices()[i + 2];

                        JTriangle tri = new JTriangle
                        {
                            V0 = new JVector
                            {
                                X = locations[index1].X,
                                Y = locations[index1].Y,
                                Z = locations[index1].Z
                            },
                            V1 = new JVector
                            {
                                X = locations[index2].X,
                                Y = locations[index2].Y,
                                Z = locations[index2].Z
                            },
                            V2 = new JVector
                            {
                                X = locations[index3].X,
                                Y = locations[index3].Y,
                                Z = locations[index3].Z
                            },
                        };
                        ShapeSource.Add(tri);
                    }
                    sm.Shapes.Add(new ConvexHullShape(ShapeSource));
                }
            }
        }
        sm.InitTBN();
        sm.InitPhysics();
        if (sm.Boxes.Count > 0)
        {
            sm.Box = sm.Boxes[0];
            foreach (var box in sm.Boxes)
            {
                sm.Box += box;
            }
        }
        return sm;
    }
  
    public void InitTBN()
    {
        for (int i = 0; i < Elements.Count; i ++)
        {
            InitMeshTBN(i);
        }
    }

    public void InitPhysics()
    {
        if (Shapes.Count > 0 )
        {
            return;
        }
        List<JVector> vertices = new List<JVector>();
        foreach(var element in Elements)
        {
            foreach(var vertex in element.Vertices)
            {

                vertices.Add(new JVector
                {
                    X = vertex.Location.X,
                    Y = vertex.Location.Y,
                    Z = vertex.Location.Z
                });
            }
        }
        Shapes.Add(new PointCloudShape(vertices));

    }
    private void InitMeshTBN(int index)
    {
        var vertics = Elements[index].Vertices;
        var indices = Elements[index].Indices;

        for(int i = 0; i < indices.Count - 2; i += 3)
        {

            var p1 = vertics[(int)indices[i]];
            var p2 = vertics[(int)indices[i + 1]];
            var p3 = vertics[(int)indices[i + 2]];

            Vector3 Edge1 = p2.Location - p1.Location;
            Vector3 Edge2 = p3.Location - p1.Location;
            Vector2 DeltaUV1 = p2.TexCoord - p1.TexCoord;
            Vector2 DeltaUV2 = p3.TexCoord - p1.TexCoord;

            float f = 1.0f / (DeltaUV1.X * DeltaUV2.Y - DeltaUV2.X * DeltaUV1.Y);

            Vector3 tangent1;
            Vector3 bitangent1;

            tangent1.X = f * (DeltaUV2.Y * Edge1.X - DeltaUV1.Y * Edge2.X);
            tangent1.Y = f * (DeltaUV2.Y * Edge1.Y - DeltaUV1.Y * Edge2.Y);
            tangent1.Z = f * (DeltaUV2.Y * Edge1.Z - DeltaUV1.Y * Edge2.Z);
            tangent1 = Vector3.Normalize(tangent1);

            bitangent1.X = f * (-DeltaUV2.X * Edge1.X + DeltaUV1.X * Edge2.X);
            bitangent1.Y = f * (-DeltaUV2.X * Edge1.Y + DeltaUV1.X * Edge2.Y);
            bitangent1.Z = f * (-DeltaUV2.X * Edge1.Z + DeltaUV1.X * Edge2.Z);
            bitangent1 = Vector3.Normalize(bitangent1);

            p1.Tangent = tangent1;
            p2.Tangent = tangent1;
            p3.Tangent = tangent1;


            p1.BitTangent = bitangent1;
            p2.BitTangent = bitangent1;
            p3.BitTangent = bitangent1;

            vertics[(int)indices[i]] = p1;
            vertics[(int)indices[i + 1]] = p2;
            vertics[(int)indices[i + 2]] = p3;

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
        ReleaseMemory();
    }

    public void ReleaseMemory()
    {
        foreach(var element in Elements)
        {
            element.Vertices = null;
            element.Indices = null;
        }
    }

    public override void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(MagicCode.Asset);
        bw.WriteInt32(MagicCode.StaticMesh);
        bw.WriteInt32(Elements.Count);
        foreach(var element in Elements)
        {
            element.Serialize(bw, engine);
        }
    }

    public override void Deserialize(BinaryReader br, Engine engine)
    {
        var AssetMagicCode = br.ReadInt32();
        if (AssetMagicCode != MagicCode.Asset)
            throw new Exception("");
        var TextureMagicCode = br.ReadInt32();
        if (TextureMagicCode != MagicCode.StaticMesh)
            throw new Exception("");
        var count = br.ReadInt32();
        for(var i = 0; i < count; i++)
        {
            var element = new Element<StaticMeshVertex>() {
                Vertices = new List<StaticMeshVertex>(),
                Indices = new List<uint>(),
                Material = new Material()
            };
            element.Deserialize(br, engine);
            Elements.Add(element);
        }

    }
}

public class Element<T> : ISerializable  where T  : struct, ISerializable
{
    public required List<T> Vertices;
    public required List<uint> Indices;
    public required Material Material;
    public uint VertexArrayObjectIndex;
    public uint VertexBufferObjectIndex;
    public uint ElementBufferObjectIndex;
    public uint IndicesLen;

    public void Deserialize(BinaryReader br, Engine engine)
    {
        var count = br.ReadInt32();
        for(int i = 0; i < count; i ++ )
        {
            var vertex = new T();
            vertex.Deserialize(br, engine);
            Vertices.Add(vertex);
        }

        count = br.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            Indices.Add(br.ReadUInt32());
        }
        Material = ISerializable.AssetDeserialize<Material>(br, engine);
    }

    public void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteInt32(Vertices.Count);
        foreach(var vertex in Vertices)
        {
            vertex.Serialize(bw, engine);
        }
        bw.WriteInt32(Indices.Count);
        foreach (var index in Indices)
        {
            bw.Write(BitConverter.GetBytes(index));
        }
        ISerializable.AssetSerialize(Material, bw, engine);
    }
}

public struct StaticMeshVertex: ISerializable
{
    public Vector3 Location;

    public Vector3 Normal;

    public Vector3 Tangent;

    public Vector3 BitTangent;

    public Vector3 Color;

    public Vector2 TexCoord;

    public void Deserialize(BinaryReader br, Engine engine)
    {
        Location = br.ReadVector3();
        Normal = br.ReadVector3();
        Tangent = br.ReadVector3();
        BitTangent = br.ReadVector3();
        Color = br.ReadVector3();
        TexCoord = br.ReadVector2();
    }

    public void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.Write(Location);
        bw.Write(Normal);
        bw.Write(Tangent);
        bw.Write(BitTangent);
        bw.Write(Color);
        bw.Write(TexCoord);
    }
}
