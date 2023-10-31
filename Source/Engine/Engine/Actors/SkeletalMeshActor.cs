using Spark.Engine.Assets;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public class SkeletalMeshActor : Actor
{
    public SkeletalMeshComponent SkeletalMeshComponent { get; private set; }
    public SkeletalMeshActor(Level level, string Name = "") : base(level, Name)
    {
        SkeletalMeshComponent = new SkeletalMeshComponent(this);
    }

    public AnimSequence? AnimSequence { get => SkeletalMeshComponent.AnimSequence; set => SkeletalMeshComponent.AnimSequence = value; }

    public SkeletalMesh? SkeletalMesh { get => SkeletalMeshComponent.SkeletalMesh; set => SkeletalMeshComponent.SkeletalMesh = value; }
    
}
