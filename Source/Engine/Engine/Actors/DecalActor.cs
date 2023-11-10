﻿using Spark.Engine.Assets;
using Spark.Engine.Components;

namespace Spark.Engine.Actors;

public class DecalActor : Actor
{
    public DecalActor(Level level, string Name = "") : base(level, Name)
    {
        DecalComponent = new DecalComponent(this);
    }

    public DecalComponent DecalComponent { get; private set; }

    public Material? Material
    {
        get => DecalComponent.Material;
        set => DecalComponent.Material = value;
    }
}