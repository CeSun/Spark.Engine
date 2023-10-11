using SharpGLTF.Schema2;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGLES;
using static Spark.Engine.StaticEngine;
using Spark.Engine.Physics;
using Jitter2.LinearMath;
using System.Collections.ObjectModel;
using Jitter2.Collision.Shapes;

namespace Spark.Engine.Assets;

public class StaticMesh
{
    List<Element<StaticMeshVertex>> _Elements = new List<Element<StaticMeshVertex>>();
    public List<Shape> Shapes = new List<Shape>();
    public IReadOnlyList<Element<StaticMeshVertex>> Elements => _Elements;
    public Box Box { get; private set; }

    public List<Box> Boxes { get; private set; } = new List<Box>();
    public string Path { get; private set; }
    public StaticMesh(string path)
    {
        Path = path;
        LoadAsset();
    }

    public StaticMesh(List<Element<StaticMeshVertex>> Elementes)
    {
        Path = string.Empty;
        _Elements.AddRange(Elementes);
        InitRender();
    }


  
    protected void LoadAsset()
    {
        using var sr = FileSystem.GetStream("Content" + Path);

        var model = ModelRoot.ReadGLB(sr);
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
                    Boxes.Add(box);
                    List<uint> Indices = new List<uint>();
                    foreach (var index in glPrimitive.IndexAccessor.AsIndicesArray())
                    {
                        Indices.Add(index);
                    }
                    var Material = new Material();
                    foreach (var glChannel in glPrimitive.Material.Channels)
                    {
                        if (glChannel.Texture == null)
                            continue;
                        var texture = new Texture(glChannel.Texture.PrimaryImage.Content.Content.ToArray());
                        if (glChannel.Key == "BaseColor" || glChannel.Key == "Diffuse")
                        {
                            Material.Diffuse = texture;
                        }
                        if (glChannel.Key == "Normal")
                        {
                            Material.Normal = texture;
                        }
                    }
                    _Elements.Add(new Element<StaticMeshVertex>
                    {
                        Vertices = staticMeshVertices,
                        Material = Material,
                        Indices = Indices
                    });
                }
            }
            else
            {
                foreach (var glPrimitive in glMesh.Primitives)
                {
                    List<JTriangle> ShapeSource = new List<JTriangle>();
                    var locations = glPrimitive.GetVertices("POSITION").AsVector3Array();
                    for(int i = 0; i < glPrimitive.GetIndices().Count; i +=3)
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
                    Shapes.Add(new ConvexHullShape(ShapeSource));
                }
            }
        }
        InitTBN();
        InitRender();
        InitPhysics();
        if (Boxes.Count > 0)
        {
            Box = Boxes[0];
            foreach(var box in Boxes)
            {
                Box += box;
            }
        }

    }

    private void InitTBN()
    {
        for (int i = 0; i < _Elements.Count; i ++)
        {
            InitMeshTBN(i);
        }
    }

    private void InitPhysics()
    {
        if (Shapes.Count > 0 )
        {
            return;
        }
        List<JVector> vertices = new List<JVector>();
        foreach(var element in _Elements)
        {
            foreach(var vertex in element.Vertices)
            {

                vertices.Add(new JVector
                {
                    X = vertex.Location.X,
                    Y = vertex.Location.Y,
                    Z = vertex.Location.Z
                });
            }
        }
        Shapes.Add(new PointCloudShape(vertices));

    }
    private void InitMeshTBN(int index)
    {
        var vertics = _Elements[index].Vertices;
        var indices = _Elements[index].Indices;

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
        for (var index = 0; index < _Elements.Count; index++)
        {
            uint vao = gl.GenVertexArray();
            uint vbo = gl.GenBuffer();
            uint ebo = gl.GenBuffer();
            gl.BindVertexArray(vao);
            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
            fixed (StaticMeshVertex* p = CollectionsMarshal.AsSpan(_Elements[index].Vertices))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(_Elements[index].Vertices.Count * sizeof(StaticMeshVertex)), p, GLEnum.StaticDraw);
            }
            gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
            fixed (uint* p = CollectionsMarshal.AsSpan(_Elements[index].Indices))
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(_Elements[index].Indices.Count * sizeof(uint)), p, GLEnum.StaticDraw);
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
            _Elements[index].VertexArrayObjectIndex = vao;
            _Elements[index].VertexBufferObjectIndex = vbo;
            _Elements[index].ElementBufferObjectIndex = ebo;
        }
    }


    ~StaticMesh()
    {
        
    }
}

public class Element<T> where T  : struct
{
    public required List<T> Vertices;
    public required List<uint> Indices;
    public required Material Material;
    public uint VertexArrayObjectIndex;
    public uint VertexBufferObjectIndex;
    public uint ElementBufferObjectIndex;
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
