using Spark.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpGLTF;
using SharpGLTF.Schema2;
using System.Numerics;
using Material = Spark.Engine.Assets.Material;

namespace Spark.Engine.Util.Importers;

public static class StaticMeshImporter
{
    public static StaticMesh ImportFromGLTF(Stream stream)
    {
        var StaticMesh = new StaticMesh();
        var model = ModelRoot.ReadGLB(stream);
        foreach (var mesh in model.LogicalMeshes)
        {
           
            foreach (var Primitive in mesh.Primitives)
            {
                if (Primitive.DrawPrimitiveType != PrimitiveType.TRIANGLES)
                    continue;
                var sector = new StaticMesh.Sector()
                {
                    Indices = new List<uint>(),
                    Vertices = new List<StaticMesh.Vertex>(),
                    Material = new Assets.Material(),
                };
                foreach (var (name, values) in Primitive.VertexAccessors)
                {
                    if(name == "POSITION")
                    {
                        int index = 0;
                        foreach (var value in values.AsVector3Array())
                        {
                            if (sector.Vertices.Count <= index)
                            {
                                sector.Vertices.Add(new StaticMesh.Vertex());
                            }
                            var vertex = sector.Vertices[index];
                            vertex.Position = new Vector3(value.X, value.Y, value.Z);
                            sector.Vertices[index] = vertex;
                            index++;
                        }
                    }
                    if (name == "NORMAL")
                    {
                        int index = 0;
                        foreach (var value in values.AsVector3Array())
                        {
                            if (sector.Vertices.Count <= index)
                            {
                                sector.Vertices.Add(new StaticMesh.Vertex());
                            }
                            var vertex = sector.Vertices[index];
                            vertex.Normal = new Vector3(value.X, value.Y, value.Z);
                            sector.Vertices[index] = vertex;
                            index++;
                        }
                    }
                    if (name == "TEXCOORD_0")
                    {
                        int index = 0;
                        foreach (var value in values.AsVector2Array())
                        {
                            if (sector.Vertices.Count <= index)
                            {
                                sector.Vertices.Add(new StaticMesh.Vertex());
                            }
                            var vertex = sector.Vertices[index];
                            vertex.TexCoord = new Vector2(value.X, value.Y);
                            sector.Vertices[index] = vertex;
                            index++;
                        }
                    }
                }
                sector.Indices = Primitive.IndexAccessor.AsIndicesArray().ToList();

                var material = new Material();
                foreach (var channel in Primitive.Material.Channels)
                {
                    if (channel.Texture == null)
                        continue;
                    var name = channel.Key;
                    var image = channel.Texture.PrimaryImage.Content.Content.ToArray();
                    material.Textures.Add(name, TextureImporter.LoadStaticTexture(image));
                }
                sector.Material = material;
                StaticMesh.Sectors.Add(sector);
            }
                
        }


        return StaticMesh;
    }
}
