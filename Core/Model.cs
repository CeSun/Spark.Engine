using Assimp;
using GlmSharp;
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
        private void InitSkeleton(Scene scene)
        {
            if (skeleton == null)  // 该模型内无骨骼，从模型信息内创建骨骼
            {
                skeleton = new Skeleton();
                ProcessNode(scene.RootNode, (parentNode, currentNode) =>
                {
                    var bone = new BoneNode()
                    {
                        Name = currentNode.Name,
                        Parent = null,
                        LocalTransform = Tools.Cast2GlmMat4(currentNode.Transform),
                    };
                    // 把骨骼加到
                    if (skeleton.Bones.ContainsKey(bone.Name))
                        throw new Exception($"【{bone.Name}】骨骼重复");
                    skeleton.Bones[bone.Name] = bone;
                    if (parentNode == null)
                    {
                        // 如果跟节点是空的, 那么就把当前节点作为根节点
                        skeleton.Root = bone;
                    }
                    else
                    {
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
        private void InitMesh(Scene scene)
        {
            foreach(var aiMesh in scene.Meshes)
            {
                List<Vertex> vertex = new List<Vertex>();
                List<int> indices = new List<int>();
                Material material = new Material();
                for (int i = 0; i < aiMesh.VertexCount; i++)
                {
                    vertex.Add(new Vertex { 
                        Position = new vec3 { x = aiMesh.Vertices[i].X, y = aiMesh.Vertices[i].Y, z = aiMesh.Vertices[i].Z },
                        Normal = new vec3 { x = aiMesh.Normals[i].X, y = aiMesh.Normals[i].Y, z = aiMesh.Normals[i].Z },
                        TexCoords = new vec2 { x = aiMesh.TextureCoordinateChannels[0][i].X, y = aiMesh.TextureCoordinateChannels[0][i].Y}
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
        public mat4 LocalTransform;
        // 子节点
        public List<BoneNode> Childern {
            get { return _Childern; }
        }
        private List<BoneNode> _Childern = new List<BoneNode>();

    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex 
    {
        public vec3 Position;
        public vec3 Normal;
        public vec2 TexCoords;
    }

   

}
namespace GlmSharp {
    public class Tools 
    {
        static public mat4 Cast2GlmMat4(Assimp.Matrix4x4 mat)
        {
            return new mat4
            {
                m00 = mat.A1,
                m01 = mat.A2,
                m02 = mat.A3,
                m03 = mat.A4,
                m10 = mat.B1,
                m11 = mat.B2,
                m12 = mat.B3,
                m13 = mat.B4,
                m20 = mat.C1,
                m21 = mat.C2,
                m22 = mat.C3,
                m23 = mat.C4,
                m30 = mat.D1,
                m31 = mat.D2,
                m32 = mat.D3,
                m33 = mat.D4,
            };
        }

    }


}