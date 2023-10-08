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
using Silk.NET.OpenGL;
using System.Runtime.InteropServices;

namespace Spark.Engine.Assets;

public partial class SkeletalMesh
{


    List<Element<SkeletalMeshVertex>> _Elements = new List<Element<SkeletalMeshVertex>>();
    public IReadOnlyList<Element<SkeletalMeshVertex>> Elements => _Elements;

    List<JVector> ConvexHullSourceData = new List<JVector>();
    

    public Skeleton? Skeleton { get; set; }
    public SkeletalMesh()
    {
    }

    public unsafe void InitRender()
    {

        for (var index = 0; index < _Elements.Count; index++)
        {
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ebo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (SkeletalMeshVertex* p = CollectionsMarshal.AsSpan(_Elements[index].Vertices))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(_Elements[index].Vertices.Count * sizeof(SkeletalMeshVertex)), p, GLEnum.StaticDraw);
            }
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            fixed (uint* p = CollectionsMarshal.AsSpan(_Elements[index].Indices))
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(_Elements[index].Indices.Count * sizeof(uint)), p, GLEnum.StaticDraw);
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
            _Elements[index].VertexArrayObjectIndex = vao;
            _Elements[index].VertexBufferObjectIndex = vbo;
            _Elements[index].ElementBufferObjectIndex = ebo;
        }
    }
   
   


 

    
}

