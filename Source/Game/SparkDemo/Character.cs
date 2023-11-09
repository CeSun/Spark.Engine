using Jitter2.Collision.Shapes;
using SharpGLTF.Schema2;
using Silk.NET.Input;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Components;
using Spark.Util;
using System.Numerics;

namespace SparkDemo
{
    public class Character : Actor
    {
        public float MoveSpeed = 10;

        public Vector3 Speed; 
        public SkeletalMeshComponent Mesh { get; set; }

        public float DownSpeed = 0;
        public CapsuleComponent CapsuleComponent { get; set; }

        public SkeletalMeshComponent Wpn;

        public StaticMeshComponent Mag;
        protected override bool ReceieveUpdate => true;
        public Character(Level level, string Name = "") : base(level, Name)
        {
            CapsuleComponent = new CapsuleComponent(this);
            CapsuleComponent.Radius = 0.5F;
            CapsuleComponent.Length = 1;
            CapsuleComponent.IsStatic = true;
            Mesh = new SkeletalMeshComponent(this);
            Mesh.RelativeScale = new Vector3(0.02F);
            Mesh.RelativeLocation = new Vector3(0, -1F, 0);
            Mesh.IsStatic = true;
            Mesh.AttachTo(CapsuleComponent, "", Matrix4x4.Identity, AttachRelation.KeepRelativeTransform); ;

            var fun = async () =>
            {
                var (mesh, sk, _) = await SkeletalMesh.ImportFromGLBAsync("/StaticMesh/Jason.glb");
                var (_, sk2, anim) = await SkeletalMesh.ImportFromGLBAsync("/StaticMesh/AK47_Player_3P_Anim.glb");
                Mesh.SkeletalMesh = mesh;
                Mesh.AnimSequence = anim[0];
            };
            fun();


            Wpn = new SkeletalMeshComponent(this);
            SkeletalMesh.ImportFromGLBAsync("/StaticMesh/AK47.glb").Then(res =>
            {
                var (AK, _, akanim) = res;
                Wpn.SkeletalMesh = AK;
                Wpn.AnimSequence = akanim[0];
            });

            Wpn.AttachTo(Mesh, "b_RightWeapon", Matrix4x4.Identity, AttachRelation.KeepRelativeTransform);
            Wpn.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, 90F.DegreeToRadians(), 0);


            Mag = new StaticMeshComponent(this);
            Mag.AttachTo(Wpn, "b_Magazine_1", Matrix4x4.Identity, AttachRelation.KeepRelativeTransform);
            Mag.IsStatic = true;
            Mag.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, 90F.DegreeToRadians(), 0);
            StaticMesh.LoadFromGLBAsync("/StaticMesh/AK47_Magazine.glb").Then(Mesh =>
            {
                Mag.StaticMesh = Mesh;
            });

        }


        protected override void OnUpdate(double DeltaTime)
        {
            base.OnUpdate(DeltaTime);
            var Down = this.UpVector * -1;
            var location = WorldLocation;

            var AddSpeed = -9.8f;
            var YSpeed = Speed.Y + AddSpeed * (float)DeltaTime;
            var dis = YSpeed * (float)DeltaTime;
            var endLocation = new Vector3(location.X, location.Y + dis, location.Z); ;
            if (CurrentLevel.PhyWorld.Raycast(this.CapsuleComponent.CapsuleShape, new Jitter2.LinearMath.JVector (location.X, location.Y, location.Z), new Jitter2.LinearMath.JVector(Down.X, Down.Y, Down.Z), out var normal, out var distance))
            {
                if (distance - CapsuleComponent.Radius > CapsuleComponent.Length / 2)
                {
                    if ((endLocation - location).Length() < (distance - CapsuleComponent.Radius - CapsuleComponent.Length / 2))
                        this.WorldLocation = endLocation;
                    else
                    {

                        this.WorldLocation = new Vector3(location.X, location.Y - (distance - CapsuleComponent.Radius) + CapsuleComponent.Length / 2, location.Z);
                    }
                }

            }
            else
            {
                this.WorldLocation = endLocation;
            }

            Speed = (this.WorldLocation - location) / (float)DeltaTime;
        }

    }
}
