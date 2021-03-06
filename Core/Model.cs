using Assimp;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;
using Quaternion = OpenTK.Mathematics.Quaternion;

namespace LiteEngine.Core
{
    public class Model : GameObject
    {
        public Model () : base("Model")
        {
            Animations = new Dictionary<string, Animation> ();
        }
        public override void Draw(double deltaTime)
        {
            base.Draw(deltaTime);
        }

        private delegate void ProcessNodeAction(Node parent, Node Current);
        public void LoadModel(string path, Skeleton skeleton = null)
        {
            AssimpContext context = new AssimpContext();
            var scene = context.ImportFile(path);
            if (skeleton == null)
                // 加载骨骼
                InitSkeleton(scene);
            else
                Skeleton = skeleton;
            // 加载网格
            InitMesh(scene);
            // 加载动画
            InitAnimation(scene);
        }

        // 先仅仅支持一下蒙皮骨骼动画吧
        private void InitAnimation(Assimp.Scene scene)
        {
            Console.WriteLine($"{scene.AnimationCount}");
            foreach(var anim in scene.Animations)
            {
                Animation animation = new Animation(anim.Name, anim.DurationInTicks);
                var mp = animation.Nodes;
                foreach (var channel in anim.NodeAnimationChannels)
                {
                    var animationNode = new AnimationNode(channel.NodeName);
                    foreach(var pos in channel.PositionKeys)
                    {
                        animationNode.PositionKeys.Add(new AnimationNodeKey<Vector3> { Time = pos.Time, Value = new Vector3(pos.Value.X, pos.Value.Y, pos.Value.Z) });
                    }
                    foreach(var scale in channel.ScalingKeys)
                    {
                        animationNode.ScaleKeys.Add(new AnimationNodeKey<Vector3> { Time = scale.Time, Value = new Vector3(scale.Value.X, scale.Value.Y, scale.Value.Z) });
                    }
                    foreach (var rotation in channel.RotationKeys)
                    {
                        animationNode.RotationKeys.Add(new AnimationNodeKey<Quaternion> { Time = rotation.Time, Value = new Quaternion(rotation.Value.X, rotation.Value.Y, rotation.Value.Z, rotation.Value.W) });
                    }
                    mp.Add(animationNode.Name, animationNode);
                }
                Animations.Add(animation.Name, animation);
            }
        }

        public Dictionary<string, Animation> Animations { get; set; }

        private void InitSkeleton(Assimp.Scene scene)
        {
            if (skeleton == null)  // 该模型内无骨骼，从模型信息内创建骨骼
            {
                BoneNode? Root = null;
                Dictionary<string, BoneNode> Bones = new Dictionary<string, BoneNode>();
                // 递归查找骨骼
                ProcessNode(scene.RootNode, (parentNode, currentNode) =>
                {
                    var bone = new BoneNode()
                    {
                        Name = currentNode.Name,
                        Parent = null,
                        LocalTransform = Tools.Cast2Matrix4(currentNode.Transform),
                        Id = Bones.Count,
                    };
                    if (Root == null)
                        Root = bone;
                    // 把骨骼加到
                    if (Bones.ContainsKey(bone.Name))
                        throw new Exception($"【{bone.Name}】骨骼重复");
                    Bones.Add(bone.Name, bone);
                    // 如果跟节点不是空的, 找到父节点，把自己加进去
                    if (parentNode != null)
                    {
                        var parent = Bones.GetValueOrDefault(parentNode.Name);
                        if (parent == null)
                            throw new Exception($"【{bone.Name}】的父骨骼没找到！");
                        bone.Parent = parent;
                        parent.Childern.Add(bone);
                    }
                });
                if (Root == null)
                    throw new Exception("没有找到骨骼");
                // 构建骨骼
                Skeleton = new Skeleton(Root, Bones);
            }
            // todo 检查骨骼，检查两次骨骼是否一致，不一致就抛异常 
        }
        private void ProcessNode(Node node, ProcessNodeAction action)
        {
            action(node.Parent, node);
            foreach (var child in node.Children)
            {
                ProcessNode(child, action);
            }
        }
        private void InitMesh(Assimp.Scene scene)
        {
            foreach(var aiMesh in scene.Meshes)
            {
                Dictionary<int, int> mp = new Dictionary<int, int>();
                List<Vertex> vertexs = new List<Vertex>();
                List<int> indices = new List<int>();
                Material material = new Material() { Shader = Shader.Default };
                for (int i = 0; i < aiMesh.VertexCount; i++)
                {
                    vertexs.Add(new Vertex { 
                        Position = new Vector3 { X = aiMesh.Vertices[i].X, Y = aiMesh.Vertices[i].Y, Z = aiMesh.Vertices[i].Z },
                        Normal = new Vector3 { X = aiMesh.Normals[i].X, Y= aiMesh.Normals[i].Y, Z = aiMesh.Normals[i].Z },
                        TexCoords = new Vector2 { X = aiMesh.TextureCoordinateChannels[0][i].X, Y = aiMesh.TextureCoordinateChannels[0][i].Y}
                    });
                }
                foreach(var face in aiMesh.Faces)
                {
                    foreach (var indice in face.Indices)
                    {
                        indices.Add(indice);
                    }
                }
                var aiMaterial = scene.Materials[aiMesh.MaterialIndex];
                aiMaterial.GetMaterialTextureCount(TextureType.Diffuse);
                TextureType[] types = new TextureType[] { TextureType.Diffuse , TextureType.Specular , TextureType.Normals , TextureType.Height };
                foreach (var type in types)
                {
                    foreach (var aiTexture in aiMaterial.GetMaterialTextures(type))
                    {
                        var texture = Texture.Load(aiTexture.FilePath);
                        material.Add(texture);
                    }
                }
                foreach(var aiBone in aiMesh.Bones)
                {
                    if (Skeleton == null)
                        throw new Exception("没有骨骼！");
                    if (Skeleton.Bones.TryGetValue(aiBone.Name, out var bone))
                    {
                        bone.OffsetTransform = Tools.Cast2Matrix4(aiBone.OffsetMatrix);
                        foreach (var weight in aiBone.VertexWeights)
                        {
                            var index = mp.GetValueOrDefault(weight.VertexID);
                            var vertex = vertexs[weight.VertexID];
                            vertex.Bones[index] = bone.Id;
                            vertex.Weights[index] = weight.Weight;
                            vertexs[weight.VertexID] = vertex;
                            mp[weight.VertexID] = index + 1;
                        }
                    }
                    else
                    {
                        throw new Exception($"没有找到这个骨头！{aiBone.Name}");
                    }

                    // bone.
                }
                var mesh = new Mesh(vertexs, indices, material);
                mesh.Owner = this;
            }
        }
        Skeleton? skeleton;

