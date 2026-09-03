using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器层级面板：组合标题和层级树，向宿主暴露刷新与选择操作。</summary>
internal sealed class EditorHierarchyPanel : UIElement
{
    private readonly HierarchyPanel _hierarchy;
    private readonly UIStackPanel _panel;
    private readonly UITextBox _search;
    private readonly UIButton _viewOptionsButton;
    private readonly UIMenuPanel _viewOptions = new() { MinWidth = 190f, MaxWidth = 240f };

    public EditorHierarchyPanel(Spark.Engine.Worlds.World world)
    {
        _hierarchy = new HierarchyPanel(world);
        _panel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 0f),
            BackgroundColor = UITheme.Default.PanelBackground,
        };
        var header = new UIGridPanel
        {
            FixedSize = new UISize(0f, 26f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 2f),
            CellSpacing = 6f,
        };
        header.RowDefinitions.Add(UIGridDefinition.Star());
        header.ColumnDefinitions.Add(UIGridDefinition.Star());
        header.ColumnDefinitions.Add(UIGridDefinition.Auto());
        var title = new UILabel
        {
            Text = "OUTLINER",
            TextColor = UITheme.Default.TextDimColor,
        };
        _viewOptionsButton = new UIButton
        {
            Text = "View",
            FixedSize = new UISize(54f, 22f),
            Clicked = ShowViewOptions,
        };
        header.AddChild(title);
        header.AddChild(_viewOptionsButton);
        header.SetColumn(title, 0);
        header.SetColumn(_viewOptionsButton, 1);
        _panel.AddChild(header);

        _search = new UITextBox
        {
            FixedSize = new UISize(0f, 26f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            PlaceholderText = "Search actors...",
            TextChanged = value =>
            {
                _hierarchy.SearchText = value;
                _hierarchy.Refresh();
            },
        };
        _panel.AddChild(_search);
        _panel.AddChild(_hierarchy.Element);
        AddChild(_panel);
    }

    public string SearchText
    {
        get => _hierarchy.SearchText;
        set
        {
            _search.Text = value ?? string.Empty;
            _hierarchy.SearchText = _search.Text;
            _hierarchy.Refresh();
        }
    }

    public bool ShowInternalActors
    {
        get => _hierarchy.ShowInternalActors;
        set
        {
            _hierarchy.ShowInternalActors = value;
            _hierarchy.Refresh();
        }
    }

    public bool ShowComponents
    {
        get => _hierarchy.ShowComponents;
        set
        {
            _hierarchy.ShowComponents = value;
            _hierarchy.Refresh();
        }
    }

    public bool OnlySelected
    {
        get => _hierarchy.OnlySelected;
        set
        {
            _hierarchy.OnlySelected = value;
            _hierarchy.Refresh();
        }
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

    private void ShowViewOptions()
    {
        _viewOptions.Clear();
        AddToggleOption("Show Internal Actors", ShowInternalActors,
            value => ShowInternalActors = value);
        AddToggleOption("Show Components (Developer)", ShowComponents,
            value => ShowComponents = value);
        AddToggleOption("Only Selected", OnlySelected,
            value => OnlySelected = value);
        _viewOptions.Canvas = FindCanvas();
        var menuX = System.Math.Max(Bounds.X, _viewOptionsButton.Bounds.Right - _viewOptions.MinWidth);
        _viewOptions.Show(new System.Numerics.Vector2(menuX, _viewOptionsButton.Bounds.Bottom));
    }

    private void AddToggleOption(string label, bool value, Action<bool> setValue)
        => _viewOptions.AddItem(new UIMenuItem($"{(value ? "[x]" : "[ ]")} {label}",
            () => setValue(!value)));
}
