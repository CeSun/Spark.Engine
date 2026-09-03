using System.Numerics;
using System.Globalization;
using System.Reflection;

namespace Spark.Engine.UI;

/// <summary>
/// 属性网格：显示和编辑对象的属性。自动反射对象属性，生成标签 + 编辑器行。
/// <para>
/// 支持的基本类型：int, float, string, bool, enum, Vector2, Vector3, Vector4, Color (Vector4)。
/// 不支持嵌套对象、数组、自定义类型。
/// </para>
/// </summary>
public sealed class UIPropertyGrid : UIElement
{
    private readonly UIScrollBox _scrollBox;
    private readonly UIStackPanel _rowsPanel;
    private object? _target;

    /// <summary>属性行高度。</summary>
    public float RowHeight { get; set; } = 24f;

    /// <summary>标签列宽度。</summary>
    public float LabelWidth { get; set; } = 120f;

    /// <summary>背景色。</summary>
    public Vector4 BackgroundColor { get; set; } = new(0.12f, 0.14f, 0.18f, 1f);

    /// <summary>标签颜色。</summary>
    public Vector4 LabelColor { get; set; } = new(0.70f, 0.72f, 0.75f, 1f);

    /// <summary>值颜色。</summary>
    public Vector4 ValueColor { get; set; } = new(0.90f, 0.92f, 0.95f, 1f);

    /// <summary>行交替颜色。</summary>
    public Vector4 RowAltColor { get; set; } = new(0.10f, 0.12f, 0.15f, 1f);

    /// <summary>属性值变化回调。</summary>
    public Action<string, object?>? PropertyChanged { get; set; }

    /// <summary>属性写入前的命令化变更请求。设置后由宿主负责实际写入。</summary>
    public Action<object, string, object?, object?>? PropertyEditRequested { get; set; }

    /// <summary>当前目标对象。</summary>
    public object? Target
    {
        get => _target;
        set
        {
            _target = value;
            RebuildRows();
        }
    }

    /// <summary>属性行列表。</summary>
    private readonly List<PropertyRow> _rows = new();

    /// <summary>当前网格实际显示的属性名（供 Inspector/自动化测试读取）。</summary>
    public IReadOnlyList<string> PropertyNames => _rows.Select(row => row.PropertyName).ToArray();

    public UIPropertyGrid()
    {
        ClipToBounds = true;

        _rowsPanel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Spacing = 0f,
        };

        _scrollBox = new UIScrollBox
        {
            ScrollDirection = UIScrollDirection.Vertical,
            Content = _rowsPanel,
        };

        AddChild(_scrollBox);
    }

    private void RebuildRows()
    {
        _rows.Clear();
        _rowsPanel.ClearChildren();

        if (_target == null)
            return;

        var type = _target.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && IsSupportedType(p.PropertyType))
            .OrderBy(p => p.Name);

        foreach (var prop in properties)
        {
            var row = CreateRow(prop);
            _rows.Add(row);
            _rowsPanel.AddChild(row);
        }
    }

    /// <summary>属性网格可编辑的类型集合：基元 + enum + 数向量（其余类型行不生成，避免只读噪声）。</summary>
    private static bool IsSupportedType(Type type) =>
        type.IsPrimitive
        || type == typeof(string)
        || type.IsEnum
        || type == typeof(Vector2)
        || type == typeof(Vector3)
        || type == typeof(Vector4)
        || type == typeof(Quaternion);

    private PropertyRow CreateRow(PropertyInfo prop)
    {
        var row = new PropertyRow
        {
            PropertyName = prop.Name,
            PropertyType = prop.PropertyType,
            RowHeight = RowHeight,
            LabelWidth = LabelWidth,
            LabelColor = LabelColor,
            ValueColor = ValueColor,
            RowAltColor = _rows.Count % 2 == 1 ? RowAltColor : new(0f, 0f, 0f, 0f),
        };

        // 读取当前值
        try
        {
            var value = prop.GetValue(_target);
            row.SetValue(value);
        }
        catch
        {
            row.SetValue(null);
        }

        // 值变化回调
        row.ValueChanged = (newValue) =>
        {
            try
            {
                if (_target != null)
                {
                    var oldValue = prop.GetValue(_target);
                    if (PropertyEditRequested != null)
                        PropertyEditRequested.Invoke(_target, prop.Name, oldValue, newValue);
                    else
                    {
                        prop.SetValue(_target, newValue);
                        PropertyChanged?.Invoke(prop.Name, newValue);
                    }
                }
            }
            catch
            {
                // 设置失败，恢复旧值
            }
        };

        return row;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        // 先测量内部滚动容器（计算内容尺寸，滚动范围依赖它）
        _scrollBox.Measure(availableSize);
        return base.OnMeasure(availableSize);
    }

    protected override void OnArrange()
    {
        _scrollBox.Arrange(ContentRect);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }

    /// <summary>
    /// 刷新显示（在外部修改了目标对象属性后调用）。
    /// </summary>
    public void Refresh()
    {
        if (_target == null)
            return;

        var type = _target.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && IsSupportedType(p.PropertyType))
            .OrderBy(p => p.Name)
            .ToList();

        for (int i = 0; i < _rows.Count && i < properties.Count; i++)
        {
            try
            {
                var value = properties[i].GetValue(_target);
                _rows[i].SetValue(value);
            }
            catch { }
        }
    }
}

