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
        light.InnerAngle = 15f;
        light.OuterAngle = 20f;
        light.FalloffRadius = 50;
        SpotLightActor = light;


        DirectionalLightActor directionalLightActor = new DirectionalLightActor(world);
        directionalLightActor.Color = Color.White;
        directionalLightActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(-90f.DegreeToRadians(), -120f.DegreeToRadians(), 0);
        directionalLightActor.LightStrength = 10f;

        CameraActor = new CameraActor(world);
        CameraActor.ClearColor = Color.Red;
        CameraActor.NearPlaneDistance = 1;
        var staticmesh = new StaticMeshActor(world);
        staticmesh.StaticMesh = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/tree_stump.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });

        staticmesh.WorldScale = new Vector3(10f);
        staticmesh.WorldLocation = staticmesh.ForwardVector * 20 - staticmesh.UpVector * 1;

        var staticmesh2 = new StaticMeshActor(world);
        staticmesh2.StaticMesh = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/brass_vase.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });

        staticmesh2.WorldScale = new Vector3(50f);
        staticmesh2.WorldLocation = staticmesh.ForwardVector * 5 + staticmesh.RightVector * 15 - staticmesh.UpVector * 1;



        var staticmesh3 = new StaticMeshActor(world);
        staticmesh3.StaticMesh = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/namaqualand_boulder.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });

        staticmesh3.WorldScale = new Vector3(10f);
        staticmesh3.WorldLocation = staticmesh.ForwardVector * 5 - staticmesh.RightVector * 20 - staticmesh.UpVector * 1;


        if (world.Engine.MainMouse != null)
        {
            world.Engine.MainMouse.MouseMove += (mouse, mousePos) =>
            {
                if (mouse.IsButtonPressed(MouseButton.Left) == false)
                {
                    return;
                }
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
            world.Engine.MainMouse.MouseDown += (mouse, Button) =>
            {
                if (mouse.IsButtonPressed(MouseButton.Left))
                {
                    LastFramePos = mouse.Position;
                }
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