        public Skeleton? Skeleton { get => skeleton; set => skeleton = value; }

    }

    public class Skeleton
    {
        public Skeleton(BoneNode root, Dictionary<string, BoneNode> bones)
        {
            Root = root;
            _Bones = bones;
            Transform = new Matrix4[bones.Count];
            ProcessMat();
        }
        public BoneNode Root { get; set; }
        public Dictionary<string, BoneNode> Bones { get => _Bones; }
        private Dictionary<string, BoneNode> _Bones = new Dictionary<string, BoneNode>();
        public Matrix4[] Transform;
       
        private void ProcessMat()
        {
            ProcessNode(Root, node => {
                Matrix4 mat = Matrix4.Identity;
                if (node.Parent != null)
                {
                    mat = node.Parent.MiddleTransform;
                }
                node.MiddleTransform = node.LocalTransform * mat;
                Transform[node.Id] = node.OffsetTransform * node.MiddleTransform;
            });
        }

        private void ProcessNode(BoneNode bone, Action<BoneNode> action)
        {
            action(bone);

            foreach(var child in bone.Childern)
            {
                ProcessNode(child, action);
            }
        }
    }
    public class BoneNode
    {
        public int Id;
        // 骨骼名字
        public string Name = "";
        // 父节点
        public BoneNode? Parent;
        // 本地矩阵
        public Matrix4 LocalTransform;
        // 顶点转换到骨骼空间的矩阵
        public Matrix4 OffsetTransform;
        public Matrix4 MiddleTransform;

        // 子节点
        public List<BoneNode> Childern {
            get { return _Childern; }
        }
        private List<BoneNode> _Childern = new List<BoneNode>();

        public BoneNode()
        {
            Parent = null;
            LocalTransform = Matrix4.Zero;
            OffsetTransform = Matrix4.Identity;
        }

    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex 
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoords;
        public Vector4i Bones;
        public Vector4 Weights;
    }

   

}
namespace OpenTK.Mathematics
{
   
    public class Tools 
    {
        static public Matrix4 Cast2Matrix4(Matrix4x4 mat)
        {
            return new Matrix4
            {
                M11 = mat.A1,
                M12 = mat.B1,
                M13 = mat.C1,
                M14 = mat.D1,
                M21 = mat.A2,
                M22 = mat.B2,
                M23 = mat.C2,
                M24 = mat.D2,
                M31 = mat.A3,
                M32 = mat.B3,
                M33 = mat.C3,
                M34 = mat.D3,
                M41 = mat.A4,
                M42 = mat.B4,
                M43 = mat.C4,
                M44 = mat.D4,
            };
        }

    }

}