/// <summary>
/// 属性网格中的一行：标签 + 值编辑器；向量和旋转按分量拆成多个 UITextBox。
/// </summary>
internal sealed class PropertyRow : UIElement
{
    public string PropertyName { get; set; } = string.Empty;

    public Type PropertyType { get; set; } = typeof(object);

    public float RowHeight { get; set; } = 24f;

    public float LabelWidth { get; set; } = 120f;

    public Vector4 LabelColor { get; set; }
    public Vector4 ValueColor { get; set; }
    public Vector4 RowAltColor { get; set; }

    /// <summary>值变化回调。</summary>
    public Action<object?>? ValueChanged { get; set; }

    private object? _currentValue;
    private bool _editing;
    private readonly List<UITextBox> _editors = new();
    private int _componentCount;

    public PropertyRow()
    {
        ConfigureEditors(1);
    }

    public void SetValue(object? value)
    {
        if (_editing)
            return; // 编辑中不覆盖用户输入（外部每帧 Refresh 不打断输入）

        _currentValue = value;
        ConfigureEditors(GetComponentCount(value));
        if (_componentCount == 1 && !_editing)
            _editors[0].Visible = false;
        SetEditorTexts(value);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        float w = FixedSize is { } fsv && fsv.Width > 0f ? fsv.Width : 0f;
        float h = FixedSize is { } fsv2 && fsv2.Height > 0f ? fsv2.Height : RowHeight;
        var valueWidth = System.Math.Max(0f, availableSize.Width - LabelWidth - 8f);
        var spacing = _componentCount > 1 ? (_componentCount - 1) * 3f : 0f;
        var editorWidth = System.Math.Max(0f, (valueWidth - spacing) / _componentCount);
        foreach (var editor in _editors)
            editor.Measure(new UISize(editorWidth, h));
        return new UISize(w, h);
    }

    protected override void OnArrange()
    {
        float valueX = Bounds.X + LabelWidth + 4f;
        float valueW = System.Math.Max(0f, Bounds.Width - LabelWidth - 8f);
        var spacing = _componentCount > 1 ? (_componentCount - 1) * 3f : 0f;
        var editorWidth = System.Math.Max(0f, (valueW - spacing) / _componentCount);
        for (var index = 0; index < _editors.Count; index++)
        {
            var editor = _editors[index];
            editor.TextColor = ValueColor;
            editor.Arrange(new UIRect(valueX + index * (editorWidth + 3f), Bounds.Y, editorWidth, Bounds.Height));
        }
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        // 背景
        if (RowAltColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), RowAltColor);

        var textRenderer = GetTextRenderer();

        // 标签
        if (textRenderer != null && !string.IsNullOrEmpty(PropertyName))
        {
            float labelY = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
            // 精确截断：长属性名不溢出标签列
            var label = textRenderer.Truncate(PropertyName, LabelWidth - 8f);
            textRenderer.DrawText(ui, targetId, label, new Vector2(Bounds.X + 4f, labelY), LabelColor);
        }

        // 标量保持原有的静态文本显示；向量/旋转始终显示多个真实输入框，符合 UE Details 的分量编辑习惯。
        if (_componentCount == 1 && !_editing && textRenderer != null)
        {
            float valueX = Bounds.X + LabelWidth + 4f;
            float valueW = System.Math.Max(0f, Bounds.Width - LabelWidth - 8f);
            float valueY = Bounds.Y + (Bounds.Height - textRenderer.LineHeight) * 0.5f;
            string displayText = textRenderer.Truncate(GetDisplayText(_currentValue), valueW);
            textRenderer.DrawText(ui, targetId, displayText, new Vector2(valueX, valueY), ValueColor);
        }

