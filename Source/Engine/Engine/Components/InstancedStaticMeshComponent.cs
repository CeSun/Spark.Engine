using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Components;

public class InstancedStaticMeshComponent : PrimitiveComponent
{
    protected List<PrimitiveComponent> PrimitiveComponents;

    public bool CanRender = false;
    public InstancedStaticMeshComponent(Actor actor) : base(actor)
    {
        PrimitiveComponents = new List<PrimitiveComponent>();
    }

    public StaticMesh? StaticMesh
    {
        get => _StaticMesh;
        set
        {
            if (value == null)
            {
                _StaticMesh = null;
                return;
            }
            if (_StaticMesh != value)
            {
                _StaticMesh = value;
            }
        }
    }
    public StaticMesh? _StaticMesh;

    public unsafe virtual void Build()
    {
        if (StaticMesh == null)
            return;
        List<Matrix4x4> WorldTransforms = new List<Matrix4x4>();
        foreach (var component in PrimitiveComponents)
        {
            WorldTransforms.Add(component.WorldTransform);
            WorldTransforms.Add(component.NormalTransform);
        }
        var vbo = gl.GenBuffer();
        gl.BindBuffer(GLEnum.ArrayBuffer, vbo);

        fixed (void* p = CollectionsMarshal.AsSpan(WorldTransforms))
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(Matrix4x4) * WorldTransforms.Count), p, BufferUsageARB.StaticDraw);
        }
        gl.BindVertexArray(StaticMesh.Elements[0].VertexArrayObjectIndex);
        gl.EnableVertexAttribArray(6);
        gl.VertexAttribPointer(6, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)0);
        gl.EnableVertexAttribArray(7);
        gl.VertexAttribPointer(7, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)sizeof(Vector4));
        gl.EnableVertexAttribArray(8);
        gl.VertexAttribPointer(8, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 2));
        gl.EnableVertexAttribArray(9);
        gl.VertexAttribPointer(9, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 3));

        gl.EnableVertexAttribArray(10);
        gl.VertexAttribPointer(10, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 4));
        gl.EnableVertexAttribArray(11);
        gl.VertexAttribPointer(11, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 5));
        gl.EnableVertexAttribArray(12);
        gl.VertexAttribPointer(12, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 6));
        gl.EnableVertexAttribArray(13);
        gl.VertexAttribPointer(13, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 7));

        gl.VertexAttribDivisor(6, 1);
        gl.VertexAttribDivisor(7, 1);
        gl.VertexAttribDivisor(8, 1);
        gl.VertexAttribDivisor(9, 1);

        gl.VertexAttribDivisor(10, 1);
        gl.VertexAttribDivisor(11, 1);
        gl.VertexAttribDivisor(12, 1);
        gl.VertexAttribDivisor(13, 1);
        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        CanRender = true;
    }
    public void AddComponent(SubInstancedStaticMeshComponent primitivce)
    {
        if (primitivce == null)
            return;
        PrimitiveComponents.Add(primitivce);
    }


    public virtual unsafe void RenderISM(CameraComponent cameraComponent, double DeltaTime)
    {
        if (StaticMesh == null)
            return;
        if (CanRender == false)
            return;
        StaticMesh.Elements[0].Material.Use();
        gl.BindVertexArray(StaticMesh.Elements[0].VertexArrayObjectIndex);
        gl.DrawElementsInstanced(GLEnum.Triangles, (uint)StaticMesh.Elements[0].Indices.Count, GLEnum.UnsignedInt, (void*)0, (uint)PrimitiveComponents.Count);
    }

}
public class SubInstancedStaticMeshComponent : PrimitiveComponent
{
    public SubInstancedStaticMeshComponent(Actor actor) : base(actor)
    {
    }
}