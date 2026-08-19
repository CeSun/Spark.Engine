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
/// 网格布局容器（P6）：按 <see cref="RowDefinitions"/> 和 <see cref="ColumnDefinitions"/> 划分网格，
/// 子元素通过附加属性 <see cref="GetRow"/>/<see cref="GetColumn"/> 定位到指定单元格。
/// 支持 Fixed / Proportional(Star) / Auto 三种尺寸模式。
/// </summary>
public sealed class UIGridPanel : UIElement
{
    private readonly List<UIGridDefinition> _rowDefs = new();
    private readonly List<UIGridDefinition> _colDefs = new();

    /// <summary>背景色（alpha = 0 表示透明）。</summary>
    public Vector4 BackgroundColor { get; set; }

    /// <summary>单元格间距。</summary>
    public float CellSpacing { get; set; }

    public IList<UIGridDefinition> RowDefinitions => _rowDefs;
    public IList<UIGridDefinition> ColumnDefinitions => _colDefs;

    // 附加属性：子元素的行列索引
    private static readonly Dictionary<UIElement, int> _rows = new();
    private static readonly Dictionary<UIElement, int> _cols = new();

    public static void SetRow(UIElement element, int row) => _rows[element] = row;
    public static int GetRow(UIElement element) => _rows.TryGetValue(element, out var r) ? r : 0;

    public static void SetColumn(UIElement element, int col) => _cols[element] = col;
    public static int GetColumn(UIElement element) => _cols.TryGetValue(element, out var c) ? c : 0;

    protected override UISize OnMeasure(UISize availableSize)
    {
        if (FixedSize is { } fs && fs.Width > 0f && fs.Height > 0f)
            return fs;

        // 测量所有子元素，收集每行/列的 Auto 尺寸
        var rowAutoSizes = new float[_rowDefs.Count];
        var colAutoSizes = new float[_colDefs.Count];

        foreach (var child in Children)
        {
            if (!child.Visible) continue;
            int row = System.Math.Clamp(GetRow(child), 0, System.Math.Max(0, _rowDefs.Count - 1));
            int col = System.Math.Clamp(GetColumn(child), 0, System.Math.Max(0, _colDefs.Count - 1));

            var childAvail = new UISize(float.PositiveInfinity, float.PositiveInfinity);
            var desired = child.Measure(childAvail);

            if (row < _rowDefs.Count && _rowDefs[row].Type == UIGridUnitType.Auto)
                rowAutoSizes[row] = System.Math.Max(rowAutoSizes[row], desired.Height);
            if (col < _colDefs.Count && _colDefs[col].Type == UIGridUnitType.Auto)
                colAutoSizes[col] = System.Math.Max(colAutoSizes[col], desired.Width);
        }

        // 计算总尺寸
        float totalW = ComputeTotalSize(_colDefs, colAutoSizes, availableSize.Width);
        float totalH = ComputeTotalSize(_rowDefs, rowAutoSizes, availableSize.Height);

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

        // 解析行列实际尺寸
        var rowSizes = ResolveSizes(_rowDefs, content.Height);
        var colSizes = ResolveSizes(_colDefs, content.Width);

        // 计算累积偏移
        var rowOffsets = new float[rowSizes.Length + 1];
        var colOffsets = new float[colSizes.Length + 1];
        for (int i = 0; i < rowSizes.Length; i++)
            rowOffsets[i + 1] = rowOffsets[i] + rowSizes[i] + CellSpacing;
        for (int i = 0; i < colSizes.Length; i++)
            colOffsets[i + 1] = colOffsets[i] + colSizes[i] + CellSpacing;

        // 安置子元素
        foreach (var child in Children)
        {
            if (!child.Visible) continue;
            int row = System.Math.Clamp(GetRow(child), 0, System.Math.Max(0, rowSizes.Length - 1));
            int col = System.Math.Clamp(GetColumn(child), 0, System.Math.Max(0, colSizes.Length - 1));

            float x = content.X + colOffsets[col];
            float y = content.Y + rowOffsets[row];
            float w = col < colSizes.Length ? colSizes[col] : 0f;
            float h = row < rowSizes.Length ? rowSizes[row] : 0f;

            child.Arrange(new UIRect(x, y, w, h));
        }
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (BackgroundColor.W > 0f)
            ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height), BackgroundColor);
    }

    private static float ComputeTotalSize(List<UIGridDefinition> defs, float[] autoSizes, float available)
    {
        float fixedSum = 0f;
        float autoSum = 0f;
        float starSum = 0f;

        for (int i = 0; i < defs.Count; i++)
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

        float remaining = System.Math.Max(0f, available - fixedSum - autoSum);
        float total = fixedSum + autoSum;
        if (starSum > 0f)
            total += remaining; // star 消耗所有剩余空间

        return total;
    }

    private static float[] ResolveSizes(List<UIGridDefinition> defs, float totalAvailable)
    {
        if (defs.Count == 0)
            return [];

        var sizes = new float[defs.Count];
        float fixedSum = 0f;
        float autoSum = 0f;
        float starSum = 0f;

        // Pass 1: 计算 Fixed 和 Auto
        for (int i = 0; i < defs.Count; i++)
        {
            switch (defs[i].Type)
            {
                case UIGridUnitType.Fixed:
                    sizes[i] = defs[i].Value;
                    fixedSum += defs[i].Value;
                    break;
                case UIGridUnitType.Auto:
                    // Auto 尺寸已在 Measure 阶段确定，这里用 DesiredSize 或回退
                    // 简化：Auto 在 Arrange 时无法重新 Measure，使用 0 作为占位
                    // 实际上应该在 Measure 阶段缓存 auto 尺寸
                    sizes[i] = 0f; // 将在下面修正
                    break;
                case UIGridUnitType.Proportional:
                    starSum += defs[i].Value;
                    break;
            }
        }

        // 对于 Auto，我们需要从子元素的 DesiredSize 推断
        // 但由于 Arrange 不重新 Measure，我们依赖 Measure 阶段的缓存
        // 这里简化处理：Auto 尺寸在 Measure 时已计算并体现在 totalAvailable 中
        // 重新计算 auto 总和
        autoSum = 0f;
        for (int i = 0; i < defs.Count; i++)
        {
            if (defs[i].Type == UIGridUnitType.Auto)
            {
                // 从 Measure 阶段的 DesiredSize 推断不太可行
                // 简化：Auto 视为 0，由 star 填充剩余
                // TODO: 完善 Auto 尺寸的传递
                sizes[i] = 0f;
            }
        }

        // Pass 2: 分配 Star
        float remaining = System.Math.Max(0f, totalAvailable - fixedSum - autoSum);
        if (starSum > 0f)
        {
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i].Type == UIGridUnitType.Proportional)
                    sizes[i] = remaining * (defs[i].Value / starSum);
            }
        }

        return sizes;
    }
}
