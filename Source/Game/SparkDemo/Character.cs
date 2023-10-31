﻿using SharpGLTF.Schema2;
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

        protected override bool ReceieveUpdate => false;
        public Character(Level level, string Name = "") : base(level, Name)
        {
            Mesh = new SkeletalMeshComponent(this);
            SkeletalMesh.ImportFromGLBAsync("/StaticMesh/Soldier.glb").Then(res =>
            {
                var (mesh, sk, anim) = res;
                Mesh.SkeletalMesh = mesh;
                Mesh.AnimSequence = anim[1];
            });
            Mesh.IsStatic = true;
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
