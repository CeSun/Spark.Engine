using Jitter2;
using Jitter2.LinearMath;
using Silk.NET.OpenGLES;
using Spark.Util;
using System.Runtime.InteropServices;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Render.Renderer;

public class Debug : Singleton<Debug>, IDebugDrawer
{
    uint vao = default;
    uint vbo = default;
    uint ebo = default;
    public unsafe Debug()
    {
        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();
        gl.BindVertexArray(vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
        gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
        // Location
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(JVector), (void*)0);
        gl.BindVertexArray(0);
    }

    List<JVector> Locations = new List<JVector>();
    List<uint> Indices = new List<uint>();
    public unsafe void DrawPoint(in JVector v)
    {
        Locations.Clear();
        Locations.Add(v);
        gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
        fixed (void* p = CollectionsMarshal.AsSpan(Locations))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(JVector) * Locations.Count), p, BufferUsageARB.DynamicDraw);
        }

        gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
        Indices.Clear();
        Indices.Add(0);
        fixed (void* p = CollectionsMarshal.AsSpan(Indices))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(uint) * Indices.Count), p, BufferUsageARB.DynamicDraw);
        }

        gl.BindVertexArray(vao);
        gl.DrawElements(GLEnum.Points, (uint)Indices.Count, GLEnum.UnsignedInt, (void*)0);
    }

    public unsafe void DrawSegment(in JVector pA, in JVector pB)
    {
        Locations.Clear();
        Locations.Add(pA);
        Locations.Add(pB);
        gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
        fixed (void* p = CollectionsMarshal.AsSpan(Locations))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(JVector) * Locations.Count), p, BufferUsageARB.DynamicDraw);
        }

        gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
        Indices.Clear();
        Indices.Add(0);
        Indices.Add(1);
        fixed (void* p = CollectionsMarshal.AsSpan(Indices))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(uint) * Indices.Count), p, BufferUsageARB.DynamicDraw);
        }
        
        gl.BindVertexArray(vao);
        gl.DrawElements(GLEnum.Points, (uint)Indices.Count, GLEnum.UnsignedInt, (void*)0);
    }

    public unsafe void DrawTriangle(in JVector pA, in JVector pB, in JVector pC)
    {
        Locations.Clear();
        Locations.Add(pA);
        Locations.Add(pB);
        Locations.Add(pC);
        gl.BindBuffer(GLEnum.ArrayBuffer, vbo);
        fixed (void* p = CollectionsMarshal.AsSpan(Locations))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(JVector) * Locations.Count), p, BufferUsageARB.DynamicDraw);
        }

        gl.BindBuffer(GLEnum.ElementArrayBuffer, ebo);
        Indices.Clear();
        Indices.Add(0);
        Indices.Add(1);
        Indices.Add(1);
        fixed (void* p = CollectionsMarshal.AsSpan(Indices))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(uint) * Indices.Count), p, BufferUsageARB.DynamicDraw);
        }

        gl.BindVertexArray(vao);
        gl.DrawElements(GLEnum.Points, (uint)Indices.Count, GLEnum.UnsignedInt, (void*)0);
    }
}
