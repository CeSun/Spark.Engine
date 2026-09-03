using System.Diagnostics;
using System.Numerics;
using System.Text;
using Spark.Engine.Input;

namespace Spark.Engine.UI;

/// <summary>
/// 单行文本输入框：支持光标、鼠标定位/拖选、Shift/Ctrl 导航、剪贴板和 Undo/Redo。
/// 支持 IME 单行组合态预览；多行排版保留给后续版本。
/// </summary>
public sealed class UITextBox : UIElement
{
    private readonly StringBuilder _buffer = new();
    private readonly Stack<TextEdit> _undo = new();
    private readonly Stack<TextEdit> _redo = new();
    private int _cursor;
    private int _selectionAnchor;
    private bool _focused;
    private bool _draggingSelection;
    private Vector2 _lastPointerPosition;
    private float _horizontalOffset;
    private EditKind _lastEditKind;
    private string _compositionText = string.Empty;
    private bool _isComposing;

    private readonly Stopwatch _blinkTimer = Stopwatch.StartNew();

    private enum EditKind
    {
        None,
        Insert,
        Delete,
        Replace,
    }

    private sealed record TextEdit(
        string BeforeText,
        int BeforeCursor,
        int BeforeAnchor,
        string AfterText,
        int AfterCursor,
        int AfterAnchor,
        EditKind Kind);

    public Vector4 BackgroundColor { get; set; } = new(0.10f, 0.12f, 0.16f, 1f);

    public Vector4 TextColor { get; set; } = Vector4.One;

    public Vector4 SelectionColor { get; set; } = new(0.20f, 0.45f, 0.80f, 0.85f);

    public string PlaceholderText { get; set; } = string.Empty;

    public Vector4 PlaceholderColor { get; set; } = new(0.60f, 0.62f, 0.66f, 1f);

    public bool ReadOnly { get; set; }

    public int MaxLength { get; set; }

    /// <summary>设置后以该字符绘制文本，原始文本仍通过 Text 暴露；null 表示不掩码。</summary>
    public char? MaskChar { get; set; }

    public IClipboard? Clipboard { get; set; }

    public Action<string>? TextChanged { get; set; }

    /// <summary>单行输入框按下 Enter 时提交当前文本。</summary>
    public Action<string>? Submitted { get; set; }

    /// <summary>单行输入框按下 Escape 时取消当前编辑。</summary>
    public Action? Cancelled { get; set; }

    /// <summary>输入框获得或失去画布焦点时触发。</summary>
    public Action<bool>? FocusChanged { get; set; }

    public string Text
    {
        get => _buffer.ToString();
        set
        {
            var next = value ?? string.Empty;
            if (MaxLength > 0 && next.Length > MaxLength)
                next = next[..MaxLength];

            _buffer.Clear();
            _buffer.Append(next);
            _cursor = _buffer.Length;
            _selectionAnchor = _cursor;
            _undo.Clear();
            _redo.Clear();
            _lastEditKind = EditKind.None;
            EnsureCursorVisible();
        }
    }

    public int CursorPosition => _cursor;

    public int SelectionStart => System.Math.Min(_cursor, _selectionAnchor);

    public int SelectionLength => System.Math.Abs(_cursor - _selectionAnchor);

    public bool HasSelection => SelectionLength > 0;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public Vector2? ImeCandidatePosition
    {
        get
        {
            if (!_focused || GetTextRenderer() is not { } renderer)
                return null;
            var display = GetPreviewText(out var visualCursor);
            var x = Bounds.X + Padding.Left - _horizontalOffset + renderer.Measure(display[..visualCursor]).X;
            return new Vector2(x, Bounds.Y + Padding.Top + renderer.LineHeight);
        }
    }

    private const float DefaultMinHeight = 24f;

    public UITextBox()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        var textRenderer = GetTextRenderer();
        float textW = textRenderer == null ? 0f : textRenderer.Measure(DisplayText).X;
        // An explicitly supplied FixedSize with width <= 0 is the layout
        // system's fill marker. Only an omitted FixedSize uses content sizing.
        float w = FixedSize is { } fsv
            ? (fsv.Width > 0f ? fsv.Width : 0f)
            : System.Math.Max(60f, textW + Padding.Left + Padding.Right + 10f);
        float h = FixedSize is { } fsv2 && fsv2.Height > 0f ? fsv2.Height : DefaultMinHeight;
        return new UISize(w, h);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        var rect = Bounds;
        ui.DrawRect(targetId, new Vector2(rect.X, rect.Y), new Vector2(rect.Width, rect.Height), BackgroundColor);

