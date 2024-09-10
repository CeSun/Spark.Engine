﻿using Spark.Engine.Components;

namespace Spark.Engine;
public class SkeletalMeshActor : Actor
{
    public SkeletalMeshComponent SkeletalMeshComponent { get; private set; }

    public SkeletalMeshActor(World world) : base(world)
    {
        SkeletalMeshComponent = new SkeletalMeshComponent(this);
    }

}
