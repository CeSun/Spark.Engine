using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Physics;
using System.Numerics;

namespace Spark.Engine.Components;

public class DecalComponent : PrimitiveComponent
{
    BoundingBox BoundingBox;
    static readonly Box Box = new Box
    {
        MinPoint = new Vector3(-1, -1, -1),
        MaxPoint = new Vector3(1, 1, 1)
    };
    public override BaseBounding? Bounding => BoundingBox;
    protected override bool ReceieveUpdate => true;
    public DecalComponent(Actor actor) : base(actor)
    {
        BoundingBox = new BoundingBox(this);
        RefreshBoudingBox();
    }

    private void RefreshBoudingBox()
    {
        var worldTransform = WorldTransform;
        Box box = default;
        for (int i = 0; i < 8; i++)
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
        BoundingBox.Box.MaxPoint = box.MaxPoint;
        BoundingBox.Box.MinPoint = box.MinPoint;
        UpdateOctree();
    }


    private Material? _Material;
    public Material? Material 
    {
        get => _Material;
        set
        {
            _Material = value;
            if (_Material != null)
            {
                Engine.NextFrame.Add(() =>
                {
                    foreach (var texture in _Material.Textures)
                    {
                        if (texture != null)
                            texture.InitRender(gl);
                    }
                });

            }
        }
    }


    public override void OnUpdate(double DeltaTime)
    {
        base.OnUpdate(DeltaTime);
        RefreshBoudingBox();
    }
}
