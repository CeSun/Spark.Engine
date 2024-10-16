﻿using System.Numerics;
using Jitter2.LinearMath;
using SharpGLTF.Schema2;
using Spark.Core;
using Spark.Core.Assets;
using Spark.Core.Shapes;
using Spark.Util;
using Material = Spark.Core.Assets.Material;
using Texture = Spark.Core.Assets.Texture;

namespace Spark.Importer;

public class StaticMeshImportSetting
{
    public bool ImporterPhysicsAsset { get; set; } = false;
}

public class SkeletalMeshImportSetting
{
    public string SkeletonAssetPath { get; set; } = string.Empty;

}
public static class MeshImporter
{

    public static void ImporterStaticMeshFromGlbStream(StreamReader streamReader, StaticMeshImportSetting staticMeshImportSetting, out List<Texture> textures, out List<Material> materials, out StaticMesh staticMesh)
    {
        textures = new List<Texture>();
        materials = new List<Material>();
        ModelRoot model = ModelRoot.ReadGLB(streamReader.BaseStream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        var sm = new StaticMesh();
        foreach (var node in model.LogicalNodes)
        {
            var glMesh = node.Mesh;
            if (glMesh == null)
                continue;
            var lod = new StaticMeshLod();
            var elements = lod.Elements;

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
                            var vertex = new StaticMeshVertex();
                            if (staticMeshVertices.Count > index)
                            {
                                vertex = staticMeshVertices[index];
                            }
                            else
                            {
                                staticMeshVertices.Add(vertex);
                            }
                            vertex.Location = new Vector3 { X = v.X, Y = v.Y, Z = v.Z };
                            if (staticMeshVertices.Count > index)
                            {
                            }
                            staticMeshVertices[index] = vertex;
                            index++;
                        }
                    }
                    if (kv.Key == "NORMAL")
                    {
                        foreach (var v in kv.Value.AsVector3Array())
                        {
                            var vertex = new StaticMeshVertex();
                            if (staticMeshVertices.Count > index)
                            {
                                vertex = staticMeshVertices[index];
                            }
                            else
                            {
                                staticMeshVertices.Add(vertex);
                            }
                            vertex.Normal = new Vector3 { X = v.X, Y = v.Y, Z = v.Z };
                            staticMeshVertices[index] = vertex;
                            index++;
                        }
                    }
                    if (kv.Key == "TEXCOORD_0")
                    {
                        foreach (var v in kv.Value.AsVector2Array())
                        {

                            var vertex = new StaticMeshVertex();
                            if (staticMeshVertices.Count > index)
                            {
                                vertex = staticMeshVertices[index];
                            }
                            else
                            {
                                staticMeshVertices.Add(vertex);
                            }
                            vertex.TexCoord = new Vector2 { X = v.X, Y = v.Y };
                            staticMeshVertices[index] = vertex;
                            index++;
                        }
                    }
                }
                List<uint> indices = [.. glPrimitive.IndexAccessor.AsIndicesArray()];
                var material = new Material()
                {
                    ShaderPath = "Engine/Shader/BasePassShader/BasePassShader.json",
                    BlendMode = glPrimitive.Material.Alpha switch
                    {
                        AlphaMode.OPAQUE => BlendMode.Opaque,
                        AlphaMode.MASK => BlendMode.Masked,
                        AlphaMode.BLEND => BlendMode.Translucent,
                        _ => BlendMode.Opaque
                    }
                };

