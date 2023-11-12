using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using Jitter2.LinearMath;
using SharpGLTF.Schema2;
using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Physics;
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
            if (_StaticMesh != null)
            {
                RigidBody = Owner.CurrentLevel.PhyWorld.CreateRigidBody();
                foreach(var shape in _StaticMesh.Shapes)
                {
                    RigidBody.AddShape(new TransformedShape(shape, JVector.Zero));
                }
                RigidBody.Position = new JVector(WorldLocation.X, WorldLocation.Y, WorldLocation.Z);
                RigidBody.IsStatic = IsStatic;
                RigidBody.Tag = this;
                UpdatePhyscisScale();
                UpdatePhyscisRotation();
                var worldTransform = WorldTransform;
                Box box = default;
                for(int i = 0; i < 8; i ++)
                {
                    if (i == 0)
                    {
                        box.MinPoint = Vector3.Transform(_StaticMesh.Box[i], worldTransform);
                        box.MaxPoint = box.MinPoint;
                    }
                    else
                    {
                        box += Vector3.Transform(_StaticMesh.Box[i], worldTransform);
                    }
                }
                BoundingBox = new BoundingBox(box.MaxPoint, box.MinPoint, this);     
            }
            InitRender();
        }
    }
    private void UpdatePhyscisRotation()
    {
        if (RigidBody == null)
            return;
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
    private void UpdatePhyscisScale()
    {
        if (RigidBody == null)
            return;
        var Matrix = Matrix4x4.CreateScale(WorldScale);
        foreach (var shape in RigidBody.Shapes)
        {
            if (shape is TransformedShape transShape)
            {
                transShape.Transformation = new JMatrix
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

 
    public override void OnUpdate(double DeltaTime)
    {
        base.OnUpdate(DeltaTime);


        if (TransformDirtyFlag)
        {
            if (RigidBody != null)
            {
                if (RotationDirtyFlag == true)
                {
                    UpdatePhyscisRotation();
                    RotationDirtyFlag = false;
                }
                if (ScaleDirtyFlag == true)
                {
                    //UpdatePhyscisScale();
                    ScaleDirtyFlag = false;
                }
                if (TranslateDirtyFlag == true)
                {
                    RigidBody.Position = new JVector(WorldLocation.X, WorldLocation.Y, WorldLocation.Z);
                    TranslateDirtyFlag = false;
                }
            }
            if (BoundingBox != null && StaticMesh != null)
            {
                var worldTransform = WorldTransform;

                Box box = default;
                for (int i = 0; i < 8; i++)
                {
                    if (i == 0)
                    {
                        box.MinPoint = Vector3.Transform(StaticMesh.Box[i], worldTransform);
                        box.MaxPoint = box.MinPoint;
                    }
                    else
                    {
                        box += Vector3.Transform(StaticMesh.Box[i], worldTransform);
                    }
                }

                BoundingBox.MaxPoint = box.MaxPoint;
                BoundingBox.MinPoint = box.MinPoint;

                UpdateOctree();
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
            if (BoundingBox != null)
            {
                CurrentLevel.RenderObjectOctree.RemoveObject(BoundingBox);
                BoundingBox = null;
            }
        }
    }
}
