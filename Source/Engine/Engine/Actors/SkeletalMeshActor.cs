using Spark.Core.Components;

namespace Spark.Core.Actors;
public class SkeletalMeshActor : Actor
{
    public SkeletalMeshComponent SkeletalMeshComponent { get; private set; }

    public SkeletalMeshActor(World world, bool registorToWorld = true) : base(world, registorToWorld)
    {
        SkeletalMeshComponent = new SkeletalMeshComponent(this, registorToWorld);
    }

}
