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
            BoundingBox = null;
            if (value != null)
            {
                RigidBody = Owner.CurrentLevel.PhyWorld.CreateRigidBody();
                var sm = Matrix4x4.CreateScale(WorldScale);
                foreach(var shape in value.Shapes)
                {
                    RigidBody.AddShape(new TransformedShape(shape, JVector.Zero, new JMatrix
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
                    }));
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

                RigidBody.IsStatic = IsStatic;
                RigidBody.Tag = this;
                var worldTransform = WorldTransform;
               
                BoundingBox = new Physics.BoundingBox(Vector3.Transform(value.Box.MaxPoint, worldTransform), Vector3.Transform(value.Box.MinPoint, worldTransform), this);
                UpdateOctree();
            }
            InitRender();
        }
    }
        
    public StaticMeshComponent(Actor actor) : base(actor)
    {

    }

    public override Vector3 RelativeLocation 
    { 
        get => base.RelativeLocation;
        set 
        {
            base.RelativeLocation = value;
            if (RigidBody != null)
            {
                RigidBody.Position = new JVector(WorldLocation.X, WorldLocation.Y, WorldLocation.Z);
            }
            if (BoundingBox != null && StaticMesh != null)
            {
                var worldTransform = WorldTransform;
                BoundingBox.MinPoint = Vector3.Transform(StaticMesh.Box.MinPoint, worldTransform);
                BoundingBox.MaxPoint = Vector3.Transform(StaticMesh.Box.MaxPoint, worldTransform);

                UpdateOctree();
            }
        } 
    }
    public override Quaternion RelativeRotation
    { 
        get => base.RelativeRotation;
        set
        {
            base.RelativeRotation = value;
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
            if (BoundingBox != null && StaticMesh != null)
            {
                var worldTransform = WorldTransform;
                BoundingBox.MinPoint = Vector3.Transform(StaticMesh.Box.MinPoint, worldTransform);
                BoundingBox.MaxPoint = Vector3.Transform(StaticMesh.Box.MaxPoint, worldTransform);

                UpdateOctree();
            }
        }
    }
    public override Vector3 RelativeScale 
    { 
        get => base.RelativeScale;
        set 
        { 
            base.RelativeScale = value;
            if (RigidBody != null && StaticMesh != null)
            {
                for (int i = RigidBody.Shapes.Count - 1; i >= 0; i--)
                {
                    RigidBody.RemoveShape(RigidBody.Shapes[i]);
                }
                var sm = Matrix4x4.CreateScale(WorldScale);
                foreach (var shape in StaticMesh.Shapes)
                {
                    RigidBody.AddShape(new TransformedShape(shape, JVector.Zero, new JMatrix
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
                    }));
                }
            }

            if (BoundingBox != null && StaticMesh != null)
            {
                var worldTransform = WorldTransform;
                BoundingBox.MinPoint = Vector3.Transform(StaticMesh.Box.MinPoint, worldTransform);
                BoundingBox.MaxPoint = Vector3.Transform(StaticMesh.Box.MaxPoint, worldTransform);

                UpdateOctree();
            }
        }
    }

 
    public override void OnUpdate(double DeltaTime)
    {
        base.OnUpdate(DeltaTime);
        if (RigidBody != null && IsStatic == false)
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

                var tmpWorldTransform = MatrixHelper.CreateTransform(new Vector3(RigidBody.Position.X, RigidBody.Position.Y, RigidBody.Position.Z), rotationM.Rotation(), WorldScale);
                Matrix4x4.Invert(ParentWorldTransform, out var ParentInverseWorldTransform);
                var tmpRelativeTransform = tmpWorldTransform* ParentInverseWorldTransform;
                _RelativeLocation = tmpRelativeTransform.Translation;
                _RelativeRotation = tmpRelativeTransform.Rotation();

                if (BoundingBox != null && StaticMesh != null)
                {
                    var worldTransform = WorldTransform;
                    BoundingBox.MinPoint = Vector3.Transform(StaticMesh.Box.MinPoint, worldTransform);
                    BoundingBox.MaxPoint = Vector3.Transform(StaticMesh.Box.MaxPoint, worldTransform);
                    UpdateOctree();
                }
            }
        }

    }
    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
        if (StaticMesh != null)
        {
            int index = 0;
            gl.PushGroup("Render Static Mesh:" + StaticMesh.Path);
            foreach (var element in StaticMesh.Elements)
            {
                for (int i = 0; i < element.Material.Textures.Count(); i++)
                {
                    var texture = element.Material.Textures[i];
                    gl.ActiveTexture(GLEnum.Texture0 + i);
                    if (texture != null)
                    {
                        gl.BindTexture(GLEnum.Texture2D, texture.TextureId);
                    }
                    else
                    {
                        gl.BindTexture(GLEnum.Texture2D, 0);
                    }
                }
                gl.BindVertexArray(element.VertexArrayObjectIndex);
                unsafe
                {
                    gl.DrawElements(GLEnum.Triangles, (uint)element.IndicesLen, GLEnum.UnsignedInt, (void*)0);
                }
                index++;
            }
            gl.PopGroup();
        }
    }

    public void InitRender()
    {
        if (StaticMesh == null)
            return;
        StaticMesh.InitRender(gl);
        foreach (var element in StaticMesh.Elements)
        {
            foreach (var texture in
            element.Material.Textures)
            {
                if (texture != null)
                    texture.InitRender(gl);
            }
        }
    }
    protected override void OnEndPlay()
    {
        if (RigidBody != null)
        {
            Owner.CurrentLevel.PhyWorld.Remove(RigidBody);
        }
    }
}
