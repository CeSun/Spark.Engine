using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Physics;
using System.Numerics;

namespace Spark.Engine.Components;

public class DecalComponent : PrimitiveComponent
{
    private readonly BoundingBox _boundingBox;

    private static readonly Box Box = new()
    {
        MinPoint = new Vector3(-1, -1, -1),
        MaxPoint = new Vector3(1, 1, 1)
    };
    public override BaseBounding Bounding => _boundingBox;
    protected override bool ReceiveUpdate => true;
    public DecalComponent(Actor actor) : base(actor)
    {
        _boundingBox = new BoundingBox(this);
        RefreshBoundingBox();
    }

    private void RefreshBoundingBox()
    {
        var worldTransform = WorldTransform;
        Box box = default;
        for (var i = 0; i < 8; i++)
        {
            if (i == 0)
            {
                box.MinPoint = Vector3.Transform(Box[i], worldTransform);
                box.MaxPoint = box.MinPoint;
            }
            else
            {
                box += Vector3.Transform(Box[i], worldTransform);
            }
        }
        _boundingBox.Box.MaxPoint = box.MaxPoint;
        _boundingBox.Box.MinPoint = box.MinPoint;
        UpdateOctree();
    }


    [Property]
    public Material? Material { get; set; }


    public override void OnUpdate(double deltaTime)
    {
        base.OnUpdate(deltaTime);
        RefreshBoundingBox();
    }
}
