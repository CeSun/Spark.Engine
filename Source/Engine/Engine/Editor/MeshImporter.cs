﻿using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;
using Spark.Engine.Assets;
using Spark.Engine.Physics;
using Spark.Engine.Platform;
using Material = Spark.Engine.Assets.Material;
using Texture = Spark.Engine.Assets.Texture;

namespace Spark.Engine.Editor;

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
    public static void ImporterStaticMeshFromGlbFile(this Engine engine, string filePath, StaticMeshImportSetting staticMeshImportSetting, List<Texture> textures, List<Material> materials, out StaticMesh staticMesh)
    {
        using var sr = IFileSystem.Instance.GetStreamReader(filePath);
        ModelRoot model = ModelRoot.ReadGLB(sr.BaseStream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        var sm = new StaticMesh();
        foreach (var glMesh in model.LogicalMeshes)
        {
            if (glMesh == null)
                continue;
            if (glMesh.Name.IndexOf("Physics_", StringComparison.CurrentCultureIgnoreCase) < 0)
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
                    Box box = new Box();
                    if (staticMeshVertices.Count > 0)
                    {
                        box.MaxPoint = box.MinPoint = staticMeshVertices[0].Location;
                    }
                    foreach (var vertex in staticMeshVertices)
                    {
                        box += vertex.Location;
                    }
                    sm.Boxes.Add(box);
                    List<uint> indices = [.. glPrimitive.IndexAccessor.AsIndicesArray()];
                    var material = new Material();
                    Texture? metallicRoughness = null;
                    Texture? ambientOcclusion = null;

                    foreach (var glChannel in glPrimitive.Material.Channels)
                    {
                        if (glChannel.Texture == null)
                            continue;

                        if (glChannel.Key == "MetallicRoughness")
                        {
                            metallicRoughness = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                            continue;
                        }
                        if (glChannel.Key == "AmbientOcclusion")
                        {
                            ambientOcclusion = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                            continue;
                        }
                        if (glChannel.Key == "Parallax")
                        {
                            engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                            continue;
                        }

                        var texture = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                        switch (glChannel.Key)
                        {
                            case "BaseColor":
                            case "Diffuse":
                                material.BaseColor = texture;
                                break;
                            case "Normal":
                                material.Normal = texture;
                                break;
                        }
                    }
                    var arm = engine.MergePbrTexture(metallicRoughness, ambientOcclusion);
                    material.Arm = arm;
                    sm.Elements.Add(new Element<StaticMeshVertex>
                    {
                        Vertices = staticMeshVertices,
                        Material = material,
                        Indices = indices,
                        IndicesLen = (uint)indices.Count
                    });
                }
            }
            else if (staticMeshImportSetting.ImporterPhysicsAsset)
            {
                foreach (var glPrimitive in glMesh.Primitives)
                {
                    List<JTriangle> shapeSource = new List<JTriangle>();
                    var locations = glPrimitive.GetVertices("POSITION").AsVector3Array();
                    for (int i = 0; i < glPrimitive.GetIndices().Count; i += 3)
                    {
                        var index1 = (int)glPrimitive.GetIndices()[i];
                        var index2 = (int)glPrimitive.GetIndices()[i + 1];
                        var index3 = (int)glPrimitive.GetIndices()[i + 2];

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
                        shapeSource.Add(tri);
                    }
                    sm.Shapes.Add(new ConvexHullShape(shapeSource));
                }
            }
        }
        sm.InitTbn();
        sm.InitPhysics();
        engine.NextRenderFrame.Add(sm.InitRender);
        staticMesh = sm;
        foreach (var element in staticMesh.Elements)
        {
            materials.Add(element.Material);
            textures.AddRange(element.Material.Textures.OfType<Texture>());
        }
    }


    public static void ImporterSkeletalMeshFromGlbFile(this Engine engine, string filePath,
        SkeletalMeshImportSetting skeletalMeshImportSetting, List<Texture> textures, List<Material> materials,List<AnimSequence> animSequences,out Skeleton skeleton, out SkeletalMesh skeletalMesh)

    {
        Skeleton? tmpSkeleton = null;
        using var sr = IFileSystem.Instance.GetStreamReader(filePath);
        if (string.IsNullOrEmpty(skeletalMeshImportSetting.SkeletonAssetPath) == false)
        {
            tmpSkeleton = engine.AssetMgr.Load<Skeleton>(skeletalMeshImportSetting.SkeletonAssetPath);
        }
        var model = ModelRoot.ReadGLB(sr.BaseStream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        SkeletalMesh sk = new SkeletalMesh();
        LoadVertices(engine, sk, model);
        tmpSkeleton ??= LoadBones(model);
        sk.Skeleton = tmpSkeleton;
        var anims = LoadAnimSequence(model, tmpSkeleton);
        engine.NextRenderFrame.Add(sk.InitRender);
        skeletalMesh = sk;
        skeleton = tmpSkeleton;
        foreach (var element in skeletalMesh.Elements)
        {
            materials.Add(element.Material);
            textures.AddRange(element.Material.Textures.OfType<Texture>());
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

    static void LoadVertices(Engine engine, SkeletalMesh skeletalMesh, ModelRoot model)
    {
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
                Box box = new Box();
                if (skeletalMeshVertices.Count > 0)
                {
                    box.MaxPoint = box.MinPoint = skeletalMeshVertices[0].Location;
                }
                foreach (var vertex in skeletalMeshVertices)
                {
                    box += vertex.Location;
                }
                //Boxes.Add(box);
                //SkeletalMesh.Meshes.Add(staticMeshVertices);

                var indices = glPrimitive.IndexAccessor.AsIndicesArray().ToList();
                // SkeletalMesh._IndicesList.Add(Indices);
                var material = new Material();
                Texture? metallicRoughness = null;
                Texture? ambientOcclusion = null;
                Texture? parallax = null;

                foreach (var glChannel in glPrimitive.Material.Channels)
                {
                    if (glChannel.Texture == null)
                        continue;

                    if (glChannel.Key == "MetallicRoughness")
                    {
                        metallicRoughness = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new TextureImportSetting());
                        continue;
                    }
                    if (glChannel.Key == "AmbientOcclusion")
                    {
                        ambientOcclusion = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new TextureImportSetting());
                        continue;
                    }
                    if (glChannel.Key == "Parallax")
                    {
                        parallax = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new TextureImportSetting());
                        continue;
                    }
                    var texture = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                    switch (glChannel.Key)
                    {
                        case "BaseColor":
                        case "Diffuse":
                            material.BaseColor = texture;
                            break;
                        case "Normal":
                            material.Normal = texture;
                            break;
                    }

                }
                var custom = engine.MergePbrTexture(metallicRoughness, ambientOcclusion);
                material.Arm = custom;
                skeletalMesh.Elements.Add(new Element<SkeletalMeshVertex>
                {
                    Material = material,
                    Vertices = skeletalMeshVertices,
                    Indices = indices,
                    IndicesLen = (uint)indices.Count
                });

            }
        }
        skeletalMesh.InitTbn();
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
}

