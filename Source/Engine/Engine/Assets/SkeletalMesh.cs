using Jitter.LinearMath;
using SharpGLTF.Schema2;
using Spark.Engine.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using static Spark.Engine.StaticEngine;
using System.Threading.Tasks;

namespace Spark.Engine.Assets;

public class SkeletalMesh : Asset
{
    List<List<SkeletalMeshVertex>> Meshes = new List<List<SkeletalMeshVertex>>();
    List<List<uint>> _IndicesList = new List<List<uint>>();
    public List<Material> Materials = new List<Material>();
    List<JVector> ConvexHullSourceData = new List<JVector>();
    List<uint> _VertexArrayObjectIndexes = new List<uint>();
    List<uint> VertexBufferObjectIndexes = new List<uint>();
    List<uint> _ElementBufferObjectIndexes = new List<uint>();

    public IReadOnlyList<uint> ElementBufferObjectIndexes => _ElementBufferObjectIndexes;
    public IReadOnlyList<IReadOnlyCollection<uint>> IndicesList => _IndicesList;
    public IReadOnlyList<uint> VertexArrayObjectIndexes => _VertexArrayObjectIndexes;
    public SkeletalMesh(string Path) : base(Path)
    {

    }
    protected override void LoadAsset()
    {
        using var sr = FileSystem.GetStream("Content" + Path);

        var model = ModelRoot.ReadGLB(sr);
        foreach (var glMesh in model.LogicalMeshes)
        {
            foreach (var glPrimitive in glMesh.Primitives)
            {
                List<SkeletalMeshVertex> staticMeshVertices = new List<SkeletalMeshVertex>();
                foreach (var kv in glPrimitive.VertexAccessors)
                {
                    int index = 0;
                    if (kv.Key == "POSITION")
                    {
                        foreach (var v in kv.Value.AsVector3Array())
                        {
                            var Vertex = new SkeletalMeshVertex();
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
                            var Vertex = new SkeletalMeshVertex();
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

                            var Vertex = new SkeletalMeshVertex();
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

                    if (kv.Key == "JOINTS_0")
                    {
                        foreach (var v in kv.Value.AsVector4Array())
                        {

                            var Vertex = new SkeletalMeshVertex();
                            if (staticMeshVertices.Count > index)
                            {
                                Vertex = staticMeshVertices[index];
                            }
                            else
                            {
                                staticMeshVertices.Add(Vertex);
                            }
                            Vertex.BoneIds = new Vector4 { X = v.X, Y = v.Y, Z = v.Z, W = v.W };
                            staticMeshVertices[index] = Vertex;
                            index++;
                        }
                    }

                    if (kv.Key == "WEIGHTS_0")
                    {
                        foreach (var v in kv.Value.AsVector4Array())
                        {

                            var Vertex = new SkeletalMeshVertex();
                            if (staticMeshVertices.Count > index)
                            {
                                Vertex = staticMeshVertices[index];
                            }
                            else
                            {
                                staticMeshVertices.Add(Vertex);
                            }
                            Vertex.BoneWeights = new Vector4 { X = v.X, Y = v.Y, Z = v.Z, W = v.W };
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
                //Boxes.Add(box);
                Meshes.Add(staticMeshVertices);

                List<uint> Indices = new List<uint>();
                foreach (var index in glPrimitive.IndexAccessor.AsIndicesArray())
                {
                    Indices.Add(index);
                }
                _IndicesList.Add(Indices);
                var Material = new Material();
                foreach (var glChannel in glPrimitive.Material.Channels)
                {
                    if (glChannel.Texture == null)
                        continue;
                    var texture = new Texture(glChannel.Texture.PrimaryImage.Content.Content.ToArray());
                    if (glChannel.Key == "BaseColor")
                    {
                        Material.Diffuse = texture;
                    }
                    if (glChannel.Key == "Normal")
                    {
                        Material.Normal = texture;
                    }
                }
                Materials.Add(Material);
            }
        }

        InitTBN();
        LoadBones(model);
    }
    private void InitTBN()
    {
        for (int i = 0; i < IndicesList.Count; i++)
        {
            InitMeshTBN(i);
        }
    }
    protected void LoadBones(ModelRoot model)
    {
        for(int i = 0; i < model.LogicalNodes.Count; i++)
        {

        }
    }


    private void InitMeshTBN(int index)
    {
        var vertics = Meshes[index];
        var indices = _IndicesList[index];

        for (int i = 0; i < indices.Count; i += 3)
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

    
}


public class BoneNode
{

}

public struct SkeletalMeshVertex
{
    public Vector3 Location;

    public Vector3 Normal;

    public Vector3 Tangent;

    public Vector3 BitTangent;

    public Vector3 Color;

    public Vector2 TexCoord;

    public Vector4 BoneIds;

    public Vector4 BoneWeights;
}