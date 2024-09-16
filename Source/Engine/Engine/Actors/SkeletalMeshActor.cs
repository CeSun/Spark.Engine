using Spark.Components;

namespace Spark;
public class SkeletalMeshActor : Actor
{
    public SkeletalMeshComponent SkeletalMeshComponent { get; private set; }

    public SkeletalMeshActor(World world) : base(world)
    {
        SkeletalMeshComponent = new SkeletalMeshComponent(this);
    }

}
