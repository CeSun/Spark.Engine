using Jitter2.Collision.Shapes;
using Jitter2.Dynamics;
using System.Numerics;
using Jitter2.LinearMath;
using Spark.Engine.Attributes;

namespace Spark.Engine.Components;

public class CapsuleComponent : PrimitiveComponent
{
    public CapsuleShape _CapsuleShape { get;private set; }

    private readonly RigidBody _rigidBody;
    protected override bool ReceiveUpdate => true;
    public CapsuleComponent(Actor actor) : base(actor)
    {
        _CapsuleShape = new CapsuleShape();
        _rigidBody = PhysicsWorld.CreateRigidBody();
        _rigidBody.AddShape( _CapsuleShape );
    }
    public float Radius { get => _CapsuleShape.Radius; set => _CapsuleShape.Radius = value; }
    public float Length { get => _CapsuleShape.Length; set => _CapsuleShape.Length = value; }
    public override  bool IsStatic { get => _rigidBody.IsStatic; set => _rigidBody.IsStatic = value; }


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
                RigidBody.Orientation =
                    new JQuaternion(WorldRotation.X, WorldRotation.Y, WorldRotation.Z, WorldRotation.W);
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
                var rotationM = Matrix4x4.CreateFromQuaternion(new Quaternion(RigidBody.Orientation.X,
                    RigidBody.Orientation.Y, RigidBody.Orientation.Z, RigidBody.Orientation.W));
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
