using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.LinearMath;
using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    protected override bool ReceieveUpdate => true;
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
                Owner.CurrentLevel.PhyWorld.Remove(RigidBody);
            }
            RigidBody = null;
            _StaticMesh = value;
            if (value != null)
            {
                RigidBody = Owner.CurrentLevel.PhyWorld.CreateRigidBody();
                var sm = Matrix4x4.CreateScale(WorldScale);
                foreach(var shape in value.Shapes)
                {
                    RigidBody.AddShape(new TransformedShape(shape, new JMatrix
                    {
                        M11 = sm.M11,
                        M12 = sm.M12,
                        M13 = sm.M13,
                        M21 = sm.M21,
                        M22 = sm.M22,
                        M23 = sm.M23,
                        M31 = sm.M31,
                        M32 = sm.M32,
                        M33 = sm.M33,
                    }, JVector.Zero));
                }
                RigidBody.Position = new JVector(WorldLocation.X, WorldLocation.Y, WorldLocation.Z);
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
                for (int i = RigidBody.Shapes.Count - 1; i >= 0; i--)
                {
                    RigidBody.RemoveShape(RigidBody.Shapes[i]);
                }
                var sm = Matrix4x4.CreateScale(WorldScale);
                foreach (var shape in StaticMesh.Shapes)
                {
                    RigidBody.AddShape(new TransformedShape(shape, new JMatrix
                    {
                        M11 = sm.M11,
                        M12 = sm.M12,
                        M13 = sm.M13,
                        M21 = sm.M21,
                        M22 = sm.M22,
                        M23 = sm.M23,
                        M31 = sm.M31,
                        M32 = sm.M32,
                        M33 = sm.M33,
                    }, JVector.Zero));
                }
            }
        }
    }
    public override void Update(double DeltaTime)
    {
        base.Update(DeltaTime);
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
                base.WorldRotation = rotationM.Rotation();
                base.WorldLocation = new Vector3(RigidBody.Position.X, RigidBody.Position.Y, RigidBody.Position.Z);
            }
        }

    }
    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
        if (StaticMesh != null)
        {
            int index = 0;
            gl.PushDebugGroup("Render Static Mesh:" + StaticMesh.Path);
            foreach (var element in StaticMesh.Elements)
            {
                element.Material.Use();
                gl.BindVertexArray(element.VertexArrayObjectIndex);
                unsafe
                {
                    gl.DrawElements(GLEnum.Triangles, (uint)element.Indices.Count, GLEnum.UnsignedInt, (void*)0);
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
            Owner.CurrentLevel.PhyWorld.Remove(RigidBody);
        }
    }
}
