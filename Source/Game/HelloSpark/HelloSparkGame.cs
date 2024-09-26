﻿using Silk.NET.Input;
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

