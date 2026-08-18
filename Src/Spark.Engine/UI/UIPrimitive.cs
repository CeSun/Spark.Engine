using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>
/// 单个 UI 绘制基元（屏幕空间四边形，值类型，随 <see cref="SceneSnapshot"/> 每帧快照）。
/// 逻辑线程填充，渲染线程在 UI overlay pass 里转成 NDC 顶点提交。
/// </summary>
public struct UIPrimitive
{
    /// <summary>目标渲染目标 ID（对应窗口视口 TargetId）。</summary>
    public int TargetId;

    /// <summary>矩形：x, y, width, height（窗口逻辑像素，左上原点，Y 向下）。</summary>
    public Vector4 Rect;

    /// <summary>纹理 UV：u0, v0, u1, v1（当前固定整张纹理）。</summary>
    public Vector4 UV;

    /// <summary>着色颜色 RGBA（0..1，乘在纹理采样之上）。</summary>
    public Vector4 Color;

    /// <summary>纹理 ID：0 = 内置白色纹理（纯色）；&gt;0 = 已上传的 UI 纹理（文本/图片）。</summary>
    public int TextureId;
}
