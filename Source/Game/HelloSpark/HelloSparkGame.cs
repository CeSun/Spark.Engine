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
    public async void BeginPlay(World world)
    {
        PointLightActor light = new PointLightActor(world);
        light.Color = Color.White;
        light.LightComponent.LightStrength = 3f;
        light.PointLightComponent.FalloffRadius = 10;

        CameraActor = new CameraActor(world);
        CameraActor.WorldLocation = CameraActor.WorldLocation + CameraActor.ForwardVector * -10 + CameraActor.UpVector * 4;
        CameraActor.ClearColor = Color.White;


        var staticmesh = new StaticMeshActor(world);
        var sm = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/WoodenCrate.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });
        staticmesh.StaticMesh = sm;

        staticmesh.WorldLocation += staticmesh.RightVector * 5;

        staticmesh.WorldRotation = Quaternion.CreateFromYawPitchRoll(135f.RadiansToDegree(),0, 0);
        staticmesh.WorldScale = new Vector3(1);


        var skeletalMesh = new SkeletalMeshActor(world);
        skeletalMesh.WorldScale = new Vector3(0.1f, 0.1f, 0.1f);
        skeletalMesh.WorldRotation = Quaternion.CreateFromYawPitchRoll(-90f.RadiansToDegree(), 0, 0);
        skeletalMesh.WorldLocation -= skeletalMesh.ForwardVector * 1;
        var mesh = await Task.Run(() =>
        {

            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/Jason.glb"))
            {
                MeshImporter.ImporterSkeletalMeshFromGlbStream(sr, new SkeletalMeshImportSetting(), out var textures, out var materials, out var anim, out var skeletal, out var mesh);
                
                return mesh;
            }
        });
        skeletalMesh.SkeletalMeshComponent.SkeletalMesh = mesh;
        var anim = await Task.Run(() =>
        {

            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/AK47_Player_3P_Anim.glb"))
            {
                MeshImporter.ImporterSkeletalMeshFromGlbStream(sr, new SkeletalMeshImportSetting(), out var textures, out var materials, out var anim, out var skeletal, out var mesh);
                return anim[0];
            }

        });
        skeletalMesh.SkeletalMeshComponent.AnimSequence = anim;

    }

    public void EndPlay(World world)
    {
    }

    public void Update(World world, double deltaTime)
    {
        if (CameraActor != null)
        {
            // CameraActor.WorldLocation += new System.Numerics.Vector3(0.0001f, 0, 0);
        }
    }
}

