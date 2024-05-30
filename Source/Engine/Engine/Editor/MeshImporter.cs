using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Jitter2.Collision.Shapes;
using Jitter2.LinearMath;
using SharpGLTF.Schema2;
using Spark.Engine.Assets;
using Spark.Engine.Physics;
using Spark.Engine.Platform;
using Material = Spark.Engine.Assets.Material;
using Texture = Spark.Engine.Assets.Texture;

namespace Spark.Engine.Editor;

public class StaticMeshImportSetting
{

}
public static class MeshImporter
{
    public static StaticMesh ImporterStaticMeshFromGlbFile(this Engine engine, string filePath, StaticMeshImportSetting staticMeshImportSetting)
    {
        using var sr = IFileSystem.Instance.GetStreamReader(filePath);
        ModelRoot model = ModelRoot.ReadGLB(sr.BaseStream, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.TryFix });

        StaticMesh sm = new StaticMesh();
        foreach (var glMesh in model.LogicalMeshes)
        {
            if (glMesh.Name.IndexOf("Physics_") < 0)
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
                                var Vertex = new StaticMeshVertex();
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
                                var Vertex = new StaticMeshVertex();
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

                                var Vertex = new StaticMeshVertex();
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
                    sm.Boxes.Add(box);
                    List<uint> Indices = [.. glPrimitive.IndexAccessor.AsIndicesArray()];
                    var Material = new Material();
                    Texture? MetallicRoughness = null;
                    Texture? AmbientOcclusion = null;
                    Texture? Parallax = null;

                    foreach (var glChannel in glPrimitive.Material.Channels)
                    {
                        if (glChannel.Texture == null)
                            continue;

                        if (glChannel.Key == "MetallicRoughness")
                        {
                            MetallicRoughness = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                            continue;
                        }
                        if (glChannel.Key == "AmbientOcclusion")
                        {
                            AmbientOcclusion = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                            continue;
                        }
                        if (glChannel.Key == "Parallax")
                        {

                            Parallax = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                            continue;
                        }

                        var texture = engine.ImportTextureFromMemory(glChannel.Texture.PrimaryImage.Content.Content.ToArray(), new());
                        if (glChannel.Key == "BaseColor" || glChannel.Key == "Diffuse")
                        {
                            Material.BaseColor = texture;
                        }
                        if (glChannel.Key == "Normal")
                        {
                            Material.Normal = texture;
                        }

                    }
                    var arm = engine.MergePbrTexture(MetallicRoughness, AmbientOcclusion);
                    Material.Arm = arm;
                    sm.Elements.Add(new Element<StaticMeshVertex>
                    {
                        Vertices = staticMeshVertices,
                        Material = Material,
                        Indices = Indices,
                        IndicesLen = (uint)Indices.Count
                    });
                }
            }
            else
            {
                foreach (var glPrimitive in glMesh.Primitives)
                {
                    List<JTriangle> ShapeSource = new List<JTriangle>();
                    var locations = glPrimitive.GetVertices("POSITION").AsVector3Array();
                    for (int i = 0; i < glPrimitive.GetIndices().Count; i += 3)
                    {
                        int index1 = (int)glPrimitive.GetIndices()[i];
                        int index2 = (int)glPrimitive.GetIndices()[i + 1];
                        int index3 = (int)glPrimitive.GetIndices()[i + 2];

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
                        ShapeSource.Add(tri);
                    }
                    sm.Shapes.Add(new ConvexHullShape(ShapeSource));
                }
            }
        }
        sm.InitTBN();
        sm.InitPhysics();
        return sm;
    }
}

