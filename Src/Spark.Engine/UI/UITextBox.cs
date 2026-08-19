using System.Diagnostics;
using System.Numerics;
using System.Text;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>单行文本输入框：点击聚焦，接收文本输入与编辑键（退格/删除/方向/Home/End），绘制光标。</summary>
public sealed class UITextBox : UIElement
{
    private readonly StringBuilder _buffer = new();
    private int _cursor;
    private bool _focused;

    /// <summary>光标闪烁计时：可见 530ms / 隐藏 530ms（与 Windows 默认闪烁周期一致）。</summary>
    private readonly Stopwatch _blinkTimer = Stopwatch.StartNew();

    public Vector4 BackgroundColor { get; set; } = new Vector4(0.10f, 0.12f, 0.16f, 1f);

    public Vector4 TextColor { get; set; } = Vector4.One;

    public string Text
    {
        get => _buffer.ToString();
        set
        {
            _buffer.Clear();
            _buffer.Append(value);
            _cursor = System.Math.Clamp(_cursor, 0, _buffer.Length);
        }
    }

    private const float DefaultMinHeight = 24f;

    public UITextBox()
    {
        Focusable = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        var textRenderer = GetTextRenderer();
        float textW = 0f;
        if (textRenderer != null && _buffer.Length > 0)
        {
            textW = textRenderer.Measure(_buffer.ToString()).X;
        }

        float w = FixedSize is { } fsv && fsv.Width > 0f ? fsv.Width : System.Math.Max(60f, textW + Padding.Left + Padding.Right + 10f);
        float h = FixedSize is { } fsv2 && fsv2.Height > 0f ? fsv2.Height : DefaultMinHeight;

        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        var rect = Bounds;

        ui.DrawRect(targetId, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), BackgroundColor);

        string text = _buffer.ToString();
        float textX = rect.X + Padding.Left;
        float textY = rect.Y + Padding.Top;
        ui.Text.DrawText(ui, targetId, text, new Vector2(textX, textY), TextColor);

        if (_focused && IsCursorVisible())
        {
            float cursorX = textX + ui.Text.Measure(_buffer.ToString(0, _cursor)).X;
            float cursorHeight = ui.Text.Measure(" ").Y;
            ui.DrawRect(targetId, new Vector2(cursorX, textY), new Vector2(1.5f, cursorHeight), new Vector4(1f, 1f, 1f, 0.9f));
        }
    }

    /// <summary>光标闪烁相位：可见 530ms 后隐藏 530ms 循环。</summary>
    private bool IsCursorVisible()
    {
        const long OnMs = 530;
        return _blinkTimer.ElapsedMilliseconds % (OnMs * 2) < OnMs;
    }

    protected internal override void OnFocusChanged(bool focused)
    {
        _focused = focused;
        if (focused)
            _blinkTimer.Restart();
    }

    protected internal override void OnTextInput(string text)
    {
        _buffer.Insert(_cursor, text);
        _cursor += text.Length;
        _blinkTimer.Restart();
    }

    protected internal override void OnKeyDown(Key key)
    {
        _blinkTimer.Restart(); // 任意按键都让光标立即可见并重置闪烁周期
        switch (key)
        {
            case Key.Backspace when _cursor > 0:
                _buffer.Remove(_cursor - 1, 1);
                _cursor--;
                break;
            case Key.Delete when _cursor < _buffer.Length:
                _buffer.Remove(_cursor, 1);
                break;
            case Key.Left:
                _cursor = System.Math.Max(0, _cursor - 1);
                break;
            case Key.Right:
                _cursor = System.Math.Min(_buffer.Length, _cursor + 1);
                break;
            case Key.Home:
                _cursor = 0;
                break;
            case Key.End:
                _cursor = _buffer.Length;
                break;
        }
    }
}