        var textRenderer = ui.Text;
        EnsureCursorVisible();
        string displayText = GetPreviewText(out var visualCursor);
        bool showPlaceholder = _buffer.Length == 0 && !string.IsNullOrEmpty(PlaceholderText) && !_focused;
        string shownText = showPlaceholder ? PlaceholderText : displayText;

        float textX = rect.X + Padding.Left - _horizontalOffset;
        float textY = rect.Y + Padding.Top;

        if (!showPlaceholder && HasSelection && !_isComposing)
        {
            float selectionX = textX + textRenderer.Measure(displayText[..SelectionStart]).X;
            float selectionW = textRenderer.Measure(displayText.Substring(SelectionStart, SelectionLength)).X;
            ui.DrawRect(targetId, new Vector2(selectionX, textY), new Vector2(selectionW, textRenderer.LineHeight), SelectionColor);
        }

        if (!string.IsNullOrEmpty(shownText))
            textRenderer.DrawText(ui, targetId, shownText, new Vector2(textX, textY), showPlaceholder ? PlaceholderColor : TextColor);

        if (_isComposing && _compositionText.Length > 0 && MaskChar == null)
        {
            var compositionStart = SelectionStart;
            var prefixWidth = textRenderer.Measure(displayText[..compositionStart]).X;
            var compositionWidth = textRenderer.Measure(_compositionText).X;
            ui.DrawRect(
                targetId,
                new Vector2(textX + prefixWidth, textY + textRenderer.LineHeight - 1f),
                new Vector2(System.Math.Max(1f, compositionWidth), 1f),
                TextColor);
        }

