using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;

namespace Spark.Engine;
public class SkeletalMeshActor : Actor
{
    public SkeletalMeshComponent SkeletalMeshComponent { get; private set; }
    public SkeletalMeshActor(World.Level level) : base(level)
    {
        SkeletalMeshComponent = new SkeletalMeshComponent(this);
    }

    public AnimSequence? AnimSequence { get => SkeletalMeshComponent.AnimSequence; set => SkeletalMeshComponent.AnimSequence = value; }

    public SkeletalMesh? SkeletalMesh { get => SkeletalMeshComponent.SkeletalMesh; set => SkeletalMeshComponent.SkeletalMesh = value; }
    
}
