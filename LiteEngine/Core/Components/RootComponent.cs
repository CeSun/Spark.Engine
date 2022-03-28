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
#pragma warning disable CS8625 // 无法将 null 字面量转换为非 null 的引用类型。
    public RootComponent(Actor owner) : base(null, "RootComponent")
    {
        Owner = owner;
    }
#pragma warning restore CS8625 // 无法将 null 字面量转换为非 null 的引用类型。

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
