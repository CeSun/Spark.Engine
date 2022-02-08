using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assimp;
using OpenTK.Mathematics;
namespace LiteEngine.Core
{
    public class ModelLoader
    {
        public static void Load(string path)
        {
            
            AssimpContext ctx = new AssimpContext();
            var scene = ctx.ImportFile(path);
            if (scene == null)
                throw new Exception($"没有找到模型: {path}");

            Dictionary<int, int> weightNum = new Dictionary<int, int>();
            foreach (var aiMesh in scene.Meshes)
            {
                weightNum.Clear();
                List<Vertex> vertices = new List<Vertex>();
                for (int i = 0; i < aiMesh.VertexCount; i ++)
                {
                    vertices.Add(new Vertex { 
                        Position = new Vector3(aiMesh.Vertices[i].X, aiMesh.Vertices[i].Y, aiMesh.Vertices[i].Z) ,
                        Normal = new Vector3(aiMesh.Normals[i].X, aiMesh.Normals[i].Y, aiMesh.Normals[i].Z),
                        TexCoords = new Vector2(aiMesh.TextureCoordinateChannels[0][i].X, aiMesh.TextureCoordinateChannels[0][i].Y),
                    });
                }
                foreach(var aiBone in aiMesh.Bones)
                {
                    foreach(var aiWeight in aiBone.VertexWeights)
                    {
                        var num = weightNum.GetValueOrDefault(aiWeight.VertexID);
                        var vertex = vertices[num];
                        // vertex.Bones[num] = aiBone.VertexID;
                    }
                }

            }
            ProcessNode(scene.RootNode, node => {
                
            
            });

        }
        private static void ProcessNode(Node node, Action<Node> action)
        {
            if (node == null)
                throw new Exception($"node is null");
            action(node);

            foreach(var child in node.Children)
            {
                ProcessNode(child, action);
            }
        }
        
    }
}
