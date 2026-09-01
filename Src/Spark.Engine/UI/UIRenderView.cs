using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>
/// 引擎画面显示控件：将一个渲染目标（离屏纹理）的内容显示在 UI 中。
/// <para>
/// 使用方式：
/// 1. 创建一个 <see cref="Render.Common.TextureRenderTarget"/> 作为离屏渲染目标
/// 2. 创建一个 <see cref="Components.CameraComponent"/> 并将其 RenderTarget 设为上述目标
/// 3. 创建 UIRenderView 并设置 RenderViewId（通过 UIManager.RegisterRenderView 获取）
/// 4. 将 UIRenderView 添加到 UI 树中
/// </para>
/// <para>
/// 注意：TextureRenderTarget 是渲染线程对象，逻辑线程通过 ID 间接引用。
/// 需要在 UIManager 中注册后才能使用。
/// </para>
/// <para>
/// 画面清晰度：离屏目标分辨率固定时，若显示区域大于目标分辨率会产生放大模糊。
/// 开启 <see cref="AutoResize"/> 后，控件会随显示区域动态请求重建离屏目标
/// （经 <see cref="RenderViewResizeRequested"/> 回调），使分辨率与显示尺寸匹配；
/// <see cref="ResolutionScale"/> 可额外超采样（&gt;1 更锐利，GPU 开销更大）。
/// </para>
/// </summary>
public sealed class UIRenderView : UIElement
{
    /// <summary>渲染视图 ID（由 UIManager.RegisterRenderView 分配）。</summary>
    public int RenderViewId { get; set; }

    /// <summary>背景色（当渲染视图无效或未注册时显示）。</summary>
    public Vector4 BackgroundColor { get; set; } = new Vector4(0.05f, 0.05f, 0.08f, 1f);

    /// <summary>是否保持宽高比（根据渲染目标尺寸自动调整显示区域）。</summary>
    public bool MaintainAspectRatio { get; set; } = true;

    /// <summary>显示尺寸变化时自动请求重建离屏渲染目标（消除放大模糊）。</summary>
    public bool AutoResize { get; set; } = true;

    /// <summary>超采样倍率（离屏分辨率 = 显示尺寸 × 此值；&gt;1 时缩小显示更锐利）。</summary>
    public float ResolutionScale { get; set; } = 1f;

    /// <summary>触发重建的尺寸变化阈值（逻辑像素；低于此值的抖动不触发重建）。</summary>
    public float ResizeThreshold { get; set; } = 8f;

    /// <summary>
    /// 请求重建渲染视图：参数为（当前 RenderViewId，期望宽，期望高），返回新 RenderViewId（&gt;0）或 0（拒绝）。
    /// 典型实现：创建新离屏目标 → 更新相机 RenderTarget → 延迟释放旧目标 → 返回新 Id。
    /// </summary>
    public Func<int, uint, uint, int>? RenderViewResizeRequested { get; set; }

    public UIRenderView()
    {
        ClipToBounds = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        // A zero FixedSize is the fill marker used by editor work areas.
        if (FixedSize is { } fill && fill.Width <= 0f && fill.Height <= 0f)
            return new UISize(0f, 0f);

        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        // 默认填充可用空间
        float w = availableSize.Width > 0f ? availableSize.Width : 320f;
        float h = availableSize.Height > 0f ? availableSize.Height : 240f;

        if (FixedSize is { } fsv)
        {
            if (fsv.Width > 0f) w = fsv.Width;
            if (fsv.Height > 0f) h = fsv.Height;
        }

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        // 自适应分辨率：显示尺寸与离屏目标尺寸不匹配时请求重建（本帧后续相机收集即可用新目标）
        TryRequestResize(ui);

        // 绘制背景
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);

        // 绘制渲染视图内容
        if (RenderViewId > 0)
        {
            var rect = CalculateDisplayRect(ui);
            ui.DrawRenderView(targetId, RenderViewId, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height));
        }
    }

    /// <summary>显示尺寸变化超过阈值时请求重建离屏渲染目标。</summary>
    private void TryRequestResize(UIManager ui)
    {
        if (!AutoResize || RenderViewId <= 0 || RenderViewResizeRequested == null)
            return;

        if (Bounds.Width <= 0f || Bounds.Height <= 0f)
            return;

        uint desiredW = (uint)MathF.Max(1f, Bounds.Width * ResolutionScale);
        uint desiredH = (uint)MathF.Max(1f, Bounds.Height * ResolutionScale);

        // 与当前实际目标尺寸比较，低于阈值不触发（防抖，避免窗口抖动时频繁重建）
        var (currentW, currentH) = ui.GetRenderViewSize(RenderViewId);
        if (currentW > 0 && currentH > 0 &&
            System.Math.Abs((int)desiredW - (int)currentW) < ResizeThreshold &&
            System.Math.Abs((int)desiredH - (int)currentH) < ResizeThreshold)
            return;

        int newId = RenderViewResizeRequested(RenderViewId, desiredW, desiredH);
        if (newId > 0)
            RenderViewId = newId;
    }

    /// <summary>计算实际显示区域（考虑宽高比保持）。</summary>
    private UIRect CalculateDisplayRect(UIManager ui)
    {
        if (!MaintainAspectRatio || RenderViewId <= 0)
            return new UIRect(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);

        var viewSize = ui.GetRenderViewSize(RenderViewId);
        if (viewSize.Width <= 0 || viewSize.Height <= 0)
            return new UIRect(Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);

        float viewAspect = viewSize.Width / (float)viewSize.Height;
        float boundsAspect = Bounds.Width / Bounds.Height;

        float displayWidth, displayHeight;
        float offsetX = 0f, offsetY = 0f;

        if (viewAspect > boundsAspect)
        {
            // 渲染视图更宽，以宽度为基准
            displayWidth = Bounds.Width;
            displayHeight = displayWidth / viewAspect;
            offsetY = (Bounds.Height - displayHeight) * 0.5f;
        }
        else
        {
            // 渲染视图更高，以高度为基准
            displayHeight = Bounds.Height;
            displayWidth = displayHeight * viewAspect;
            offsetX = (Bounds.Width - displayWidth) * 0.5f;
        }

        return new UIRect(Bounds.X + offsetX, Bounds.Y + offsetY, displayWidth, displayHeight);
    }
}
