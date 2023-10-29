using SharpGLTF.Schema2;
using Silk.NET.Input;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Components;
using Spark.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SparkDemo
{
    public class Character : Actor
    {
        public float Speed = 10;
        public SkeletalMeshComponent Mesh { get; set; }

        protected override bool ReceieveUpdate => false;
        public CameraComponent Camera { get; set; }
        public Character(Level level, string Name = "") : base(level, Name)
        {
            Mesh = new SkeletalMeshComponent(this);
            var (mesh, sk, anim) = SkeletalMesh.ImportFromGLB("/StaticMesh/Soldier.glb");
            Mesh.SkeletalMesh = mesh;
            Mesh.AnimSequence = anim[2];
            Mesh.IsStatic = true;
            RootComponent = Mesh;

            /*
            Camera = new CameraComponent(this);
            Camera.ParentComponent = Mesh;
            Camera.NearPlaneDistance = 1;
            Camera.FarPlaneDistance = 100;

            Camera.RelativeLocation = Vector3.Zero - 2 * Camera.ForwardVector + 3* Camera.UpVector;
            Camera.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, -30F.DegreeToRadians(), 0);
            */
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

                this.WorldLocation += this.RootComponent.ForwardVector * Move.X * Speed * (float)DeltaTime;

                this.WorldLocation += this.RootComponent.RightVector * Move.Y * Speed * (float)DeltaTime;
            }
        }
    }
}