        // 分隔线
        ui.DrawRect(targetId, new Vector2(Bounds.X + LabelWidth, Bounds.Y), new Vector2(1f, Bounds.Height), new Vector4(0.20f, 0.22f, 0.25f, 1f));
    }

    protected internal override void OnMouseClick()
    {
        if (_editing)
            return;

        // 点击标签列时聚焦第一个输入框；点击分量框则由 UITextBox 自己处理焦点。
        _editing = true;
        if (_editors.Count > 0)
        {
            _editors[0].Visible = true;
            FindCanvas()?.Focus(_editors[0]);
        }
    }

    private void CommitEdit()
    {
        if (!_editing)
            return;

        _editing = false;

        if (_currentValue == null)
            return;

        try
        {
            object? newValue = ParseEditorValue();

            if (!Equals(newValue, _currentValue))
            {
                _currentValue = newValue;
                ValueChanged?.Invoke(newValue);
            }
        }
        catch
        {
            // 保留原值；下次进入编辑时会重新同步。
        }

        if (_componentCount == 1)
            _editors[0].Visible = false;
        SetEditorTexts(_currentValue);
    }

    private object? ParseEditorValue()
    {
        var componentTexts = _editors.Select(editor => editor.Text).ToArray();
        return _currentValue switch
        {
            int => int.TryParse(componentTexts[0], NumberStyles.Integer, CultureInfo.CurrentCulture, out int i) ? i : _currentValue,
            float => TryParseFloat(componentTexts[0], out float f) ? f : _currentValue,
            double => double.TryParse(componentTexts[0], NumberStyles.Float, CultureInfo.CurrentCulture, out double d) ? d : _currentValue,
            bool => bool.TryParse(componentTexts[0], out bool b) ? b : _currentValue,
            string => componentTexts[0],
            Vector2 => ParseComponents(componentTexts, 2) is { } p2 ? new Vector2(p2[0], p2[1]) : _currentValue,
            Vector3 => ParseComponents(componentTexts, 3) is { } p3 ? new Vector3(p3[0], p3[1], p3[2]) : _currentValue,
            Vector4 => ParseComponents(componentTexts, 4) is { } p4 ? new Vector4(p4[0], p4[1], p4[2], p4[3]) : _currentValue,
            Quaternion => ParseEulerComponents(componentTexts) is { } rotation ? rotation : _currentValue,
            _ => _currentValue,
        };
    }

    private void ConfigureEditors(int componentCount)
    {
        componentCount = System.Math.Clamp(componentCount, 1, 4);
        if (_componentCount == componentCount && _editors.Count == componentCount)
            return;

        foreach (var editor in _editors)
            RemoveChild(editor);
        _editors.Clear();
        _componentCount = componentCount;
        for (var index = 0; index < componentCount; index++)
        {
            var editor = new UITextBox
            {
                Visible = componentCount > 1,
                BackgroundColor = new Vector4(0.10f, 0.12f, 0.16f, 1f),
                TextColor = ValueColor,
                Padding = UIEdgeInsets.HorizontalVertical(4f, 2f),
                PlaceholderText = componentCount > 1 ? GetComponentLabel(index, componentCount) : string.Empty,
            };
            editor.Submitted = _ =>
            {
                CommitEdit();
                ReleaseEditorFocus();
            };
            editor.Cancelled = () =>
            {
                CancelEdit();
                ReleaseEditorFocus();
            };
            editor.FocusChanged = focused =>
            {
                if (focused)
                {
                    editor.Visible = true;
                    _editing = true;
                }
                else
                    CommitEdit();
            };
            _editors.Add(editor);
            AddChild(editor);
        }
    }

    private static int GetComponentCount(object? value) => value switch
    {
        Vector2 => 2,
        Vector3 => 3,
        Vector4 => 4,
        Quaternion => 3,
        _ => 1,
    };

    private void SetEditorTexts(object? value)
    {
        var texts = value switch
        {
            Quaternion rotation => GetEulerComponents(rotation),
            Vector2 vector => new[] { FormatComponent(vector.X), FormatComponent(vector.Y) },
            Vector3 vector => new[] { FormatComponent(vector.X), FormatComponent(vector.Y), FormatComponent(vector.Z) },
            Vector4 vector => new[] { FormatComponent(vector.X), FormatComponent(vector.Y), FormatComponent(vector.Z), FormatComponent(vector.W) },
            _ => new[] { value?.ToString() ?? string.Empty },
        };
        for (var index = 0; index < _editors.Count; index++)
            _editors[index].Text = index < texts.Length ? texts[index] : string.Empty;
    }

    private static string[] GetEulerComponents(Quaternion rotation)
    {
        var formatted = FormatEuler(rotation);
        return ParseParts(formatted, 3)?.Select(FormatComponent).ToArray()
            ?? new[] { "0", "0", "0" };
    }

    private static string FormatComponent(float value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string GetComponentLabel(int index, int count) => count switch
    {
        2 => index == 0 ? "X" : "Y",
        3 => index switch { 0 => "X", 1 => "Y", _ => "Z" },
        _ => index switch { 0 => "X", 1 => "Y", 2 => "Z", _ => "W" },
    };

    private static float[]? ParseComponents(IReadOnlyList<string> texts, int expectedCount)
    {
        if (texts.Count != expectedCount)
            return null;
        var values = new float[expectedCount];
        for (var index = 0; index < expectedCount; index++)
        {
            if (!TryParseFloat(texts[index], out values[index]))
                return null;
        }
        return values;
    }

    private static Quaternion? ParseEulerComponents(IReadOnlyList<string> texts)
    {
        var values = ParseComponents(texts, 3);
        if (values is not { Length: 3 })
            return null;
        const float degreesToRadians = MathF.PI / 180f;
        return Quaternion.CreateFromYawPitchRoll(
            values[1] * degreesToRadians,
            values[0] * degreesToRadians,
            values[2] * degreesToRadians);
    }

    /// <summary>解析向量文本（兼容 "&lt;1; 2; 3&gt;" / "1,2,3" / "1 2 3"）；分量数不符或解析失败返回 null。</summary>
    private static float[]? ParseParts(string text, int expectedCount)
    {
        var parts = text.Split(new[] { '<', '>', ';', ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != expectedCount)
            return null;

        var values = new float[expectedCount];
        for (int i = 0; i < expectedCount; i++)
        {
            if (!TryParseFloat(parts[i], out values[i]))
                return null;
        }
        return values;
    }

    private static bool TryParseFloat(string text, out float value)
    {
        // 角度输入允许使用 UE/建模工具常见的 "45°" 或 "45 deg" 写法；
        // 普通浮点输入仍遵循当前区域设置，并回退到不变文化的小数点格式。
        var normalized = text.Trim();
        if (normalized.EndsWith("°", StringComparison.Ordinal))
            normalized = normalized[..^1].TrimEnd();
        else if (normalized.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^3].TrimEnd();

        if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            return true;
        return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void CancelEdit()
    {
        if (!_editing)
            return;

        _editing = false;
        if (_componentCount == 1)
            _editors[0].Visible = false;
        SetEditorTexts(_currentValue);
    }

    private void ReleaseEditorFocus()
    {
        var canvas = FindCanvas();
        if (canvas?.FocusedElement is UITextBox focused && _editors.Contains(focused))
            canvas.ClearFocus();
    }

    private static string GetDisplayText(object? value) => value switch
    {
        Quaternion rotation => FormatEuler(rotation),
        Vector2 vector => FormatVector(vector.X, vector.Y),
        Vector3 vector => FormatVector(vector.X, vector.Y, vector.Z),
        Vector4 vector => FormatVector(vector.X, vector.Y, vector.Z, vector.W),
        _ => value?.ToString() ?? "null",
    };

    private static string FormatEuler(Quaternion rotation)
    {
        if (rotation.LengthSquared() < 0.000001f)
            rotation = Quaternion.Identity;
        else
            rotation = Quaternion.Normalize(rotation);

        // System.Numerics/MonoGame 的 CreateFromYawPitchRoll 组合顺序为 Yaw(Y) →
        // Pitch(X) → Roll(Z)，对应的逆变换如下（不要套用另一种常见的 XYZ 顺序公式）。
        var sinPitch = 2f * (rotation.W * rotation.X - rotation.Y * rotation.Z);
        var pitch = MathF.Abs(sinPitch) >= 1f
            ? MathF.CopySign(MathF.PI / 2f, sinPitch)
            : MathF.Asin(sinPitch);
        var yaw = MathF.Atan2(
            2f * (rotation.W * rotation.Y + rotation.X * rotation.Z),
            1f - 2f * (rotation.Y * rotation.Y + rotation.Z * rotation.Z));
        var roll = MathF.Atan2(
            2f * (rotation.W * rotation.Z + rotation.X * rotation.Y),
            1f - 2f * (rotation.X * rotation.X + rotation.Z * rotation.Z));
        const float radiansToDegrees = 180f / MathF.PI;
        return FormatVector(pitch * radiansToDegrees, yaw * radiansToDegrees, roll * radiansToDegrees);
    }

    private static string FormatVector(params float[] values)
        => $"<{string.Join(", ", values.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture)))}>";
}