public partial class SkeletalMesh
{
    public static (SkeletalMesh, Skeleton, List<AnimSequence>) ImportFromGLB(string Path)
    {
        using var sr = FileSystem.GetStream("Content" + Path);
        return ImportFromGLB(sr);
    }
    public static (SkeletalMesh, Skeleton, List<AnimSequence>) ImportFromGLB(Stream stream)
    {
        SkeletalMesh sk = new SkeletalMesh();
        var model = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix});
        LoadVertics(sk, model);
        var skeleton = LoadBones(model);
        sk.Skeleton = skeleton;
        var anims = LoadAnimSequence(model, skeleton);
        sk.InitRender();
        return (sk, skeleton, anims);
    }

    static List<AnimSequence> LoadAnimSequence(ModelRoot model, Skeleton skeleton)
    {
        List<AnimSequence> list = new List<AnimSequence>();
        foreach(var logicAnim in model.LogicalAnimations)
        {
            
            Dictionary<int, BoneChannel> dict = new Dictionary<int, BoneChannel>();
            
            foreach (var channel in logicAnim.Channels)
            {
                if(!dict.TryGetValue(channel.TargetNode.LogicalIndex, out var boneChannel))
                {
                    boneChannel = new BoneChannel();
                    boneChannel.BoneId = channel.TargetNode.LogicalIndex;
                    dict.Add(channel.TargetNode.LogicalIndex, boneChannel);
                }

                if (channel.GetTranslationSampler() != null)
                {
                    var translations = channel.GetTranslationSampler().GetLinearKeys();
                    boneChannel.Translation.AddRange(translations);
                }
                if (channel.GetRotationSampler() != null)
                {
                    var rotations = channel.GetRotationSampler().GetLinearKeys();
                    boneChannel.Rotation.AddRange(rotations);
                }
                if (channel.GetScaleSampler() != null)
                {
                    var scales = channel.GetScaleSampler().GetLinearKeys();
                    boneChannel.Scale.AddRange(scales);
                }

            }
            var anim = new AnimSequence(logicAnim.Name, logicAnim.Duration, skeleton, dict)
            {
                AnimName = logicAnim.Name
            };
            list.Add(anim);
        }
        return list;
    }

    static void LoadVertics(SkeletalMesh SkeletalMesh, ModelRoot model )
    {
        foreach (var glMesh in model.LogicalMeshes)
        {
            foreach (var glPrimitive in glMesh.Primitives)
            {
                List<SkeletalMeshVertex> SkeletalMeshVertices = new List<SkeletalMeshVertex>();
                foreach (var kv in glPrimitive.VertexAccessors)
                {
                    int index = 0;
                    if (kv.Key == "POSITION")
                    {
                        foreach (var v in kv.Value.AsVector3Array())
                        {
                            var Vertex = new SkeletalMeshVertex();
                            if (SkeletalMeshVertices.Count > index)
                            {
                                Vertex = SkeletalMeshVertices[index];
                            }
                            else
                            {
                                SkeletalMeshVertices.Add(Vertex);
                            }
                            Vertex.Location = new Vector3 { X = v.X, Y = v.Y, Z = v.Z };
                            if (SkeletalMeshVertices.Count > index)
                            {
                            }
                            SkeletalMeshVertices[index] = Vertex;
                            index++;
                        }
                    }
                    else if (kv.Key == "NORMAL")
                    {
                        foreach (var v in kv.Value.AsVector3Array())
                        {
                            var Vertex = new SkeletalMeshVertex();
                            if (SkeletalMeshVertices.Count > index)
                            {
                                Vertex = SkeletalMeshVertices[index];
                            }
                            else
                            {
                                SkeletalMeshVertices.Add(Vertex);
                            }
                            Vertex.Normal = new Vector3 { X = v.X, Y = v.Y, Z = v.Z };
                            SkeletalMeshVertices[index] = Vertex;
                            index++;
                        }
                    }
                    else if (kv.Key == "TEXCOORD_0")
                    {
                        foreach (var v in kv.Value.AsVector2Array())
                        {

                            var Vertex = new SkeletalMeshVertex();
                            if (SkeletalMeshVertices.Count > index)
                            {
                                Vertex = SkeletalMeshVertices[index];
                            }
                            else
                            {
                                SkeletalMeshVertices.Add(Vertex);
                            }
                            Vertex.TexCoord = new Vector2 { X = v.X, Y = v.Y };
                            SkeletalMeshVertices[index] = Vertex;
                            index++;
                        }
                    }

                    else if (kv.Key == "JOINTS_0")
                    {
                        foreach (var v in kv.Value.AsVector4Array())
                        {
                            var Vertex = new SkeletalMeshVertex();
                            if (SkeletalMeshVertices.Count > index)
                            {
                                Vertex = SkeletalMeshVertices[index];
                            }
                            else
                            {
                                SkeletalMeshVertices.Add(Vertex);
                            }
                            Vertex.BoneIds = new Vector4 { X = v.X, Y = v.Y, Z = v.Z, W = v.W };
                            SkeletalMeshVertices[index] = Vertex;
                            index++;
                        }
                    }

                    else if (kv.Key == "WEIGHTS_0")
                    {
                        foreach (var v in kv.Value.AsVector4Array())
                        {

                            var Vertex = new SkeletalMeshVertex();
                            if (SkeletalMeshVertices.Count > index)
                            {
                                Vertex = SkeletalMeshVertices[index];
                            }
                            else
                            {
                                SkeletalMeshVertices.Add(Vertex);
                            }
                            Vertex.BoneWeights = new Vector4 { X = v.X, Y = v.Y, Z = v.Z, W = v.W };
                            SkeletalMeshVertices[index] = Vertex;

                            index++;
                        }
                    }
                }
                Box box = new Box();
                if (SkeletalMeshVertices.Count > 0)
                {
                    box.MaxPoint = box.MinPoint = SkeletalMeshVertices[0].Location;
                }
                foreach (var Vertex in SkeletalMeshVertices)
                {
                    box += Vertex.Location;
                }
                //Boxes.Add(box);
                //SkeletalMesh.Meshes.Add(staticMeshVertices);

                List<uint> Indices = new List<uint>();
                foreach (var index in glPrimitive.IndexAccessor.AsIndicesArray())
                {
                    Indices.Add(index);
                }
                // SkeletalMesh._IndicesList.Add(Indices);
                var Material = new Material();
                foreach (var glChannel in glPrimitive.Material.Channels)
                {
                    if (glChannel.Texture == null)
                        continue;
                    var texture = new Texture(glChannel.Texture.PrimaryImage.Content.Content.ToArray());
                    if (glChannel.Key == "BaseColor" || glChannel.Key == "Diffuse")
                    {
                        Material.Diffuse = texture;
                    }
                    if (glChannel.Key == "Normal")
                    {
                        Material.Normal = texture;
                    }
                }
                //SkeletalMesh.Materials.Add(Material);
                SkeletalMesh._Elements.Add(new Element<SkeletalMeshVertex>
                {
                    Material = Material,
                    Vertices = SkeletalMeshVertices,
                    Indices = Indices
                });

            }
        }
        InitTBN(SkeletalMesh);
    }

    private static void InitTBN(SkeletalMesh SkeletalMesh)
    {
        for (int i = 0; i < SkeletalMesh.Elements.Count; i++)
        {
            InitMeshTBN(SkeletalMesh, i);
        }
    }
    private static void InitMeshTBN(SkeletalMesh SkeletalMesh, int index)
    {
        var vertics = SkeletalMesh.Elements[index].Vertices;
        var indices = SkeletalMesh.Elements[index].Indices;

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

    protected static Skeleton LoadBones(ModelRoot model)
    {
        List<BoneNode> BoneList = new List<BoneNode>();
        for (int i = 0; i < model.LogicalNodes.Count; i++)
        {
            var LogicalNode = model.LogicalNodes[i];
            var BoneNode = new BoneNode()
            {
                Name = LogicalNode.Name
            };
            BoneNode.BoneId = LogicalNode.LogicalIndex;

            BoneNode.RelativeScale = LogicalNode.LocalMatrix.Scale();
            BoneNode.RelativeLocation = LogicalNode.LocalMatrix.Translation;
            BoneNode.RelativeRotation = LogicalNode.LocalMatrix.Rotation();
            BoneNode.RelativeTransform = LogicalNode.LocalMatrix;//MatrixHelper.CreateTransform(BoneNode.RelativeLocation, BoneNode.RelativeRotation, BoneNode.RelativeScale);
            BoneNode.LocalToWorldTransform = LogicalNode.WorldMatrix;
            Matrix4x4.Invert(BoneNode.LocalToWorldTransform, out BoneNode.WorldToLocalTransform);
            if (LogicalNode.VisualParent != null)
            {
                BoneNode.ParentId = LogicalNode.VisualParent.LogicalIndex;
            }

            BoneList.Add(BoneNode);
        }
        foreach (BoneNode Bone in BoneList)
        {
            if (Bone.ParentId >= 0)
            {
                Bone.Parent = BoneList[Bone.ParentId];
                Bone.Parent.ChildrenBone.Add(Bone);
            }
        }
        List<BoneNode> TreeRoots = new List<BoneNode>();
        foreach (BoneNode Bone in BoneList)
        {
            if (Bone.ParentId < 0)
                TreeRoots.Add(Bone);
        }

       // ProcessBoneTransform(TreeRoots[0]);
        return new Skeleton(TreeRoots[0], BoneList);
    }

    public static void ProcessBoneTransform(BoneNode Bone)
    {

        if (Bone.Parent != null)
        {
            Bone.LocalToWorldTransform = Bone.RelativeTransform * Bone.Parent.LocalToWorldTransform;
        }
        else
        {
            Bone.LocalToWorldTransform = Bone.RelativeTransform;
        }

        if(Matrix4x4.Invert(Bone.LocalToWorldTransform, out Bone.WorldToLocalTransform) )
        {

        }
        foreach(var child in Bone.ChildrenBone)
        {
            ProcessBoneTransform(child);
        }
    }
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