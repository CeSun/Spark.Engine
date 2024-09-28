using Silk.NET.Input;
using Spark.Core;
using Spark.Core.Actors;
using Spark.Core.Components;
using Spark.Importer;
using Spark.Util;
using System.Drawing;

namespace HelloSpark;

public class HelloSparkGame : IGame
{
    public string Name => "HelloSpark";

    CameraActor? CameraActor;
    public void BeginPlay(World world)
    {
        CameraActor = new CameraActor(world);
        CameraActor.WorldLocation = CameraActor.WorldLocation + CameraActor.ForwardVector * -5;
        
        var staticmesh = new StaticMeshActor(world);
        using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/chair.glb"))
        {
            MeshImporter.ImporterStaticMeshFromGlbStream(sr,new StaticMeshImportSetting() { }, out var textures, out var materials, out var sm);
            staticmesh.StaticMesh = sm;
        }

        var skeletalMesh = new SkeletalMeshActor(world);
        skeletalMesh.WorldScale = new System.Numerics.Vector3(0.1f, 0.1f, 0.1f);
        using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/Jason.glb"))
        {
            MeshImporter.ImporterSkeletalMeshFromGlbStream(sr, new SkeletalMeshImportSetting(), out var textures, out var materials, out var anim, out var skeletal, out var mesh);
            skeletalMesh.SkeletalMeshComponent.SkeletalMesh = mesh;
        }
        using (var sr = world.Engine.FileSystem.GetStream("HelloSpark", "StaticMesh/AK47_Player_3P_Anim.glb"))
        {
            MeshImporter.ImporterSkeletalMeshFromGlbStream(sr, new SkeletalMeshImportSetting(), out var textures, out var materials, out var anim, out var skeletal, out var mesh);
            skeletalMesh.SkeletalMeshComponent.AnimSequence = anim[0];
        }


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

