using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;

namespace Demo;

/// <summary>每帧绕关节（原点）摆动骨骼网格的上段（bone1），验证 GPU 蒙皮。</summary>
[SceneTransient]
public sealed class SkeletalAnimator : Actor
{
    private readonly SkeletalMeshComponent _mesh;
    private readonly Matrix4x4[] _bones = new Matrix4x4[2];
    private float _time;

    public SkeletalAnimator(SkeletalMeshComponent mesh) => _mesh = mesh;

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
        // 上段绕 Z 轴摆动约 ±51°（关节在原点，bind pose 为单位阵 → 皮肤矩阵即骨世界）
        float angle = MathF.Sin(_time * 2f) * 0.9f;

        _bones[0] = Matrix4x4.Identity;
        _bones[1] = Matrix4x4.CreateRotationZ(angle);
        _mesh.BoneTransforms = _bones;
    }
}
