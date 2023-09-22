using Jitter.Collision.Shapes;
using Jitter.Dynamics;
using Jitter.LinearMath;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Components;

public class StaticMeshComponent : PrimitiveComponent
{

    public override bool IsStatic 
    {
        get => base.IsStatic;
        set
        {
            if (RigidBody != null)
            {
                RigidBody.IsStatic = value;
            }
            base.IsStatic = value;
        }
    }
    public RigidBody? RigidBody;
    private StaticMesh? _StaticMesh;
    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set
        {
            if (RigidBody != null)
            {
                Owner.CurrentLevel.PhyWorld.RemoveBody(RigidBody);
            }
            RigidBody = null;
            _StaticMesh = value;
            if (value != null)
            {
                RigidBody = new RigidBody(GetShape(value.GetConvexHull()));
                RigidBody.Position = new JVector(WorldLocation.X, WorldLocation.Y, WorldLocation.Z);
                RigidBody.Orientation = new JMatrix
                {
                    M11 = WorldTransform.M11,
                    M12 = WorldTransform.M12,
                    M13 = WorldTransform.M13,
                    M21 = WorldTransform.M21,
                    M22 = WorldTransform.M22,
                    M23 = WorldTransform.M23,
                    M31 = WorldTransform.M31,
                    M32 = WorldTransform.M32,
                    M33 = WorldTransform.M33,
                };

                Owner.CurrentLevel.PhyWorld.AddBody(RigidBody);
            }

        }
    }

    public unsafe Shape GetShape(List<JVector> Vertics)
    {
        var span = CollectionsMarshal.AsSpan(Vertics);
        for (int i = 0; i < Vertics.Count; i ++)
        {
            unsafe
            {
                fixed (JVector* p = span)
                {

                    p[i].X *= WorldScale.X;
                    p[i].Y *= WorldScale.Y;
                    p[i].Z *= WorldScale.Z;
                }
            }

        }
        return new ConvexHullShape(Vertics);

    }
    public StaticMeshComponent(Actor actor) : base(actor)
    {

    }

    public override Vector3 WorldLocation 
    { 
        get => base.WorldLocation;
        set 
        {
            base.WorldLocation = value;
            if (RigidBody != null)
            {
                RigidBody.Position = new JVector(WorldLocation.X, WorldLocation.Y, WorldLocation.Z);
            }
        } 
    }
    public override Quaternion WorldRotation 
    { 
        get => base.WorldRotation;
        set
        {
            base.WorldRotation = value;
            if (RigidBody != null)
            {
                var Matrix = Matrix4x4.CreateFromQuaternion(WorldRotation);

                RigidBody.Orientation = new JMatrix
                {
                    M11 = Matrix.M11,
                    M12 = Matrix.M12,
                    M13 = Matrix.M13,
                    M21 = Matrix.M21,
                    M22 = Matrix.M22,
                    M23 = Matrix.M23,
                    M31 = Matrix.M31,
                    M32 = Matrix.M32,
                    M33 = Matrix.M33,
                };
            }
        }
    }
    public override Vector3 WorldScale 
    { 
        get => base.WorldScale;
        set 
        { 
            base.WorldScale = value;
            if (RigidBody != null && StaticMesh != null)
            {
                RigidBody.Shape = GetShape(StaticMesh.GetConvexHull());
            }
        }
    }
    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
        if (RigidBody != null)
        {
            unsafe
            {
                var rotationM = new Matrix4x4
                {
                    M11 = RigidBody.Orientation.M11,
                    M12 = RigidBody.Orientation.M12,
                    M13 = RigidBody.Orientation.M13,
                    M14 = 0,
                    M21 = RigidBody.Orientation.M21,
                    M22 = RigidBody.Orientation.M22,
                    M23 = RigidBody.Orientation.M23,
                    M24 = 0,
                    M31 = RigidBody.Orientation.M31,
                    M32 = RigidBody.Orientation.M32,
                    M33 = RigidBody.Orientation.M33,
                    M34 = 0,
                    M41 = 0,
                    M42 = 0,
                    M43 = 0,
                    M44 = 1,
                };
                WorldRotation = rotationM.Rotation();
                WorldLocation = new Vector3(RigidBody.Position.X, RigidBody.Position.Y, RigidBody.Position.Z);
            }
        }
        
        if (StaticMesh != null)
        {
            int index = 0;
            gl.PushDebugGroup("Render Static Mesh:" + StaticMesh.Path);
            foreach (var mesh in StaticMesh.Meshes)
            {
                StaticMesh.Materials[index].Use();
                gl.BindVertexArray(StaticMesh.VertexArrayObjectIndexes[index]);
                unsafe
                {
                    gl.DrawElements(GLEnum.Triangles, (uint)StaticMesh.IndicesList[index].Count, GLEnum.UnsignedInt, (void*)0);
                }
                index++;
            }
            gl.PopDebugGroup();
        }
    }

    protected override void OnEndGame()
    {
        base.OnEndGame();
        if (RigidBody != null)
        {
            Owner.CurrentLevel.PhyWorld.RemoveBody(RigidBody);
        }
    }
}
