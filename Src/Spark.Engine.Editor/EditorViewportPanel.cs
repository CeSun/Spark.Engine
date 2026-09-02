using System.Numerics;
using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器 3D 视口面板。只管理视口区域布局，不负责相机或拾取。</summary>
internal sealed class EditorViewportPanel : UIElement
{
    private readonly UIStackPanel _panel;

    public EditorViewportPanel()
    {
        _panel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 0f),
            BackgroundColor = new Vector4(0.05f, 0.05f, 0.08f, 1f),
        };
        _panel.AddChild(new UILabel
        {
            Text = "VIEWPORT",
            TextColor = UITheme.Default.TextDimColor,
            Padding = UIEdgeInsets.HorizontalVertical(8f, 6f),
        });
        AddChild(_panel);
    }

    public void SetRenderView(UIRenderView control)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.FixedSize = new UISize(0f, 0f);
        _panel.AddChild(control);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _panel.Measure(availableSize);
        return base.OnMeasure(availableSize);
    }

    protected override void OnArrange() => _panel.Arrange(ContentRect);
}
