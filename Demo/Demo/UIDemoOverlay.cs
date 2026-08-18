using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>P2~P4 演示：保留模式控件树（停靠布局 + 盒子布局 + 文本标签 + 按钮 + 输入框），验证布局、绘制、文本与交互。</summary>
public static class UIDemoOverlay
{
    public static UIElement Build()
    {
        // 停靠布局：顶部标题条（Top）+ 底部状态栏（Bottom）+ 中部主体（Fill）
        var dock = new UIDockPanel
        {
            Padding = UIEdgeInsets.All(12f),
            BackgroundColor = new Vector4(0.08f, 0.08f, 0.10f, 0.92f),
        };

        // 标题条（Top 停靠）
        var header = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 28f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            BackgroundColor = new Vector4(0.05f, 0.25f, 0.55f, 1f),
            Dock = UIDock.Top,
        };
        header.AddChild(new UILabel { Text = "Spark.Engine UI", TextColor = new Vector4(1f, 1f, 1f, 1f) });
        dock.AddChild(header);

        // 状态栏（Bottom 停靠）
        var status = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 24f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            BackgroundColor = new Vector4(0.05f, 0.20f, 0.40f, 1f),
            Dock = UIDock.Bottom,
        };
        status.AddChild(new UILabel { Text = "Status: Ready", TextColor = new Vector4(0.85f, 0.92f, 1f, 0.9f) });
        dock.AddChild(status);

        // 中部主体（Fill，最后一个子元素填满剩余区域）
        var body = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.HorizontalVertical(4f, 2f),
            Spacing = 6f,
            Dock = UIDock.Fill,
        };

        int counter = 0;
        var counterLabel = new UILabel { Text = "Clicks: 0", TextColor = new Vector4(0.85f, 0.92f, 1f, 0.95f), FixedSize = new UISize(0f, 20f) };
        body.AddChild(counterLabel);

        var button = new UIButton
        {
            Text = "Click me",
            FixedSize = new UISize(0f, 26f),
            Padding = UIEdgeInsets.HorizontalVertical(10f, 4f),
            Clicked = () =>
            {
                counter++;
                counterLabel.Text = $"Clicks: {counter}";
            },
        };
        body.AddChild(button);

        var textBox = new UITextBox
        {
            FixedSize = new UISize(0f, 26f),
            Padding = UIEdgeInsets.HorizontalVertical(6f, 4f),
            Text = "Type here...",
        };
        body.AddChild(textBox);

        var checkbox = new UICheckbox
        {
            Text = "Enable option",
            TextColor = new Vector4(0.85f, 0.92f, 1f, 0.9f),
            FixedSize = new UISize(0f, 20f),
        };
        body.AddChild(checkbox);

        var sliderLabel = new UILabel { Text = "Value: 0.50", TextColor = new Vector4(0.85f, 0.92f, 1f, 0.85f), FixedSize = new UISize(0f, 20f) };
        body.AddChild(sliderLabel);

        var slider = new UISlider
        {
            FixedSize = new UISize(0f, 18f),
            Value = 0.5f,
            ValueChanged = value => sliderLabel.Text = $"Value: {value:F2}",
        };
        body.AddChild(slider);

        dock.AddChild(body);
        return dock;
    }
}
