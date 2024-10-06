using Silk.NET.Input;
using Spark.Core;
using Spark.Core.Actors;
using Spark.Core.Components;
using Spark.Importer;
using Spark.Util;
using System.Drawing;
using System.Numerics;
using System.Runtime.Intrinsics.X86;

namespace HelloSpark;

public class HelloSparkGame : IGame
{
    public string Name => "HelloSpark";

    CameraActor? CameraActor;
    SpotLightActor? SpotLightActor;
    public async void BeginPlay(World world)
    {
        SpotLightActor light = new SpotLightActor(world);
        light.Color = Color.White;
        light.LightStrength = 1f;
        light.InnerAngle = 20;
        light.OuterAngle = 30;
        light.FalloffRadius = 100;
        SpotLightActor = light;

        CameraActor = new CameraActor(world);
        CameraActor.ClearColor = Color.White;

        var staticmesh = new StaticMeshActor(world);
        staticmesh.StaticMesh = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/Jason.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });

        staticmesh.WorldLocation = CameraActor.ForwardVector * 50 + CameraActor.UpVector * -50;


        if (world.Engine.MainMouse != null)
        {
            world.Engine.MainMouse.MouseMove += (mouse, mousePos) =>
            {
                if (LastFramePos.X < 0 || mousePos.Y < 0)
                    LastFramePos = mousePos;
                Euler.X += (mousePos - LastFramePos).X * 0.03f;
                Euler.Y += (mousePos - LastFramePos).Y * 0.03f;

                if (Euler.Y > 89.0)
                    Euler.Y = 89;
                if (Euler.Y < -89.0)
                    Euler.Y = -89;

                LastFramePos = mousePos;

            };
        }
    }


    public Vector2 Euler;

    public Vector2 LastFramePos = new (-1, -1);
    public void EndPlay(World world)
    {
    }

    float yaw = 0;
    public void Update(World world, double deltaTime)
    {
        CameraActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(Euler.X.DegreeToRadians(), Euler.Y.DegreeToRadians(), 0);
        SpotLightActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(Euler.X.DegreeToRadians(), Euler.Y.DegreeToRadians(), 0);

    }
}

