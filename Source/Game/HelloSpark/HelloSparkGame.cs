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
        DirectionLightActor light = new DirectionLightActor(world);
        light.Color = Color.Pink;
        light.LightComponent.LightStrength = 1;

        CameraActor = new CameraActor(world);
        CameraActor.WorldLocation = CameraActor.WorldLocation + CameraActor.ForwardVector * -6;
        
        var staticmesh = new StaticMeshActor(world);
        var sm = await Task.Run(() =>
        {
            using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/chair.glb"))
            {
                MeshImporter.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);

                return sm;
            }
        });
        staticmesh.StaticMesh = sm;


        staticmesh.WorldRotation = Quaternion.CreateFromYawPitchRoll(10f.RadiansToDegree(), 30f.RadiansToDegree(), 150f.RadiansToDegree());

        var skeletalMesh = new SkeletalMeshActor(world);
        skeletalMesh.WorldScale = new Vector3(0.1f, 0.1f, 0.1f);
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