                foreach (var glChannel in glPrimitive.Material.Channels)
                {
                    if (glChannel.Texture == null)
                        continue;
                    switch (glChannel.Key)
                    {
                        case "BaseColor":
                        case "Diffuse":
                            material.AddTexture("BaseColor", TextureImporter.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new() { IsGammaSpace = true }));
                            break;
                        case "Normal":
                            material.AddTexture("Normal", TextureImporter.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new()));
                            break;
                        case "MetallicRoughness":
                            var metallicRoughness = TextureImporter.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                            var (metalness, roughness) = TextureImporter.SplitMetallicRoughnessPbrTexture(metallicRoughness);
                            material.AddTexture("Metalness", metalness);
                            material.AddTexture("Roughness", roughness);
                            break;
                        case "Occlusion":
                            material.AddTexture("Occlusion", TextureImporter.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new()));
                            break;
                        default:
                            break;
                    }
                }
                InitMeshTbn(staticMeshVertices, indices);
                elements.Add(new Element<StaticMeshVertex>
                {
                    Vertices = staticMeshVertices,
                    Material = material,
                    Indices = indices,
                });
            }
            sm.StaticMeshLods.Add(lod);
        }
        if (sm.StaticMeshLods.Count > 0)
        {
            Box box = default;
            bool first = true;
            foreach (var element in sm.StaticMeshLods[0].Elements)
            {
                foreach (var vertex in element.Vertices)
                {
                    if (first == true)
                    {
                        box.Min = vertex.Location;
                        box.Max = vertex.Location;
                        first = false;
                    }
                    else
                    {
                        box += vertex.Location;
                    }
                }
            }
            sm.Box = box;
        } 
        staticMesh = sm;
        foreach (var lod in staticMesh.StaticMeshLods)
        {
            foreach( var element in lod.Elements)
            {
                if (element.Material != null)
                {
                    materials.Add(element.Material);
                    textures.AddRange(element.Material.Textures.Values);
                }
            }
        }
    }

    public static void ImporterSkeletalMeshFromGlbStream(StreamReader streamReader,
        SkeletalMeshImportSetting skeletalMeshImportSetting, out List<Texture> textures, out List<Material> materials, out List<AnimSequence> animSequences, out Skeleton skeleton, out SkeletalMesh skeletalMesh)

    {
        textures = new();
        materials = new();
        animSequences = new();
        Skeleton? tmpSkeleton = null;
        if (string.IsNullOrEmpty(skeletalMeshImportSetting.SkeletonAssetPath) == false)
        {
            // tmpSkeleton = engine.AssetMgr.Load<Skeleton>(skeletalMeshImportSetting.SkeletonAssetPath);
        }
        var model = ModelRoot.ReadGLB(streamReader.BaseStream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        SkeletalMesh sk = new SkeletalMesh();
        LoadVertices(sk, model);
        tmpSkeleton ??= LoadBones(model);
        sk.Skeleton = tmpSkeleton;
        var anims = LoadAnimSequence(model, tmpSkeleton);
        skeletalMesh = sk;
        skeleton = tmpSkeleton;
        foreach (var element in skeletalMesh.Elements)
        {
            if (element.Material != null)
            {
                materials.Add(element.Material);
                textures.AddRange(element.Material.Textures.Values);
            }
        }
        animSequences.AddRange(anims);
    }

    static List<AnimSequence> LoadAnimSequence(ModelRoot model, Skeleton skeleton)
    {
        Dictionary<int, int> node2Bone = new Dictionary<int, int>();
        for (int i = 0; i < model.LogicalSkins[0].JointsCount; i++)
        {
            var (logicalNode, _) = model.LogicalSkins[0].GetJoint(i);
            node2Bone.Add(logicalNode.LogicalIndex, i);
        }
        List<AnimSequence> list = [];
        foreach (var logicAnim in model.LogicalAnimations)
        {


            Dictionary<int, BoneChannel> dict = new Dictionary<int, BoneChannel>();

            foreach (var channel in logicAnim.Channels)
            {
                if (node2Bone.TryGetValue(channel.TargetNode.LogicalIndex, out var boneId) == false)
                    continue;
                if (!dict.TryGetValue(boneId, out var boneChannel))
                {
                    boneChannel = new BoneChannel
                    {
                        BoneId = boneId
                    };
                    dict.Add(boneId, boneChannel);
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

    static void LoadVertices(SkeletalMesh skeletalMesh, ModelRoot model)
    {
        List<Element<SkeletalMeshVertex>> elements = new();
        foreach (var glMesh in model.LogicalMeshes)
        {
            foreach (var glPrimitive in glMesh.Primitives)
            {
                List<SkeletalMeshVertex> skeletalMeshVertices = new List<SkeletalMeshVertex>();
                foreach (var kv in glPrimitive.VertexAccessors)
                {
                    int index = 0;
                    if (kv.Key == "POSITION")
                    {
                        foreach (var v in kv.Value.AsVector3Array())
                        {
                            var vertex = new SkeletalMeshVertex();
                            if (skeletalMeshVertices.Count > index)
                            {
                                vertex = skeletalMeshVertices[index];
                            }
                            else
                            {
                                skeletalMeshVertices.Add(vertex);
                            }
                            vertex.Location = new Vector3 { X = v.X, Y = v.Y, Z = v.Z };
                            if (skeletalMeshVertices.Count > index)
                            {
                            }
                            skeletalMeshVertices[index] = vertex;
                            index++;
                        }
                    }
                    else if (kv.Key == "NORMAL")
                    {
                        foreach (var v in kv.Value.AsVector3Array())
                        {
                            var vertex = new SkeletalMeshVertex();
                            if (skeletalMeshVertices.Count > index)
                            {
                                vertex = skeletalMeshVertices[index];
                            }
                            else
                            {
                                skeletalMeshVertices.Add(vertex);
                            }
                            vertex.Normal = new Vector3 { X = v.X, Y = v.Y, Z = v.Z };
                            skeletalMeshVertices[index] = vertex;
                            index++;
                        }
                    }
                    else if (kv.Key == "TEXCOORD_0")
                    {
                        foreach (var v in kv.Value.AsVector2Array())
                        {

                            var vertex = new SkeletalMeshVertex();
                            if (skeletalMeshVertices.Count > index)
                            {
                                vertex = skeletalMeshVertices[index];
                            }
                            else
                            {
                                skeletalMeshVertices.Add(vertex);
                            }
                            vertex.TexCoord = new Vector2 { X = v.X, Y = v.Y };
                            skeletalMeshVertices[index] = vertex;
                            index++;
                        }
                    }

                    else if (kv.Key == "JOINTS_0")
                    {
                        foreach (var v in kv.Value.AsVector4Array())
                        {
                            var vertex = new SkeletalMeshVertex();
                            if (skeletalMeshVertices.Count > index)
                            {
                                vertex = skeletalMeshVertices[index];
                            }
                            else
                            {
                                skeletalMeshVertices.Add(vertex);
                            }
                            vertex.BoneIds = new Vector4 { X = v.X, Y = v.Y, Z = v.Z, W = v.W };
                            skeletalMeshVertices[index] = vertex;
                            index++;
                        }
                    }

                    else if (kv.Key == "WEIGHTS_0")
                    {
                        foreach (var v in kv.Value.AsVector4Array())
                        {

                            var vertex = new SkeletalMeshVertex();
                            if (skeletalMeshVertices.Count > index)
                            {
                                vertex = skeletalMeshVertices[index];
                            }
                            else
                            {
                                skeletalMeshVertices.Add(vertex);
                            }
                            vertex.BoneWeights = new Vector4 { X = v.X, Y = v.Y, Z = v.Z, W = v.W };
                            skeletalMeshVertices[index] = vertex;

                            index++;
                        }
                    }
                }
                var indices = glPrimitive.IndexAccessor.AsIndicesArray().ToList();
                var material = new Material()
                {
                    ShaderPath = "Engine/Shader/BasePassShader/BasePassShader.json",
                    BlendMode = glPrimitive.Material.Alpha switch
                    {
                        AlphaMode.OPAQUE => BlendMode.Opaque,
                        AlphaMode.MASK => BlendMode.Masked,
                        AlphaMode.BLEND => BlendMode.Translucent,
                        _ => BlendMode.Opaque
                    }
                };
                foreach (var glChannel in glPrimitive.Material.Channels)
                {
                    if (glChannel.Texture == null)
                        continue;
                    switch (glChannel.Key)
                    {
                        case "BaseColor":
                        case "Diffuse":
                            material.AddTexture("BaseColor", TextureImporter.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new() { IsGammaSpace = true }));
                            break;
                        case "Normal":
                            material.AddTexture("Normal", TextureImporter.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new()));
                            break;
                        case "MetallicRoughness":
                            var metallicRoughness = TextureImporter.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                            var (metalness, roughness) = TextureImporter.SplitMetallicRoughnessPbrTexture(metallicRoughness);
                            material.AddTexture("Metalness", metalness);
                            material.AddTexture("Roughness", roughness);
                            break;
                        case "Occlusion":
                            material.AddTexture("Occlusion", TextureImporter.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new()));
                            break;
                        default:
                            break;
                    }
                }
                InitMeshTbn(skeletalMeshVertices, indices);
                elements.Add(new Element<SkeletalMeshVertex>
                {
                    Material = material,
                    Vertices = skeletalMeshVertices,
                    Indices = indices,
                });

            }
        }
        skeletalMesh.Elements = elements;
    }



    static Skeleton LoadBones(ModelRoot model)
    {
        List<BoneNode> boneList = new List<BoneNode>();
        List<Node> bone2Node = new List<Node>();
        Dictionary<int, BoneNode> node2Bone = new Dictionary<int, BoneNode>();
        for (int i = 0; i < model.LogicalSkins[0].JointsCount; i++)
        {
            var (logicalNode, inversMatrix) = model.LogicalSkins[0].GetJoint(i);
            var boneNode = new BoneNode
            {
                Name = logicalNode.Name,
                BoneId = i,
                RelativeScale = logicalNode.LocalMatrix.Scale(),
                RelativeLocation = logicalNode.LocalMatrix.Translation,
                RelativeRotation = logicalNode.LocalMatrix.Rotation(),
                RelativeTransform = logicalNode.LocalMatrix, //MatrixHelper.CreateTransform(BoneNode.RelativeLocation, BoneNode.RelativeRotation, BoneNode.RelativeScale);
                LocalToWorldTransform = logicalNode.WorldMatrix,
                WorldToLocalTransform = inversMatrix
            };
            boneList.Add(boneNode);
            bone2Node.Add(logicalNode);
            node2Bone.Add(logicalNode.LogicalIndex, boneNode);
        }
        foreach (BoneNode bone in boneList)
        {
            var node = bone2Node[bone.BoneId];
            if (node.VisualParent != null && node2Bone.TryGetValue(node.VisualParent.LogicalIndex, out var parentBone))
            {
                parentBone.ChildrenBone.Add(bone);
                bone.Parent = parentBone;
                bone.ParentId = parentBone.BoneId;
            }
        }
        List<BoneNode> treeRoots = new List<BoneNode>();
        foreach (BoneNode bone in boneList)
        {
            if (bone.ParentId < 0)
                treeRoots.Add(bone);
        }
        return new Skeleton
        {
            Root = treeRoots[0],
            BoneList = boneList,
            RootParentMatrix = bone2Node[treeRoots[0].BoneId].VisualParent.WorldMatrix
        };
    }

    private static void InitMeshTbn<T>(List<T> Vertices, List<uint> Indices) where T :IVertex
    {
        var indices = Indices;
        var vertices = Vertices;
        for (int i = 0; i < indices.Count - 2; i += 3)
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
}

