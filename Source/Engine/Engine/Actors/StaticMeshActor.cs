﻿using Spark.Assets;
using Spark.Components;

namespace Spark;

public class StaticMeshActor : Actor
{
    public StaticMeshComponent StaticMeshComponent { get; private set; }
    public StaticMeshActor(World world) : base(world)
    {
        StaticMeshComponent = new StaticMeshComponent(this);
    }

    public bool IsStatic
    {
        get => StaticMeshComponent.IsStatic;
        set => StaticMeshComponent.IsStatic = value;
    }

    public StaticMesh? StaticMesh
    {
        get => StaticMeshComponent.StaticMesh;
        set => StaticMeshComponent.StaticMesh = value;
    }

}
