using LiteEngine.Core.Actors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components;

public class RootComponent : Component
{
    public RootComponent(Actor owner) : base("RootComponent")
    {
        Owner = owner;
    }

    public Actor Owner { get;private set; }

    public override void Update(float deltaTime)
    {
        var scaleMat4 = Matrix4x4.CreateScale(RelativeScale);
        var rotationMat4 = Matrix4x4.CreateFromQuaternion(RelativeRotation);
        var translateMat4 = Matrix4x4.CreateTranslation(RelativeLocation);
        RelativeTransform = scaleMat4 * rotationMat4 * translateMat4;
        if (Owner != null)
        {
            WorldTransform = RelativeTransform * Owner.WorldTransform;
        }
        SubComponents.ForEach((x) => x.Update(deltaTime));

    }


}
