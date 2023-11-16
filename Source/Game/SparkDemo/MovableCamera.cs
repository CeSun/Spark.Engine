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

namespace SparkDemo
{
    public class MovableCamera : Actor
    {
        public float Speed = 10;
        public CameraComponent CameraComponent { get; private set; }

        bool IsClick = false;
        Vector2 LastClickPosition;
        Vector2 Rotation = Vector2.Zero;
        protected override bool ReceieveUpdate => true;
        IMouse? MoveMouse;
        IMouse? ViewMouse;
        Vector2 MovePosition;
        Vector2 ViewPosition;


        public SkeletalMeshComponent Arm;

        public SkeletalMeshComponent Wpn;

        public StaticMeshComponent Mag;


        public MovableCamera(Level level, string Name = "") : base(level, Name)
        {
            CameraComponent = new CameraComponent(this);
            CameraComponent.NearPlaneDistance = 0.1F;
            CameraComponent.FieldOfView = 55;
            if (CurrentWorld.Engine.IsMobile == false)
            {

                CurrentWorld.Engine.MainMouse.MouseDown += (mouse, Button) =>
                {
                    if (Button == MouseButton.Left)
                    {
                        IsClick = true;
                        LastClickPosition = mouse.Position;
                    }
                };
                CurrentWorld.Engine.MainMouse.MouseUp += (mouse, Button) =>
                {
                    if (Button == MouseButton.Left)
                    {
                        IsClick = false;
                    }
                };

                CurrentWorld.Engine.MainMouse.MouseMove += (mouse, position) =>
                {
                    if (IsClick == false)
                        return;
                    var delta = position - LastClickPosition;

                    Rotation += delta * 0.1F;
                    CameraComponent.RelativeRotation = Quaternion.CreateFromYawPitchRoll(-Rotation.X.DegreeToRadians(), -Rotation.Y.DegreeToRadians(), 0);

                    LastClickPosition = position;
                };
            }
            else
            {
                CurrentWorld.Engine.MainMouse.MouseDown += (mouse, button) =>
                {
                    var half = new Vector2(CurrentWorld.Engine.WindowSize.X /2, CurrentWorld.Engine.WindowSize.Y/ 2);
                    if (mouse.Position.X < half.X)
                    {
                        if (MoveMouse != null)
                            return;
                        MoveMouse = mouse;
                        MovePosition = mouse.Position;
                    }
                    else
                    {
                        if (ViewMouse != null)
                            return;
                        ViewMouse = mouse;
                        ViewPosition = mouse.Position;
                    }
                };

                CurrentWorld.Engine.MainMouse.MouseUp += (mouse, Button) =>
                {
                    if (MoveMouse == mouse)
                        MoveMouse = null;
                    if (ViewMouse == mouse)
                        ViewMouse = null;
                };

                CurrentWorld.Engine.MainMouse.MouseMove += (mouse, v) =>
                {
                    if (ViewMouse == mouse)
                    {
                        var delta = mouse.Position - ViewPosition;
                        Rotation += delta * 0.1F;
                        CameraComponent.RelativeRotation = Quaternion.CreateFromYawPitchRoll(-Rotation.X.DegreeToRadians(), -Rotation.Y.DegreeToRadians(), 0);
                        ViewPosition = mouse.Position;
                    }
                };

            }


            Arm = new SkeletalMeshComponent(this);
            Arm.AttachTo(CameraComponent, "", Matrix4x4.Identity, AttachRelation.KeepRelativeTransform);
            Arm.RelativeRotation = Quaternion.CreateFromYawPitchRoll(180F.DegreeToRadians(), 0, 0);
            Arm.RelativeScale = Vector3.One * 0.01F;
            Arm.IsCastShadowMap = false;

            var fun = async () =>
            {
                var (mesh, sk, _) = await SkeletalMesh.ImportFromGLBAsync("/StaticMesh/JasonArm.glb");
                var (_, _, anim) = await SkeletalMesh.ImportFromGLBAsync("/StaticMesh/AK47_Arm_Anim.glb");
                Arm.SkeletalMesh = mesh;
                Arm.AnimSequence = anim[0];

                var ctx = SynchronizationContext.Current;

            };
            fun();

            Wpn = new SkeletalMeshComponent(this);

            SkeletalMesh.ImportFromGLBAsync("/StaticMesh/AK47.glb").Then((res) =>
            {
                var (AK, _, akanim) = res;
                Wpn.SkeletalMesh = AK;
                Wpn.AnimSequence = akanim[0];
            });
            Wpn.AttachTo(Arm, "b_RightWeapon", Matrix4x4.Identity, AttachRelation.KeepRelativeTransform);
            Wpn.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, 90F.DegreeToRadians(), 0);
            Wpn.IsCastShadowMap = false;


            Mag = new StaticMeshComponent(this);
            StaticMesh.LoadFromGLBAsync("/StaticMesh/AK47_Magazine.glb").Then(Mesh =>
            {
                Mag.StaticMesh = Mesh;
            });
            Mag.AttachTo(Wpn, "b_Magazine_1", Matrix4x4.Identity, AttachRelation.KeepRelativeTransform);
            Mag.IsStatic = true;
            Mag.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, 90F.DegreeToRadians(), 0);
            Mag.IsCastShadowMap = false;

        }
        protected override void OnUpdate(double DeltaTime)
        {
            base.OnUpdate(DeltaTime);

            if (MoveMouse != null)
            {

                var delta = MoveMouse.Position - MovePosition;

                this.WorldLocation += ForwardVector * -delta.Y * Speed * 0.00005f;

                this.WorldLocation += RightVector * delta.X * Speed * 0.00005f;
            }
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
