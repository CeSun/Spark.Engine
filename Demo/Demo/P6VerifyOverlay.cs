using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>
/// P6 验证场景：所有控件均不设 FixedSize（除 DockPanel 的 Top/Bottom 需要固定高度外），
/// 验证两阶段 Measure/Arrange 的内容自适应、scissor 裁剪、Tab 焦点导航、焦点环、新布局容器。
/// 
/// 替换方式：在 DemoApp.Initialize 中把
///   uiCanvas.Root = UIDemoOverlay.Build();
/// 改为
///   uiCanvas.Root = P6VerifyOverlay.Build();
/// </summary>
public static class P6VerifyOverlay
{
    public static UIElement Build()
    {
        var dock = new UIDockPanel
        {
            Padding = UIEdgeInsets.All(12f),
            BackgroundColor = new Vector4(0.08f, 0.08f, 0.10f, 0.92f),
        };

        // ===== 顶部标题条（Top 停靠，固定高度）=====
        var header = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 32f), // Top 停靠需要固定高度
            Spacing = 12f,
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            BackgroundColor = new Vector4(0.05f, 0.25f, 0.55f, 1f),
            Dock = UIDock.Top,
        };
        // ✅ 验证点1：Label 无 FixedSize，应按文字内容自适应宽度
        header.AddChild(new UILabel { Text = "🔧 P6 Verify", TextColor = Vector4.One });
        header.AddChild(new UILabel { Text = "|", TextColor = new Vector4(1f, 1f, 1f, 0.3f) });
        header.AddChild(new UILabel { Text = "No FixedSize on controls", TextColor = new Vector4(0.7f, 0.9f, 1f, 0.9f) });
        dock.AddChild(header);

        // ===== 底部状态栏（Bottom 停靠，固定高度）=====
        var status = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 24f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            BackgroundColor = new Vector4(0.05f, 0.20f, 0.40f, 1f),
            Dock = UIDock.Bottom,
        };
        status.AddChild(new UILabel { Text = "Tab: navigate focus | Click blank: clear focus | Blue ring = focused", TextColor = new Vector4(0.85f, 0.92f, 1f, 0.8f) });
        dock.AddChild(status);

        // ===== 中部主体（Fill）=====
        var body = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 8f,
            Dock = UIDock.Fill,
        };

        // --- 区域1：自适应标签 & 按钮 ---
        body.AddChild(new UILabel
        {
            Text = "▼ Auto-sizing Labels & Buttons (no FixedSize)",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        var autoRow = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 8f,
        };
        // ✅ 验证点2：不同长度文字的 Label 应有不同宽度
        autoRow.AddChild(new UILabel { Text = "Short", TextColor = Vector4.One, Padding = UIEdgeInsets.All(4f) });
        autoRow.AddChild(new UILabel { Text = "Medium length label", TextColor = Vector4.One, Padding = UIEdgeInsets.All(4f) });
        autoRow.AddChild(new UILabel { Text = "A much longer label to test adaptive width", TextColor = Vector4.One, Padding = UIEdgeInsets.All(4f) });
        body.AddChild(autoRow);

        var btnRow = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 8f,
        };
        // ✅ 验证点3：Button 按文字+Padding 自适应宽高
        int clickCount = 0;
        var clickLabel = new UILabel { Text = "Clicks: 0", TextColor = Vector4.One };
        btnRow.AddChild(new UIButton
        {
            Text = "Short",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () => { clickCount++; clickLabel.Text = $"Clicks: {clickCount}"; },
        });
        btnRow.AddChild(new UIButton
        {
            Text = "Medium Button",
            Padding = UIEdgeInsets.HorizontalVertical(12f, 6f),
            Clicked = () => { clickCount++; clickLabel.Text = $"Clicks: {clickCount}"; },
        });
        btnRow.AddChild(new UIButton
        {
            Text = "A Very Long Button Label",
            Padding = UIEdgeInsets.HorizontalVertical(16f, 8f),
            Clicked = () => { clickCount++; clickLabel.Text = $"Clicks: {clickCount}"; },
        });
        body.AddChild(btnRow);
        body.AddChild(clickLabel);

        // --- 区域2：Checkbox & Slider 自适应 ---
        body.AddChild(new UILabel
        {
            Text = "▼ Auto-sizing Checkbox & Slider",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        var checkRow = new UIStackPanel { Orientation = UIOrientation.Horizontal, Spacing = 16f };
        // ✅ 验证点4：Checkbox 按方框+文字自适应
        checkRow.AddChild(new UICheckbox { Text = "Option A", TextColor = Vector4.One });
        checkRow.AddChild(new UICheckbox { Text = "A longer option text", TextColor = Vector4.One });
        checkRow.AddChild(new UICheckbox { Text = "X", TextColor = Vector4.One });
        body.AddChild(checkRow);

        // ✅ 验证点5：Slider 无 FixedSize 时应有合理的最小高度
        var sliderRow = new UIStackPanel { Orientation = UIOrientation.Horizontal, Spacing = 8f };
        var sliderValLabel = new UILabel { Text = "0.50", TextColor = Vector4.One };
        sliderRow.AddChild(new UILabel { Text = "Slider:", TextColor = Vector4.One });
        sliderRow.AddChild(new UISlider
        {
            Value = 0.5f,
            ValueChanged = v => sliderValLabel.Text = $"{v:F2}",
        });
        sliderRow.AddChild(sliderValLabel);
        body.AddChild(sliderRow);

        // --- 区域3：TextBox 自适应 ---
        body.AddChild(new UILabel
        {
            Text = "▼ Auto-sizing TextBox (tab to focus, type to test)",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        var tbRow = new UIStackPanel { Orientation = UIOrientation.Horizontal, Spacing = 8f };
        tbRow.AddChild(new UILabel { Text = "Name:", TextColor = Vector4.One });
        tbRow.AddChild(new UITextBox
        {
            Text = "Type here...",
            Padding = UIEdgeInsets.HorizontalVertical(6f, 4f),
        });
        body.AddChild(tbRow);

        // --- 区域4：WrapPanel 演示 ---
        body.AddChild(new UILabel
        {
            Text = "▼ WrapPanel (items wrap when exceeding width)",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        var wrap = new UIWrapPanel
        {
            ItemSpacing = 6f,
            LineSpacing = 4f,
            BackgroundColor = new Vector4(0.12f, 0.14f, 0.18f, 0.8f),
            Padding = UIEdgeInsets.All(6f),
        };
        string[] tags = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa", "Lambda", "Mu" };
        foreach (var tag in tags)
        {
            wrap.AddChild(new UIButton
            {
                Text = tag,
                Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
                BackgroundColor = new Vector4(0.20f, 0.35f, 0.55f, 1f),
                HoverColor = new Vector4(0.30f, 0.45f, 0.65f, 1f),
            });
        }
        body.AddChild(wrap);

        // --- 区域5：ClipToBounds 裁剪演示 ---
        body.AddChild(new UILabel
        {
            Text = "▼ ClipToBounds (child exceeds parent, clipped)",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        var clipContainer = new UIStackPanel
        {
            FixedSize = new UISize(200f, 40f), // 固定小容器
            ClipToBounds = true,
            BackgroundColor = new Vector4(0.25f, 0.10f, 0.10f, 0.8f),
            Padding = UIEdgeInsets.All(4f),
        };
        // 子元素总宽度远超 200px，应被裁剪
        clipContainer.AddChild(new UILabel { Text = "This text is very long and should be clipped at the container boundary →→→→→→→→→→→→", TextColor = Vector4.One });
        body.AddChild(clipContainer);

        dock.AddChild(body);
        return dock;
    }
}
