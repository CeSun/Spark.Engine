using System.Numerics;

namespace Spark.Engine.Math;

/// <summary>
/// 视锥体：6 个裁剪平面（正半侧为内部）。由视图×投影矩阵提取（Gribb-Hartmann 方法），
/// 供渲染线程做包围球剔除。
/// </summary>
public struct Frustum
{
    public Plane Left;
    public Plane Right;
    public Plane Bottom;
    public Plane Top;
    public Plane Near;
    public Plane Far;

    /// <summary>
    /// 从（视图 × 投影）行主序矩阵提取视锥平面。
    /// 约定：System.Numerics 为行主序；WebGPU 裁剪空间 z ∈ [0, 1]（近平面取第三列）。
    /// </summary>
    public static Frustum FromViewProjection(in Matrix4x4 viewProjection)
    {
        var m = viewProjection;

        Vector4 col1 = new(m.M11, m.M21, m.M31, m.M41);
        Vector4 col2 = new(m.M12, m.M22, m.M32, m.M42);
        Vector4 col3 = new(m.M13, m.M23, m.M33, m.M43);
        Vector4 col4 = new(m.M14, m.M24, m.M34, m.M44);

        Frustum frustum;
        frustum.Left = Plane.Normalize(new Plane(col4.X + col1.X, col4.Y + col1.Y, col4.Z + col1.Z, col4.W + col1.W));
        frustum.Right = Plane.Normalize(new Plane(col4.X - col1.X, col4.Y - col1.Y, col4.Z - col1.Z, col4.W - col1.W));
        frustum.Bottom = Plane.Normalize(new Plane(col4.X + col2.X, col4.Y + col2.Y, col4.Z + col2.Z, col4.W + col2.W));
        frustum.Top = Plane.Normalize(new Plane(col4.X - col2.X, col4.Y - col2.Y, col4.Z - col2.Z, col4.W - col2.W));
        frustum.Near = Plane.Normalize(new Plane(col3.X, col3.Y, col3.Z, col3.W));
        frustum.Far = Plane.Normalize(new Plane(col4.X - col3.X, col4.Y - col3.Y, col4.Z - col3.Z, col4.W - col3.W));
        return frustum;
    }

    /// <summary>包围球与视锥是否相交（球完全落在任一平面负侧外则不相交）。</summary>
    public bool Intersects(in BoundingSphere sphere)
    {
        if (Plane.DotCoordinate(Left, sphere.Center) < -sphere.Radius) return false;
        if (Plane.DotCoordinate(Right, sphere.Center) < -sphere.Radius) return false;
        if (Plane.DotCoordinate(Bottom, sphere.Center) < -sphere.Radius) return false;
        if (Plane.DotCoordinate(Top, sphere.Center) < -sphere.Radius) return false;
        if (Plane.DotCoordinate(Near, sphere.Center) < -sphere.Radius) return false;
        if (Plane.DotCoordinate(Far, sphere.Center) < -sphere.Radius) return false;
        return true;
    }
}
