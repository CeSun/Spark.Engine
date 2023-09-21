﻿using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;
using Spark.Engine.Physics;
using Jitter.Collision.Shapes;
using Jitter.LinearMath;
using Jitter.Dynamics;

namespace Spark.Engine.Assets;

public class StaticMesh : Asset
{
    List<List<StaticMeshVertex>> Meshes = new List<List<StaticMeshVertex>>();
    List<List<uint>> _IndicesList = new List<List<uint>>();
    public List<Material> Materials = new List<Material>();
    List<JVector> ConvexHullSourceData = new List<JVector>();
    List<uint> _VertexArrayObjectIndexes = new List<uint>();
    List<uint> VertexBufferObjectIndexes = new List<uint>();
    List<uint> _ElementBufferObjectIndexes = new List<uint>();

    public IReadOnlyList<uint> ElementBufferObjectIndexes => _ElementBufferObjectIndexes;
    public IReadOnlyList<IReadOnlyCollection<uint>> IndicesList => _IndicesList;
    public IReadOnlyList<uint> VertexArrayObjectIndexes => _VertexArrayObjectIndexes;
    public Box Box { get; private set; }

    public List<Box> Boxes { get; private set; } = new List<Box>();
    public StaticMesh(string path) : base(path)
    {
    }

    public StaticMesh(List<StaticMeshVertex> mesh, List<uint> indices, Material material)
    {
        Meshes.Add(mesh);
        _IndicesList.Add(indices);
        Materials.Add(material);
        IsValid = true;
        IsLoaded = true;
        InitRender();
        InitPhysics();
    }

    protected virtual void InitPhysics()
    {
        List<JVector> points = new List<JVector>();
        foreach (var Mesh in Meshes)
        {
            foreach (var vertex in Mesh)
            {
                points.Add(new JVector(vertex.Location.X, vertex.Location.Y, vertex.Location.Z));
            }
        }
        var indexes = JConvexHull.Build(points, JConvexHull.Approximation.Level1);

        foreach(var i in indexes)
        {
            ConvexHullSourceData.Add(points[i]);
        }
    }

    public List<JVector> GetConvexHull()
    {
        return ConvexHullSourceData.ToList();
    }
    protected override void LoadAsset()
    {
        using var sr = FileSystem.GetStream("Content" + Path);

        var model = ModelRoot.ReadGLB(sr);
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
                Box box = new Box();
                if (staticMeshVertices.Count > 0)
                {
                    box.MaxPoint = box.MinPoint = staticMeshVertices[0].Location;
                }
                foreach(var Vertex in staticMeshVertices)
                {
                    box += Vertex.Location;
                }
                Boxes.Add(box);
                Meshes.Add(staticMeshVertices);

                List<uint> Indices= new List<uint>();
                foreach(var index in glPrimitive.IndexAccessor.AsIndicesArray())
                {
                    Indices.Add(index);
                }
                _IndicesList.Add(Indices);
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
        InitTBN();
        InitRender();
        if (Boxes.Count > 0)
        {
            Box = Boxes[0];
            foreach(var box in Boxes)
            {
                Box += box;
            }
        }

        InitPhysics();
    }

    private void InitTBN()
    {
        for (int i = 0; i < IndicesList.Count; i ++)
        {
            InitMeshTBN(i);
        }
    }

    private void InitMeshTBN(int index)
    {
        var vertics = Meshes[index];
        var indices = _IndicesList[index];

        for(int i = 0; i < indices.Count; i += 3)
        {
            var p1 = vertics[(int)indices[i]];
            var p2 = vertics[(int)indices[i + 1]];
            var p3 = vertics[(int)indices[i + 2]];

            Vector3 Edge1 = p2.Location - p1.Location;
            Vector3 Edge2 = p3.Location - p1.Location;
            Vector2 DeltaUV1 = p2.TexCoord - p1.TexCoord;
            Vector2 DeltaUV2 = p3.TexCoord - p1.TexCoord;

            float f = 1.0f / (DeltaUV1.X * DeltaUV2.Y - DeltaUV2.X * DeltaUV1.Y);

            Vector3 tangent1;
            Vector3 bitangent1;

            tangent1.X = f * (DeltaUV2.Y * Edge1.X - DeltaUV1.Y * Edge2.X);
            tangent1.Y = f * (DeltaUV2.Y * Edge1.Y - DeltaUV1.Y * Edge2.Y);
            tangent1.Z = f * (DeltaUV2.Y * Edge1.Z - DeltaUV1.Y * Edge2.Z);
            tangent1 = Vector3.Normalize(tangent1);

            bitangent1.X = f * (-DeltaUV2.X * Edge1.X + DeltaUV1.X * Edge2.X);
            bitangent1.Y = f * (-DeltaUV2.X * Edge1.Y + DeltaUV1.X * Edge2.Y);
            bitangent1.Z = f * (-DeltaUV2.X * Edge1.Z + DeltaUV1.X * Edge2.Z);
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
    private unsafe void InitRender()
    {
        for (var index = 0; index < Meshes.Count; index++)
        {
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ebo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (StaticMeshVertex* p = CollectionsMarshal.AsSpan(Meshes[index]))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(Meshes[index].Count * sizeof(StaticMeshVertex)), p, GLEnum.StaticDraw);
            }
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            fixed (uint* p = CollectionsMarshal.AsSpan(_IndicesList[index]))
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(_IndicesList[index].Count * sizeof(uint)), p, GLEnum.StaticDraw);
            }

            // Location
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)0);
            // Normal
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)sizeof(Vector3));


            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(2 * sizeof(Vector3)));


            gl.EnableVertexAttribArray(3);
            gl.VertexAttribPointer(3, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(3 * sizeof(Vector3)));

            // Color
            gl.EnableVertexAttribArray(4);
            gl.VertexAttribPointer(4, 3, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(4 * sizeof(Vector3)));
            // TexCoord
            gl.EnableVertexAttribArray(5);
            gl.VertexAttribPointer(5, 2, GLEnum.Float, false, (uint)sizeof(StaticMeshVertex), (void*)(5 * sizeof(Vector3)));
            gl.BindVertexArray(0);

            _VertexArrayObjectIndexes.Add(vao);
            VertexBufferObjectIndexes.Add(vbo);
            _ElementBufferObjectIndexes.Add(ebo);
        }
    }

    public unsafe void Render(double DeltaTime)
    {
        if (IsValid == false)
            return;
        int index = 0;
        gl.PushDebugGroup("Render Static Mesh:" + Path);
        foreach(var mesh in Meshes)
        {
            Materials[index].Use();
            gl.BindVertexArray(_VertexArrayObjectIndexes[index]);
            gl.DrawElements(GLEnum.Triangles, (uint)_IndicesList[index].Count, GLEnum.UnsignedInt, (void*)0);
            index++;
        }
        gl.PopDebugGroup();
    }


    ~StaticMesh()
    {
        foreach(var vao in VertexArrayObjectIndexes)
        {
            // gl.DeleteVertexArray(vao);
        }
        foreach(var vbo in VertexBufferObjectIndexes)
        {
            //gl.DeleteBuffer(vbo);
        }
        foreach (var ebo in _ElementBufferObjectIndexes)
        {
            //gl.DeleteBuffer(ebo);
        }
    }
}


public struct StaticMeshVertex
{
    public Vector3 Location;

    public Vector3 Normal;

    public Vector3 Tangent;

    public Vector3 BitTangent;

    public Vector3 Color;

    public Vector2 TexCoord;
}
