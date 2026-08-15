using System.Numerics;

namespace Spark.Engine.Math;

/// <summary>
/// 世界空间包围球，供渲染线程做视锥剔除。
/// 由 <see cref="Spark.Engine.Render.SceneProxy.Bounds"/> 携带，随场景快照传入渲染线程。
/// </summary>
public readonly struct BoundingSphere
{
    /// <summary>球心。</summary>
    public readonly Vector3 Center;

    /// <summary>半径。</summary>
    public readonly float Radius;

    public BoundingSphere(Vector3 center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    /// <summary>由一组点构造包围球（中心取包围盒中心，半径取最远点距离）。</summary>
    public static BoundingSphere CreateFromPoints(ReadOnlySpan<Vector3> points)
    {
        if (points.Length == 0)
            return default;

        var min = points[0];
        var max = points[0];
        for (int i = 1; i < points.Length; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }

        var center = (min + max) * 0.5f;
        float radius = 0f;
        for (int i = 0; i < points.Length; i++)
            radius = MathF.Max(radius, Vector3.Distance(center, points[i]));

        return new BoundingSphere(center, radius);
    }

    /// <summary>应用世界变换：平移/旋转球心，半径按最大轴缩放。</summary>
    public BoundingSphere Transform(in Matrix4x4 transform)
    {
        var center = Vector3.Transform(Center, transform);

        float scaleX = new Vector3(transform.M11, transform.M12, transform.M13).Length();
        float scaleY = new Vector3(transform.M21, transform.M22, transform.M23).Length();
        float scaleZ = new Vector3(transform.M31, transform.M32, transform.M33).Length();
        float maxScale = MathF.Max(scaleX, MathF.Max(scaleY, scaleZ));

        return new BoundingSphere(center, Radius * maxScale);
    }

    /// <summary>点是否在球内（含边界）。</summary>
    public bool Contains(in Vector3 point) => Vector3.DistanceSquared(Center, point) <= Radius * Radius;

    /// <summary>与另一包围球是否相交。</summary>
    public bool Intersects(in BoundingSphere other)
    {
        float radii = Radius + other.Radius;
        return Vector3.DistanceSquared(Center, other.Center) <= radii * radii;
    }

    /// <summary>与视锥是否相交。</summary>
    public bool Intersects(in Frustum frustum) => frustum.Intersects(in this);
}
