using SharpGLTF.Schema2;
using Spark.Engine.Physics;
using System.Numerics;
using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;
using Spark.Engine.Platform;


namespace Spark.Engine.Assets;

public partial class SkeletalMesh : AssetBase
{
    private readonly List<Element<SkeletalMeshVertex>> _elements = [];
    public IReadOnlyList<Element<SkeletalMeshVertex>> Elements => _elements;

    public Skeleton? Skeleton { get; set; }

    public unsafe void InitRender(GL gl)
    {

        for (var index = 0; index < _elements.Count; index++)
        {
            if (Elements[index].VertexArrayObjectIndex > 0)
                continue;
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ebo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (SkeletalMeshVertex* p = CollectionsMarshal.AsSpan(_elements[index].Vertices))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(_elements[index].Vertices.Count * sizeof(SkeletalMeshVertex)), p, GLEnum.StaticDraw);
            }
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            fixed (uint* p = CollectionsMarshal.AsSpan(_elements[index].Indices))
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(_elements[index].Indices.Count * sizeof(uint)), p, GLEnum.StaticDraw);
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
            _elements[index].VertexArrayObjectIndex = vao;
            _elements[index].VertexBufferObjectIndex = vbo;
            _elements[index].ElementBufferObjectIndex = ebo;
        }
        ReleaseMemory();
    }
   
   


 

    
}

public partial class SkeletalMesh
{
    public static List<AssetBase> ImportFromGlb(string path)
    {
        using var sr = IFileSystem.Instance.GetStreamReader( path);
        return ImportFromGlb(sr.BaseStream);
    }


    public static async Task<List<AssetBase>> ImportFromGlbAsync(string path)
    {
        using var sr = IFileSystem.Instance.GetStreamReader( path);
        return await ImportFromGlbAsync(sr.BaseStream);
    }

    public static async Task<List<AssetBase>> ImportFromGlbAsync(Stream stream)
    {
        SkeletalMesh sk = new SkeletalMesh();
        ModelRoot? model;
        Skeleton? skeleton = null;
        List<AnimSequence>? anims = null;
        await Task.Run(() =>
        {
            model = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });
            if (model == null)
                throw new Exception("加载GLB失败");
            LoadVertics(sk, model);
            skeleton = LoadBones(model);
            sk.Skeleton = skeleton;
            anims = LoadAnimSequence(model, skeleton);
        });

        if (sk == null)
            throw new Exception("sk load error");
        if (skeleton == null)
            throw new Exception("skeleton load error");
        if (anims == null)
            throw new Exception("anims load error");
        List<AssetBase> assets = new List<AssetBase>();

        foreach (var element in sk.Elements)
        {
            foreach (var texture in element.Material.Textures)
            {
                if (texture == null)
                    continue;
                assets.Add(texture);
            }
            assets.Add(element.Material);
        }
        assets.Add(skeleton);
        assets.AddRange(anims);
        assets.Add(sk);
        return assets;
    }


    public static List<AssetBase> ImportFromGlb(Stream stream)
    {
        SkeletalMesh sk = new SkeletalMesh();
        var model = ModelRoot.ReadGLB(stream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix});
        LoadVertics(sk, model);
        var skeleton = LoadBones(model);
        sk.Skeleton = skeleton;
        var anims = LoadAnimSequence(model, skeleton);
        List<AssetBase> assets = new List<AssetBase>();

        foreach(var element in sk.Elements)
        {
            foreach(var texture in element.Material.Textures)
            {
                if (texture == null)
                    continue;
                assets.Add(texture);
            }
            assets.Add(element.Material);
        }
        assets.Add(skeleton);
        assets.AddRange(anims);
        assets.Add(sk);
        return assets;
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
        foreach(var logicAnim in model.LogicalAnimations)
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

    static void LoadVertics(SkeletalMesh skeletalMesh, ModelRoot model )
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

                List<uint> indices = new List<uint>();
                foreach (var index in glPrimitive.IndexAccessor.AsIndicesArray())
                {
                    indices.Add(index);
                }
                // SkeletalMesh._IndicesList.Add(Indices);
                var material = new Material();
                byte[]? metallicRoughness = null;
                byte[]? ambientOcclusion = null;

                foreach (var glChannel in glPrimitive.Material.Channels)
                {
                    if (glChannel.Texture == null)
                        continue;

                    if (glChannel.Key == "MetallicRoughness")
                    {
                        metallicRoughness = glChannel.Texture.PrimaryImage.Content.Content.ToArray();
                        continue;
                    }
                    if (glChannel.Key == "AmbientOcclusion")
                    {
                        ambientOcclusion = glChannel.Texture.PrimaryImage.Content.Content.ToArray();
                        continue;
                    }
                    if (glChannel.Key == "Parallax")
                    {

                        glChannel.Texture.PrimaryImage.Content.Content.ToArray();
                        continue;
                    }

                    var texture = Texture.LoadFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray());
                    if (glChannel.Key == "BaseColor" || glChannel.Key == "Diffuse")
                    {
                        material.BaseColor = texture;
                    }
                    if (glChannel.Key == "Normal")
                    {
                        material.Normal = texture;
                    }

                }

                var custom = Texture.LoadPbrTexture(metallicRoughness, ambientOcclusion);
                material.Arm = custom;
                //SkeletalMesh.Materials.Add(Material);
                skeletalMesh._elements.Add(new Element<SkeletalMeshVertex>
                {
                    Material = material,
                    Vertices = skeletalMeshVertices,
                    Indices = indices,
                    IndicesLen = (uint)indices.Count
                });

            }
        }
        InitTbn(skeletalMesh);
    }

    private static void InitTbn(SkeletalMesh skeletalMesh)
    {
        for (int i = 0; i < skeletalMesh.Elements.Count; i++)
        {
            InitMeshTbn(skeletalMesh, i);
        }
    }
    private static void InitMeshTbn(SkeletalMesh skeletalMesh, int index)
    {
        var vertics = skeletalMesh.Elements[index].Vertices;
        var indices = skeletalMesh.Elements[index].Indices;

        for (int i = 0; i < indices.Count; i += 3)
        {
            var p1 = vertics[(int)indices[i]];
            var p2 = vertics[(int)indices[i + 1]];
            var p3 = vertics[(int)indices[i + 2]];

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

            vertics[(int)indices[i]] = p1;
            vertics[(int)indices[i + 1]] = p2;
            vertics[(int)indices[i + 2]] = p3;

        }

    }

    protected static Skeleton LoadBones(ModelRoot model)
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
            if (node.VisualParent != null  && node2Bone.TryGetValue(node.VisualParent.LogicalIndex, out var parentBone))
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
            RootParentMatrix =  bone2Node[treeRoots[0].BoneId].VisualParent.WorldMatrix
        };
    }

    public static void ProcessBoneTransform(BoneNode bone)
    {

        if (bone.Parent != null)
        {
            bone.LocalToWorldTransform = bone.RelativeTransform * bone.Parent.LocalToWorldTransform;
        }
        else
        {
            bone.LocalToWorldTransform = bone.RelativeTransform;
        }

        if(Matrix4x4.Invert(bone.LocalToWorldTransform, out bone.WorldToLocalTransform) )
        {

        }
        foreach(var child in bone.ChildrenBone)
        {
            ProcessBoneTransform(child);
        }
    }
    public void ReleaseMemory()
    {
        foreach (var element in Elements)
        {
            element.Vertices = null;
            element.Indices = null;
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
            _elements.Add(element);
        }
        Skeleton = ISerializable.AssetDeserialize<Skeleton>(br, engine);


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