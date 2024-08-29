using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

[ActorInfo(DisplayOnEditor = true, Group = "Visuals")]
public class SkeletalMeshActor : Actor
{
    public SkeletalMeshComponent SkeletalMeshComponent { get; private set; }
    public SkeletalMeshActor(World.Level level, string Name = "") : base(level, Name)
    {
        SkeletalMeshComponent = new SkeletalMeshComponent(this);
    }

    [Property(IgnoreSerialize = true)]
    public AnimSequence? AnimSequence { get => SkeletalMeshComponent.AnimSequence; set => SkeletalMeshComponent.AnimSequence = value; }

    [Property(IgnoreSerialize = true)]
    public SkeletalMesh? SkeletalMesh { get => SkeletalMeshComponent.SkeletalMesh; set => SkeletalMeshComponent.SkeletalMesh = value; }
    
}
