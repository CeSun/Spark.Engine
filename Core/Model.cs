using Assimp;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace LiteEngine.Core
{
    public class Model
    {
        public void Draw(double deltaTime)
        {
            foreach(var mesh in meshes)
            {
                mesh.Draw(deltaTime);
            }
        }

        private delegate void ProcessNodeAction(Node parent, Node Current);
        public void LoadModel(string path)
        {
            AssimpContext context = new AssimpContext();
            var scene = context.ImportFile(path);
            InitSkeleton(scene);
            InitMesh(scene);

        }
        private void InitSkeleton(Assimp.Scene scene)
        {
            if (skeleton == null)  // 该模型内无骨骼，从模型信息内创建骨骼
            {
                ProcessNode(scene.RootNode, (parentNode, currentNode) =>
                {
                    var bone = new BoneNode()
                    {
                        Name = currentNode.Name,
                        Parent = null,
                        LocalTransform = Tools.Cast2Matrix4(currentNode.Transform),
                    };
                    if (skeleton == null)
                    {
                        // 如果骨骼是空的，那么创建骨骼
                        skeleton = new Skeleton(bone);
                    }
                    else
                    {
                        // 把骨骼加到
                        if (skeleton.Bones.ContainsKey(bone.Name))
                            throw new Exception($"【{bone.Name}】骨骼重复");
                        skeleton.Bones[bone.Name] = bone;
                        // 如果跟节点不是空的, 找到父节点，把自己加进去
                        var parent = skeleton.Bones.GetValueOrDefault(parentNode.Name);
                        if (parent == null)
                            throw new Exception($"【{bone.Name}】的父骨骼没找到！");
                        bone.Parent = parent;
                        parent.Childern.Add(bone);
                    }
                });
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
                List<Vertex> vertex = new List<Vertex>();
                List<int> indices = new List<int>();
                Material material = new Material();
                for (int i = 0; i < aiMesh.VertexCount; i++)
                {
                    vertex.Add(new Vertex { 
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
                        var texture =  Texture.Load(aiTexture.FilePath);
                        material.Add(texture);
                    }
                }
                var mesh = new Mesh(vertex, indices, material);
                meshes.Add(mesh);
            }
        }
        Skeleton? skeleton;

        List<Mesh> meshes = new List<Mesh>();
    }

    public class Skeleton
    {
        public Skeleton(BoneNode root)
        {
            Root = root;
            Bones[root.Name] = root;
        }
        public BoneNode Root { get; set; }
        public Dictionary<string, BoneNode> Bones { get => _Bones; }
        private Dictionary<string, BoneNode> _Bones = new Dictionary<string, BoneNode>();

    }
    public class BoneNode
    {
        // 骨骼名字
        public string Name = "";
        // 父节点
        public BoneNode? Parent;
        // 本地矩阵
        public Matrix4 LocalTransform;
        // 子节点
        public List<BoneNode> Childern {
            get { return _Childern; }
        }
        private List<BoneNode> _Childern = new List<BoneNode>();

    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex 
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TexCoords;
    }

   

}
namespace OpenTK.Mathematics
{
    public class Tools 
    {
        static public Matrix4 Cast2Matrix4(Assimp.Matrix4x4 mat)
        {
            return new Matrix4
            {
                M11 = mat.A1,
                M12 = mat.A2,
                M13 = mat.A3,
                M14 = mat.A4,
                M21 = mat.B1,
                M22 = mat.B2,
                M23 = mat.B3,
                M24 = mat.B4,
                M31 = mat.C1,
                M32 = mat.C2,
                M33 = mat.C3,
                M34 = mat.C4,
                M41 = mat.D1,
                M42 = mat.D2,
                M43 = mat.D3,
                M44 = mat.C4,
            };
        }

    }


}