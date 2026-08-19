using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>网格行列定义类型。</summary>
public enum UIGridUnitType
{
    /// <summary>固定逻辑像素。</summary>
    Fixed,

    /// <summary>按比例分配剩余空间（类似 WPF Star）。</summary>
    Proportional,

    /// <summary>按内容自适应（取该列/行中子元素的最大 DesiredSize）。</summary>
    Auto,
}

/// <summary>网格行列定义。</summary>
public struct UIGridDefinition
{
    public UIGridUnitType Type;
    public float Value;

    public UIGridDefinition(UIGridUnitType type, float value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>固定尺寸定义。</summary>
    public static UIGridDefinition Fixed(float pixels) => new(UIGridUnitType.Fixed, pixels);

    /// <summary>比例定义（1.0 = 一份）。</summary>
    public static UIGridDefinition Star(float weight = 1f) => new(UIGridUnitType.Proportional, weight);

    /// <summary>内容自适应定义。</summary>
    public static UIGridDefinition Auto() => new(UIGridUnitType.Auto, 0f);
}

/// <summary>
/// 网格布局容器（P6 修复版）：按 <see cref="RowDefinitions"/> 和 <see cref="ColumnDefinitions"/> 划分网格，
/// 子元素通过实例级附加属性 <see cref="SetRow"/>/<see cref="SetColumn"/>/
/// <see cref="SetRowSpan"/>/<see cref="SetColumnSpan"/> 定位到指定单元格。
/// 支持 Fixed / Proportional(Star) / Auto 三种尺寸模式。
/// <para>
/// Auto 尺寸在 Measure 阶段确定并缓存到实例，供 Arrange 直接复用（旧版 Arrange 把 Auto 置 0 导致单元格塌陷）。
/// 行列 span 在 Arrange 时合并为跨越多个 track 的联合矩形。
/// </para>
/// <para>
/// 已知限制：仅 <see cref="GetRow"/>/<see cref="GetColumn"/> 直接子元素生效；span&gt;1 的子元素不参与 Auto
/// 尺寸的计算（WPF 语义更复杂，本版简化为「span 只占位，不撑大 Auto 轨」）。
/// </para>
/// </summary>
public sealed class UIGridPanel : UIElement
{
    private readonly List<UIGridDefinition> _rowDefs = new();
    private readonly List<UIGridDefinition> _colDefs = new();

    /// <summary>背景色（alpha = 0 表示透明）。</summary>
    public Vector4 BackgroundColor { get; set; }

    /// <summary>单元格间距（同时作用于行列；影响 Star 剩余空间与 track 偏移）。</summary>
    public float CellSpacing { get; set; }

    public IList<UIGridDefinition> RowDefinitions => _rowDefs;
    public IList<UIGridDefinition> ColumnDefinitions => _colDefs;

    // 实例级附加属性（修复旧版 static 字典：元素销毁后条目永不回收 + 多 Grid 互相串数据）。
    private readonly Dictionary<UIElement, int> _rows = new();
    private readonly Dictionary<UIElement, int> _cols = new();
    private readonly Dictionary<UIElement, int> _rowSpans = new();
    private readonly Dictionary<UIElement, int> _colSpans = new();

    public void SetRow(UIElement element, int row) => _rows[element] = row;
    public int GetRow(UIElement element) => _rows.TryGetValue(element, out var r) ? r : 0;

    public void SetColumn(UIElement element, int col) => _cols[element] = col;
    public int GetColumn(UIElement element) => _cols.TryGetValue(element, out var c) ? c : 0;

    /// <summary>行跨度（默认 1）。</summary>
    public void SetRowSpan(UIElement element, int span) => _rowSpans[element] = System.Math.Max(1, span);
    public int GetRowSpan(UIElement element) => _rowSpans.TryGetValue(element, out var s) ? s : 1;

    /// <summary>列跨度（默认 1）。</summary>
    public void SetColumnSpan(UIElement element, int span) => _colSpans[element] = System.Math.Max(1, span);
    public int GetColumnSpan(UIElement element) => _colSpans.TryGetValue(element, out var s) ? s : 1;

    // Measure 阶段缓存的 Auto track 尺寸，供 Arrange 复用。Arrange 未经过 Measure 时回退为 0（Auto 塌陷）。
    private float[]? _measureRowAutoSizes;
    private float[]? _measureColAutoSizes;

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        // 测量子元素（无限约束），收集 span==1 子元素对 Auto 轨的贡献
        var rowAutoSizes = new float[System.Math.Max(1, _rowDefs.Count)];
        var colAutoSizes = new float[System.Math.Max(1, _colDefs.Count)];

        foreach (var child in Children)
        {
            if (!child.Visible) continue;
            int row = ClampIndex(GetRow(child), _rowDefs.Count);
            int col = ClampIndex(GetColumn(child), _colDefs.Count);
            int rowSpan = GetRowSpan(child);
            int colSpan = GetColumnSpan(child);

            var childAvail = new UISize(float.PositiveInfinity, float.PositiveInfinity);
            var desired = child.Measure(childAvail);

            // span==1 才参与 Auto 轨尺寸：多轨 span 的内容宽度难以归到单轨，简化跳过
            if (rowSpan == 1 && row < _rowDefs.Count && _rowDefs[row].Type == UIGridUnitType.Auto)
                rowAutoSizes[row] = System.Math.Max(rowAutoSizes[row], desired.Height);
            if (colSpan == 1 && col < _colDefs.Count && _colDefs[col].Type == UIGridUnitType.Auto)
                colAutoSizes[col] = System.Math.Max(colAutoSizes[col], desired.Width);
        }

        _measureRowAutoSizes = rowAutoSizes;
        _measureColAutoSizes = colAutoSizes;

