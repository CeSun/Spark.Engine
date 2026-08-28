using System.Numerics;
using Spark.Engine;
using Spark.Engine.Platforms;
using Spark.Engine.UI;

namespace Demo;

/// <summary>
/// 引擎画面显示控件（UIRenderView）演示：
/// 创建离屏渲染目标 → 相机渲染到该目标 → UIRenderView 控件在 UI 中实时显示渲染画面。
/// 控件开启 AutoResize：显示区域变化时自动重建离屏目标，保持 1:1 分辨率，画面清晰。
/// </summary>
public static class RenderViewOverlay
{
    /// <summary>
    /// 在指定窗口的 UI 画布上挂载渲染视图控件。
    /// </summary>
    /// <param name="app">引擎应用（用于访问 UIManager）。</param>
    /// <param name="window">目标窗口。</param>
    /// <param name="renderView">UIRenderView 控件（含 RenderViewId 与自适应回调）。</param>
    public static void Attach(EngineApplication app, IWindow window, UIRenderView renderView)
    {
        var viewport = app.WindowManager.GetViewport(window)!;
        var canvas = app.UIManager.GetOrCreateCanvas(viewport.Id);
        canvas.Root = Build(renderView);
    }

    /// <summary>构建渲染视图演示 UI 树：标题 + 画面控件 + 说明。</summary>
    private static UIElement Build(UIRenderView renderView)
    {
        var dock = new UIDockPanel
        {
            Padding = UIEdgeInsets.All(8f),
            BackgroundColor = new Vector4(0.06f, 0.06f, 0.08f, 1f),
        };

        // 顶部标题
        var header = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 28f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            BackgroundColor = new Vector4(0.05f, 0.25f, 0.55f, 1f),
            Dock = UIDock.Top,
        };
        header.AddChild(new UILabel
        {
            Text = "UIRenderView - Engine View Control",
            TextColor = Vector4.One,
        });
        dock.AddChild(header);

        // 底部说明（Bottom 必须在 Fill 之前声明，否则 LastChildFill 会把最后一个子元素强制为 Fill，
        // 导致 status 被安排到 (0,0,0,0) 且文字画到窗口左上角与 header 重叠）
        var status = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 22f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            BackgroundColor = new Vector4(0.05f, 0.20f, 0.40f, 1f),
            Dock = UIDock.Bottom,
        };
        status.AddChild(new UILabel
        {
            Text = "Offscreen camera view (AutoResize adaptive resolution)",
            TextColor = new Vector4(0.85f, 0.92f, 1f, 0.9f),
        });
        dock.AddChild(status);

        // 中部：渲染视图控件（Fill 填满剩余区域，自动保持宽高比 + 自适应分辨率）
        // 作为最后一个子元素声明，符合 LastChildFill 语义
        renderView.BackgroundColor = new Vector4(0.02f, 0.02f, 0.03f, 1f);
        renderView.Dock = UIDock.Fill;
        dock.AddChild(renderView);

        return dock;
    }
}
