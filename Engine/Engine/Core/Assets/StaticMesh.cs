using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core.Assets;

public class StaticMesh : Asset
{
    List<List<StaticMeshVertex>> Meshes = new List<List<StaticMeshVertex>>();
    List<List<uint>> IndicesList = new List<List<uint>>();
    List<Material> Materials = new List<Material>();

    List<uint> VertexArrayObjectIndexes = new List<uint>();
    List<uint> VertexBufferObjectIndexes = new List<uint>();
    List<uint> ElementBufferObjectIndexes = new List<uint>();
    public StaticMesh(string path, bool IsAsync) : base(path, IsAsync)
    {
    }

    protected override async Task AsyncLoad()
    {
        await Task.Run(() =>
        {
            try
            {
                var model = SharpGLTF.Schema2.ModelRoot.Load("./Assets" + Path);
                foreach (var glMesh in model.LogicalMeshes)
                {
                    foreach(var glPrimitive in glMesh.Primitives)
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
                                    Vertex.TexCoord = new Vector2 { X = v.X, Y = v.Y};
                                    staticMeshVertices[index] = Vertex;
                                    index++;
                                }
                            }
                        }
                        Meshes.Add(staticMeshVertices);

                        List<uint> Indices= new List<uint>();
                        foreach(var index in glPrimitive.IndexAccessor.AsIndicesArray())
                        {
                            Indices.Add(index);
                        }
                        IndicesList.Add(Indices);
                        var Material = new Material();
                        foreach(var glChannel in glPrimitive.Material.Channels)
                        {
                            if (glChannel.Texture == null)
                                continue;
                            var texture = new Texture(glChannel.Texture.PrimaryImage.Content.Content.ToArray());
                            if (glChannel.Key == "BaseColor")
                            {
                                Material.Diffuse = texture;
                            }
                            if (glChannel.Key == "Normal")
                            {
                                Material.Normal = texture;
                            }
                        }
                        Materials.Add(Material);
                    }
                }
                InitRender();
                IsValid = true;

            }
            catch
            {
                IsValid = false;
            }
        });
    }

    private unsafe void InitRender()
    {

        for (var index = 0; index < Meshes.Count; index++)
        {
            VertexArrayObjectIndexes[index] = gl.GenVertexArray();
            VertexBufferObjectIndexes[index] = gl.GenBuffer();
            ElementBufferObjectIndexes[index] = gl.GenBuffer();
            gl.BindVertexArray(VertexArrayObjectIndexes[index]);

            // Location
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)0);
            // Normal
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)sizeof(Vector3));
            // Color
            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(2 * sizeof(Vector3)));
            // TexCoord
            gl.EnableVertexAttribArray(3);
            gl.VertexAttribPointer(3, 2, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(3 * sizeof(Vector3)));
            gl.BindBuffer(GLEnum.VertexArray, VertexBufferObjectIndexes[index]);
            fixed(void* p = Meshes[index].ToArray())
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(Meshes[index].Count * sizeof(StaticMeshVertex)), p, GLEnum.StaticDraw);
            }
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ElementBufferObjectIndexes[index]);
            fixed(void* p = IndicesList[index].ToArray())
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(IndicesList[index].Count * sizeof(uint)), p, GLEnum.StaticDraw);
            }
            gl.BindVertexArray(0);
            gl.BindBuffer(GLEnum.VertexArray, 0);
            gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);

        }
        


    }
    public unsafe void Render(double DeltaTime)
    {
        if (IsValid == false)
            return;
        int index = 0;
        foreach(var mesh in Meshes)
        {
            Materials[index].Use();
            gl.BindVertexArray(VertexArrayObjectIndexes[index]);
            gl.DrawElements(GLEnum.Triangles, (uint)IndicesList[index].Count, GLEnum.UnsignedInt, (void*)0);
            index++;
            gl.ActiveTexture(GLEnum.Texture0);
        }
    }
}


public struct StaticMeshVertex
{
    public Vector3 Location;

    public Vector3 Normal;

    public Vector3 Color;

    public Vector2 TexCoord;
}
