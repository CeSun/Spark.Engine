using Spark.Engine.Actors;
using Jitter2.Collision;
using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using System.Numerics;
using Jitter2.LinearMath;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;

namespace Spark.Engine.Components;

public class CapsuleComponent : PrimitiveComponent
{
    public CapsuleShape _CapsuleShape { get;private set; }

    private RigidBody _RigidBody;
    protected override bool ReceieveUpdate => true;
    public CapsuleComponent(Actor actor) : base(actor)
    {
        _CapsuleShape = new CapsuleShape();
        _RigidBody = PhysicsWorld.CreateRigidBody();
        _RigidBody.AddShape( _CapsuleShape );
    }


    [Property]
    public float Radius { get => _CapsuleShape.Radius; set => _CapsuleShape.Radius = value; }
    [Property]
    public float Length { get => _CapsuleShape.Length; set => _CapsuleShape.Length = value; }
    public override  bool IsStatic { get => _RigidBody.IsStatic; set => _RigidBody.IsStatic = value; }


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
        }
    }
    public override Vector3 RelativeScale
    {
        get => base.RelativeScale;
        set
        {
            base.RelativeScale = value;
            if (RigidBody != null)
            {
                for (int i = RigidBody.Shapes.Count - 1; i >= 0; i--)
                {
                    RigidBody.RemoveShape(RigidBody.Shapes[i]);
                }
                var sm = Matrix4x4.CreateScale(WorldScale);
                RigidBody.AddShape(new TransformedShape(_CapsuleShape, JVector.Zero, new JMatrix
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
                var tmpRelativeTransform = tmpWorldTransform * ParentInverseWorldTransform;
                _RelativeLocation = tmpRelativeTransform.Translation;
                _RelativeRotation = tmpRelativeTransform.Rotation();
            }
        }
    }
    protected override void OnBeginPlay()
    {
        base.OnBeginPlay();

    }
    protected override void OnEndPlay()
    {
    }
}
