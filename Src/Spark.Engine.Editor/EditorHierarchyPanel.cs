using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器层级面板：组合标题和层级树，向宿主暴露刷新与选择操作。</summary>
internal sealed class EditorHierarchyPanel : UIElement
{
    private readonly HierarchyPanel _hierarchy;
    private readonly UIStackPanel _panel;

    public EditorHierarchyPanel(Spark.Engine.Worlds.World world)
    {
        _hierarchy = new HierarchyPanel(world);
        _panel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(220f, 0f),
            BackgroundColor = UITheme.Default.PanelBackground,
        };
        _panel.AddChild(new UILabel
        {
            Text = "SCENE HIERARCHY",
            TextColor = UITheme.Default.TextDimColor,
            Padding = UIEdgeInsets.HorizontalVertical(8f, 6f),
        });
        _panel.AddChild(_hierarchy.Element);
        AddChild(_panel);
    }

    public Action<object?>? SelectionChanged
    {
        get => _hierarchy.SelectionChanged;
        set => _hierarchy.SelectionChanged = value;
    }

    public Action<IReadOnlyList<object>, object?>? SelectionSetChanged
    {
        get => _hierarchy.SelectionSetChanged;
        set => _hierarchy.SelectionSetChanged = value;
    }

    public Action<object, object, System.Numerics.Vector2>? ItemDropped
    {
        get => _hierarchy.ItemDropped;
        set => _hierarchy.ItemDropped = value;
    }

    public void Refresh() => _hierarchy.Refresh();

    public void SetWorld(Spark.Engine.Worlds.World world) => _hierarchy.SetWorld(world);

    public void SelectTarget(object? target) => _hierarchy.SelectTarget(target);

    public void SelectTargets(IEnumerable<object> targets, object? primary = null)
        => _hierarchy.SelectTargets(targets, primary);

    protected override UISize OnMeasure(UISize availableSize)
    {
        _panel.Measure(availableSize);
        return _panel.DesiredSize;
    }

    protected override void OnArrange() => _panel.Arrange(ContentRect);
}
