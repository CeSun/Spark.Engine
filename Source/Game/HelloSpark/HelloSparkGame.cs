using Silk.NET.Input;
using Spark.Core;
using Spark.Core.Actors;
using Spark.Core.Components;
using Spark.Importer;
using Spark.Util;
using System.Drawing;
using System.Numerics;

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
        light.FalloffRadius = 10;
        light.SpotLightComponent.CastShadow = false;
        SpotLightActor = light;

        DirectionalLightActor directionalLightActor = new DirectionalLightActor(world);
        directionalLightActor.Color = Color.White;
        directionalLightActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(-90f.DegreeToRadians(), -150f.DegreeToRadians(), 0);
        directionalLightActor.LightStrength = 10f;
        directionalLightActor.LightComponent.CastShadow = false;

        PointLightActor pointLightActor = new PointLightActor(world);


        CameraActor = new CameraActor(world);
        CameraActor.ClearFlag = CameraClearFlag.Skybox;
        CameraActor.ClearColor = Color.White;
        CameraActor.NearPlaneDistance = 1;
        
        var textureCube = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "Texture/table_mountain_2_puresky_1k.hdr"))
            {
                var texture = TextureImporter.ImportTextureHdrFromStream(sr, new TextureImportSetting());
                return TextureImporter.GenerateTextureCubeFromTextureHdr(texture);
            }
        });
        CameraActor.SkyboxTexture = textureCube;

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
        var staticmesh = new StaticMeshActor(world);
        staticmesh.StaticMesh = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/tree_stump.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });

        staticmesh.WorldScale = new Vector3(1f);
        staticmesh.WorldLocation = staticmesh.ForwardVector * 1 + staticmesh.UpVector * -2.8f;

        var staticmesh2 = new StaticMeshActor(world);
        staticmesh2.StaticMesh = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/brass_vase.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });

        staticmesh2.WorldScale = new Vector3(5f);
        staticmesh2.WorldLocation = staticmesh.ForwardVector * 2 + staticmesh.RightVector * 2 + staticmesh.UpVector * -2.5f;



        var staticmesh3 = new StaticMeshActor(world);
        staticmesh3.StaticMesh = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/namaqualand_boulder.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });

        staticmesh3.WorldScale = new Vector3(1f);
        staticmesh3.WorldLocation = staticmesh.ForwardVector * 3 - staticmesh.RightVector * 3  + staticmesh.UpVector * -2.8f;


        var staticmesh4 = new StaticMeshActor(world);
        var flower = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/coast_land_rocks_04.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });

        staticmesh4.StaticMesh = flower;
        staticmesh4.WorldScale = new Vector3(1f);
        staticmesh4.WorldLocation = staticmesh.UpVector * -3;

    }


    public Vector2 Euler;

    public Vector2 LastFramePos = new (-1, -1);
    public void EndPlay(World world)
    {
    }

    float yaw = 0;
    public void Update(World world, double deltaTime)
    {
        if (CameraActor == null || SpotLightActor == null) 
            return;
        if (world.Engine.MainKeyBoard == null)
            return;
        CameraActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(Euler.X.DegreeToRadians(), Euler.Y.DegreeToRadians(), 0);
        SpotLightActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(Euler.X.DegreeToRadians(), Euler.Y.DegreeToRadians(), 0);

        Vector2 Movement = Vector2.Zero;
        if (world.Engine.MainKeyBoard.IsKeyPressed(Key.W))
        {
            Movement.X += 1;
        }
        if (world.Engine.MainKeyBoard.IsKeyPressed(Key.S))
        {
            Movement.X += -1;
        }
        if (world.Engine.MainKeyBoard.IsKeyPressed(Key.D))
        {
            Movement.Y += 1;
        }
        if (world.Engine.MainKeyBoard.IsKeyPressed(Key.A))
        {
            Movement.Y += -1;
        }

        if (Movement.Length() > 0)
        {
            Movement = Vector2.Normalize(Movement);
            CameraActor.WorldLocation += (Movement.X * CameraActor.ForwardVector * (float)deltaTime / 1000 + Movement.Y * CameraActor.RightVector * (float)deltaTime / 1000);
            SpotLightActor.WorldLocation = CameraActor.WorldLocation;
        }

    }
}