        if (_focused && IsCursorVisible())
        {
            float cursorX = textX + textRenderer.Measure(displayText[..visualCursor]).X;
            ui.DrawRect(targetId, new Vector2(cursorX, textY), new Vector2(1.5f, textRenderer.LineHeight), new Vector4(1f, 1f, 1f, 0.9f));
        }
    }

    private string DisplayText
    {
        get
        {
            if (MaskChar is not { } mask || _buffer.Length == 0)
                return _buffer.ToString();
            return new string(mask, _buffer.Length);
        }
    }

    private bool IsCursorVisible()
    {
        const long onMs = 530;
        return _blinkTimer.ElapsedMilliseconds % (onMs * 2) < onMs;
    }

    protected internal override void OnFocusChanged(bool focused)
    {
        _focused = focused;
        if (focused)
        {
            _blinkTimer.Restart();
            EnsureCursorVisible();
        }
        else
        {
            _draggingSelection = false;
            _isComposing = false;
            _compositionText = string.Empty;
        }
        FocusChanged?.Invoke(focused);
    }

    protected internal override void OnMouseMove(Vector2 position) => _lastPointerPosition = position;

    protected internal override void OnMouseDown(MouseButton button)
    {
        if (button != MouseButton.Left)
            return;

        _draggingSelection = true;
        MoveCursorTo(_lastPointerPosition.X, extendSelection: false);
    }

    protected internal override void OnMouseDrag(Vector2 position)
    {
        if (!_draggingSelection)
            return;

        _lastPointerPosition = position;
        MoveCursorTo(position.X, extendSelection: true);
    }

    protected internal override void OnMouseUp(MouseButton button)
    {
        if (button == MouseButton.Left)
            _draggingSelection = false;
    }

    protected internal override void OnTextInput(string text)
    {
        _isComposing = false;
        _compositionText = string.Empty;
        if (ReadOnly || string.IsNullOrEmpty(text))
            return;

        if (MaxLength > 0)
        {
            int available = MaxLength - (_buffer.Length - SelectionLength);
            if (available <= 0)
                return;
            if (text.Length > available)
                text = text[..available];
        }

        ReplaceSelection(text, EditKind.Insert);
    }

    protected internal override void OnTextComposition(string text, bool isComposing)
    {
        bool nextIsComposing = isComposing && !ReadOnly && MaskChar == null;
        string nextCompositionText = nextIsComposing ? text ?? string.Empty : string.Empty;
        if (_isComposing == nextIsComposing && _compositionText == nextCompositionText)
            return;

        _isComposing = nextIsComposing;
        _compositionText = nextCompositionText;
        _blinkTimer.Restart();
        EnsureCursorVisible();
    }

    protected internal override void OnKeyDown(Key key, KeyMask keysDown)
    {
        _blinkTimer.Restart();
        if (_isComposing)
            return;
        bool ctrl = keysDown.IsDown(Key.LeftControl) || keysDown.IsDown(Key.RightControl);
        bool shift = keysDown.IsDown(Key.LeftShift) || keysDown.IsDown(Key.RightShift);

        if (ctrl)
        {
            switch (key)
            {
                case Key.A:
                    SelectAll();
                    return;
                case Key.C:
                    CopySelection();
                    return;
                case Key.X:
                    CutSelection();
                    return;
                case Key.V:
                    PasteClipboard();
                    return;
                case Key.Z:
                    Undo();
                    return;
                case Key.Y:
                    Redo();
                    return;
            }
        }

        switch (key)
        {
            case Key.Enter:
                Submitted?.Invoke(Text);
                break;
            case Key.Escape:
                Cancelled?.Invoke();
                break;
            case Key.Backspace:
                if (!ReadOnly)
                {
                    if (HasSelection)
                        ReplaceSelection(string.Empty, EditKind.Delete);
                    else if (_cursor > 0)
                    {
                        if (ctrl)
                            DeleteWord(backward: true);
                        else
                            DeleteRange(_cursor - 1, 1, EditKind.Delete);
                    }
                }
                break;
            case Key.Delete:
                if (!ReadOnly)
                {
                    if (HasSelection)
                        ReplaceSelection(string.Empty, EditKind.Delete);
                    else if (_cursor < _buffer.Length)
                    {
                        if (ctrl)
                            DeleteWord(backward: false);
                        else
                            DeleteRange(_cursor, 1, EditKind.Delete);
                    }
                }
                break;
            case Key.Left:
                MoveCursor(ctrl ? PreviousWord(_cursor) : System.Math.Max(0, _cursor - 1), shift);
                break;
            case Key.Right:
                MoveCursor(ctrl ? NextWord(_cursor) : System.Math.Min(_buffer.Length, _cursor + 1), shift);
                break;
            case Key.Home:
                MoveCursor(0, shift);
                break;
            case Key.End:
                MoveCursor(_buffer.Length, shift);
                break;
        }
    }

    public void SelectAll()
    {
        _selectionAnchor = 0;
        _cursor = _buffer.Length;
        EnsureCursorVisible();
    }

    public void ClearSelection() => _selectionAnchor = _cursor;

    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        var edit = _undo.Pop();
        _redo.Push(edit);
        Restore(edit.BeforeText, edit.BeforeCursor, edit.BeforeAnchor);
        _lastEditKind = EditKind.None;
    }

    public void Redo()
    {
        if (_redo.Count == 0)
            return;

        var edit = _redo.Pop();
        _undo.Push(edit);
        Restore(edit.AfterText, edit.AfterCursor, edit.AfterAnchor);
        _lastEditKind = edit.Kind;
    }

    private void CopySelection()
    {
        if (Clipboard == null || !HasSelection)
            return;
        Clipboard.SetText(_buffer.ToString(SelectionStart, SelectionLength));
    }

    private void CutSelection()
    {
        if (ReadOnly || !HasSelection)
            return;
        CopySelection();
        ReplaceSelection(string.Empty, EditKind.Delete);
    }

    private void PasteClipboard()
    {
        if (ReadOnly || Clipboard == null)
            return;
        var text = Clipboard.GetText();
        if (!string.IsNullOrEmpty(text))
            OnTextInput(text);
    }

    private void ReplaceSelection(string replacement, EditKind kind)
    {
        int start = SelectionStart;
        int length = SelectionLength;
        string before = _buffer.ToString();
        int beforeCursor = _cursor;
        int beforeAnchor = _selectionAnchor;

        _buffer.Remove(start, length);
        _buffer.Insert(start, replacement);
        _cursor = start + replacement.Length;
        _selectionAnchor = _cursor;
        RecordEdit(before, beforeCursor, beforeAnchor, kind);
        TextChanged?.Invoke(Text);
        EnsureCursorVisible();
    }

    private void DeleteRange(int start, int length, EditKind kind)
    {
        int oldCursor = _cursor;
        int oldAnchor = _selectionAnchor;
        string before = _buffer.ToString();
        _buffer.Remove(start, length);
        _cursor = start;
        _selectionAnchor = start;
        RecordEdit(before, oldCursor, oldAnchor, kind);
        TextChanged?.Invoke(Text);
        EnsureCursorVisible();
    }

    private void DeleteWord(bool backward)
    {
        int start = backward ? PreviousWord(_cursor) : _cursor;
        int end = backward ? _cursor : NextWord(_cursor);
        if (end > start)
            DeleteRange(start, end - start, EditKind.Delete);
    }

    private void RecordEdit(string before, int beforeCursor, int beforeAnchor, EditKind kind)
    {
        var edit = new TextEdit(before, beforeCursor, beforeAnchor, Text, _cursor, _selectionAnchor, kind);
        if (kind == EditKind.Insert && _lastEditKind == EditKind.Insert && _undo.Count > 0)
        {
            var previous = _undo.Pop();
            edit = previous with
            {
                AfterText = Text,
                AfterCursor = _cursor,
                AfterAnchor = _selectionAnchor,
            };
        }
        _undo.Push(edit);
        _redo.Clear();
        _lastEditKind = kind;
    }

    private void Restore(string text, int cursor, int anchor)
    {
        _buffer.Clear();
        _buffer.Append(text);
        _cursor = System.Math.Clamp(cursor, 0, _buffer.Length);
        _selectionAnchor = System.Math.Clamp(anchor, 0, _buffer.Length);
        TextChanged?.Invoke(Text);
        EnsureCursorVisible();
    }

    private void MoveCursor(int position, bool extendSelection)
    {
        _cursor = System.Math.Clamp(position, 0, _buffer.Length);
        if (!extendSelection)
            _selectionAnchor = _cursor;
        EnsureCursorVisible();
    }

    private void MoveCursorTo(float pointerX, bool extendSelection)
    {
        var renderer = GetTextRenderer();
        if (renderer == null)
            return;

        float localX = pointerX - Bounds.X - Padding.Left + _horizontalOffset;
        string display = DisplayText;
        int index = 0;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i <= display.Length; i++)
        {
            float x = renderer.Measure(display[..i]).X;
            float distance = System.Math.Abs(localX - x);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                index = i;
            }
        }
        MoveCursor(index, extendSelection);
    }

    private int PreviousWord(int position)
    {
        int i = System.Math.Clamp(position, 0, _buffer.Length);
        while (i > 0 && char.IsWhiteSpace(_buffer[i - 1])) i--;
        while (i > 0 && !char.IsWhiteSpace(_buffer[i - 1])) i--;
        return i;
    }

    private int NextWord(int position)
    {
        int i = System.Math.Clamp(position, 0, _buffer.Length);
        while (i < _buffer.Length && !char.IsWhiteSpace(_buffer[i])) i++;
        while (i < _buffer.Length && char.IsWhiteSpace(_buffer[i])) i++;
        return i;
    }

    private void EnsureCursorVisible()
    {
        var renderer = GetTextRenderer();
        if (renderer == null || Bounds.Width <= 0f)
            return;

        float available = System.Math.Max(1f, Bounds.Width - Padding.Left - Padding.Right - 4f);
        var display = GetPreviewText(out var visualCursor);
        float cursorX = renderer.Measure(display[..visualCursor]).X;
        if (cursorX < _horizontalOffset)
            _horizontalOffset = cursorX;
        else if (cursorX > _horizontalOffset + available)
            _horizontalOffset = cursorX - available;

        float total = renderer.Measure(display).X;
        _horizontalOffset = System.Math.Clamp(_horizontalOffset, 0f, System.Math.Max(0f, total - available));
    }

    private string GetPreviewText(out int visualCursor)
    {
        var display = DisplayText;
        visualCursor = _cursor;
        if (!_isComposing || _compositionText.Length == 0 || MaskChar != null)
            return display;

        var start = SelectionStart;
        visualCursor = start + _compositionText.Length;
        return display.Remove(start, SelectionLength).Insert(start, _compositionText);
    }
}
