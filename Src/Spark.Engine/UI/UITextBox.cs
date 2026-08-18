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

    public UITextBox()
    {
        Focusable = true;
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        var rect = Bounds;

        ui.DrawRect(targetId, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), BackgroundColor);

        string text = _buffer.ToString();
        float textX = rect.X + Padding.Left;
        float textY = rect.Y + Padding.Top;
        ui.Text.DrawText(ui, targetId, text, new Vector2(textX, textY), TextColor);

        if (_focused)
        {
            float cursorX = textX + ui.Text.Measure(_buffer.ToString(0, _cursor)).X;
            float cursorHeight = ui.Text.Measure(" ").Y;
            ui.DrawRect(targetId, new Vector2(cursorX, textY), new Vector2(1.5f, cursorHeight), new Vector4(1f, 1f, 1f, 0.9f));
        }
    }

    protected internal override void OnFocusChanged(bool focused) => _focused = focused;

    protected internal override void OnTextInput(string text)
    {
        _buffer.Insert(_cursor, text);
        _cursor += text.Length;
    }

    protected internal override void OnKeyDown(Key key)
    {
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