        // 测量阶段尺寸：Auto 用内容尺寸，Star 在有限约束下取「剩余」（与 Arrange 一致），
        // 无限约束下 Star 取 0（塌陷）。这里用 availableSize 同时算宽高。
        float totalW = ComputeDesiredTrackTotal(_colDefs, colAutoSizes, availableSize.Width);
        float totalH = ComputeDesiredTrackTotal(_rowDefs, rowAutoSizes, availableSize.Height);

        totalW += Padding.Left + Padding.Right;
        totalH += Padding.Top + Padding.Bottom;

        if (FixedSize is { } fsv)
        {
            if (fsv.Width > 0f) totalW = fsv.Width;
            if (fsv.Height > 0f) totalH = fsv.Height;
        }

        return new UISize(totalW, totalH);
    }

    protected override void OnArrange()
    {
        var content = ContentRect;

        // 复用 Measure 阶段的 Auto 尺寸；若无（Arrange 未先经过 Measure），Auto 回退为 0
        var rowAutoSizes = _measureRowAutoSizes ?? new float[System.Math.Max(1, _rowDefs.Count)];
        var colAutoSizes = _measureColAutoSizes ?? new float[System.Math.Max(1, _colDefs.Count)];

        var rowSizes = ResolveTrackSizes(_rowDefs, rowAutoSizes, content.Height);
        var colSizes = ResolveTrackSizes(_colDefs, colAutoSizes, content.Width);

        // 累积偏移（每两个 track 之间一个 CellSpacing）
        var rowOffsets = ComputeOffsets(rowSizes, CellSpacing);
        var colOffsets = ComputeOffsets(colSizes, CellSpacing);

        foreach (var child in Children)
        {
            if (!child.Visible) continue;
            int row = ClampIndex(GetRow(child), rowSizes.Length);
            int col = ClampIndex(GetColumn(child), colSizes.Length);
            int rowSpan = System.Math.Max(1, GetRowSpan(child));
            int colSpan = System.Math.Max(1, GetColumnSpan(child));

            int rowEnd = System.Math.Min(rowSizes.Length, row + rowSpan);
            int colEnd = System.Math.Min(colSizes.Length, col + colSpan);

            float x = content.X + colOffsets[col];
            float y = content.Y + rowOffsets[row];
            // 联合矩形 = [colStart, colEnd) 的 track+spacing，但末段不再多算一个 spacing
            float w = (colOffsets[colEnd] - colOffsets[col]) - CellSpacing;
            float h = (rowOffsets[rowEnd] - rowOffsets[row]) - CellSpacing;
            if (w < 0f) w = 0f;
            if (h < 0f) h = 0f;

            child.Arrange(new UIRect(x, y, w, h));
        }
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }

    /// <summary>计算一轴的期望总尺寸（Fixed + Auto + Star 剩余；无限约束下 Star 取 0）。</summary>
    private float ComputeDesiredTrackTotal(List<UIGridDefinition> defs, float[] autoSizes, float available)
    {
        float fixedSum = 0f;
        float autoSum = 0f;
        float starSum = 0f;
        int n = defs.Count;
        float spacingTotal = CellSpacing * System.Math.Max(0, n - 1);

        for (int i = 0; i < n; i++)
        {
            switch (defs[i].Type)
            {
                case UIGridUnitType.Fixed:
                    fixedSum += defs[i].Value;
                    break;
                case UIGridUnitType.Auto:
                    autoSum += i < autoSizes.Length ? autoSizes[i] : 0f;
                    break;
                case UIGridUnitType.Proportional:
                    starSum += defs[i].Value;
                    break;
            }
        }

        float used = fixedSum + autoSum + spacingTotal;
        float total = used;
        if (starSum > 0f && !float.IsPositiveInfinity(available))
            total += System.Math.Max(0f, available - used); // Star 消耗剩余

        // 有限约束下不超过可用空间
        if (!float.IsPositiveInfinity(available))
            total = System.Math.Min(total, available);

        return total;
    }

    /// <summary>Arrange 阶段解析一轴所有 track 的最终尺寸：Fixed 取值、Auto 取缓存、Star 按比例分剩余。</summary>
    private float[] ResolveTrackSizes(List<UIGridDefinition> defs, float[] autoSizes, float totalAvailable)
    {
        int n = defs.Count;
        if (n == 0)
            return [];

        var sizes = new float[n];
        float spacingTotal = CellSpacing * System.Math.Max(0, n - 1);
        float used = spacingTotal;
        float starSum = 0f;

        for (int i = 0; i < n; i++)
        {
            switch (defs[i].Type)
            {
                case UIGridUnitType.Fixed:
                    sizes[i] = defs[i].Value;
                    used += sizes[i];
                    break;
                case UIGridUnitType.Auto:
                    sizes[i] = i < autoSizes.Length ? autoSizes[i] : 0f;
                    used += sizes[i];
                    break;
                case UIGridUnitType.Proportional:
                    starSum += defs[i].Value;
                    break;
            }
        }

        if (starSum > 0f)
        {
            float remaining = System.Math.Max(0f, totalAvailable - used);
            for (int i = 0; i < n; i++)
            {
                if (defs[i].Type == UIGridUnitType.Proportional)
                    sizes[i] = remaining * (defs[i].Value / starSum);
            }
        }

        return sizes;
    }

    private static float[] ComputeOffsets(float[] sizes, float spacing)
    {
        var offsets = new float[sizes.Length + 1];
        for (int i = 0; i < sizes.Length; i++)
            offsets[i + 1] = offsets[i] + sizes[i] + spacing;
        return offsets;
    }

    private static int ClampIndex(int value, int count)
        => count <= 0 ? 0 : System.Math.Clamp(value, 0, count - 1);
}
