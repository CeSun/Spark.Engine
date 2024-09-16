using Spark.Core.Components;

namespace Spark.Core.Actors;
public class SkeletalMeshActor : Actor
{
    public SkeletalMeshComponent SkeletalMeshComponent { get; private set; }

    public SkeletalMeshActor(World world) : base(world)
    {
        SkeletalMeshComponent = new SkeletalMeshComponent(this);
    }

}
