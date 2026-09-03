using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;

namespace Demo;

/// <summary>每帧让多堵墙一起绕自身中心（Y 轴）左右摆动，使墙面法线方向持续变化，观察不同方向受光。</summary>
[SceneTransient]
[EditorActor(EditorActorFlags.Internal)]
public sealed class WallSwinger : Actor
{
    private readonly StaticMeshComponent[] _walls;
    private float _time;

    public WallSwinger(params StaticMeshComponent[] walls) => _walls = walls;

    public override void Update(float deltaTime)
    {
        _time += deltaTime;
        // 摆动幅度约 ±51°，避免转到背面；光源固定，墙面法线变化 → 受光角度随之变化
        float angle = MathF.Sin(_time * 0.8f) * 0.9f;
        var rotation = Quaternion.CreateFromYawPitchRoll(angle, 0f, 0f);
        foreach (var wall in _walls)
            wall.RelativeRotation = rotation;
    }
}
