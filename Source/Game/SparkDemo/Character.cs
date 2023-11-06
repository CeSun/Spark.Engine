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
        public float Speed = 10;
        public SkeletalMeshComponent Mesh { get; set; }


        public SkeletalMeshComponent Wpn;

        public StaticMeshComponent Mag;
        protected override bool ReceieveUpdate => false;
        public Character(Level level, string Name = "") : base(level, Name)
        {
            Mesh = new SkeletalMeshComponent(this);
            Mesh.WorldScale = new Vector3(0.03F);
            Mesh.IsStatic = true;

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
            Vector2 Move = new Vector2(0, 0);
            if (CurrentWorld.Engine.MainKeyBoard.IsKeyPressed(Key.W))
            {
                Move.X = 1;
            }
            if (CurrentWorld.Engine.MainKeyBoard.IsKeyPressed(Key.S))
            {
                Move.X = -1;
            }
            if (CurrentWorld.Engine.MainKeyBoard.IsKeyPressed(Key.A))
            {
                Move.Y = -1;
            }
            if (CurrentWorld.Engine.MainKeyBoard.IsKeyPressed(Key.D))
            {
                Move.Y = 1;
            }
            if (Move.Length() > 0)
            {
                Move = Vector2.Normalize(Move);

                this.WorldLocation += this.ForwardVector * Move.X * Speed * (float)DeltaTime;

                this.WorldLocation += this.RightVector * Move.Y * Speed * (float)DeltaTime;
            }
        }
    }
}
