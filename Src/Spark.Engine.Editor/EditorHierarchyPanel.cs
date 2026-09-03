using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器层级面板：组合标题和层级树，向宿主暴露刷新与选择操作。</summary>
internal sealed class EditorHierarchyPanel : UIElement
{
    private readonly HierarchyPanel _hierarchy;
    private EditorWorldOutlinerData _outliner;
    private readonly UIStackPanel _panel;
    private readonly UILabel _title;
    private readonly UITextBox _search;
    private readonly UIButton _viewOptionsButton;
    private readonly UIButton _addFolderButton;
    private readonly UIMenuPanel _viewOptions = new() { MinWidth = 190f, MaxWidth = 240f };
    private readonly UIMenuPanel _contextMenu = new() { MinWidth = 180f, MaxWidth = 260f };
    private object? _contextTarget;

    public EditorHierarchyPanel(Spark.Engine.Worlds.World world, EditorWorldOutlinerData? outliner = null)
    {
        _outliner = outliner ?? EditorWorldOutlinerData.For(world);
        _hierarchy = new HierarchyPanel(world, _outliner);
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
        header.ColumnDefinitions.Add(UIGridDefinition.Auto());
        _title = new UILabel
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
        _addFolderButton = new UIButton
        {
            Text = "+ Folder",
            FixedSize = new UISize(72f, 22f),
            Clicked = () => CreateFolderRequested?.Invoke(),
        };
        header.AddChild(_title);
        header.AddChild(_addFolderButton);
        header.AddChild(_viewOptionsButton);
        header.SetColumn(_title, 0);
        header.SetColumn(_addFolderButton, 1);
        header.SetColumn(_viewOptionsButton, 2);
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
        _hierarchy.ItemContextRequested = ShowContextMenu;
        _hierarchy.VisibilityToggled = target => VisibilityToggled?.Invoke(target);
        _hierarchy.RenameSubmitted = (target, value) => RenameSubmitted?.Invoke(target, value) ?? true;
        _hierarchy.DeleteRequested = target => DeleteRequested?.Invoke(target);
        _hierarchy.BackgroundContextRequested = ShowBackgroundContextMenu;
        _hierarchy.ItemDroppedOnBackground = (target, position) => ItemDroppedOnBackground?.Invoke(target, position);
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

    public Action? CreateFolderRequested { get; set; }
    public Action<object>? DeleteRequested { get; set; }
    public Action<object>? VisibilityToggled { get; set; }
    public Func<object, string, bool>? RenameSubmitted { get; set; }
    public Action<EditorActorFolder>? MakeCurrentFolderRequested { get; set; }
    public Action? ClearCurrentFolderRequested { get; set; }
    public Action<EditorActorFolder>? CreateSubfolderRequested { get; set; }
    public Action<EditorActorFolder>? SelectFolderActorsRequested { get; set; }
    public Action<Spark.Engine.Actors.Actor>? FocusActorRequested { get; set; }
    public Action<Spark.Engine.Actors.Actor>? DuplicateActorRequested { get; set; }
    public Action<Spark.Engine.Actors.Actor>? DetachActorRequested { get; set; }
    public Action<Spark.Engine.Actors.Actor>? MoveActorToCurrentFolderRequested { get; set; }
    public Action<Spark.Engine.Actors.Actor>? SelectActorChildrenRequested { get; set; }
    public Action<object, System.Numerics.Vector2>? ItemDroppedOnBackground { get; set; }
    public object? ActiveTarget => _hierarchy.SelectedTarget;

    public void Refresh()
    {
        _title.Text = _outliner.CurrentFolderGuid is { } current && _outliner.FindFolder(current) is { } folder
            ? $"OUTLINER · {folder.Name}"
            : "OUTLINER";
        _hierarchy.Refresh();
    }

    public void SetWorld(Spark.Engine.Worlds.World world)
    {
        _outliner = EditorWorldOutlinerData.For(world);
        _hierarchy.SetWorld(world);
    }

    public void SelectTarget(object? target) => _hierarchy.SelectTarget(target);

    public void SelectTargets(IEnumerable<object> targets, object? primary = null)
        => _hierarchy.SelectTargets(targets, primary);

    public bool BeginRename(object target) => _hierarchy.BeginRename(target);

    public object? GetTargetAt(System.Numerics.Vector2 position) => _hierarchy.GetTargetAt(position);

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

    private void ShowContextMenu(object target, System.Numerics.Vector2 position)
    {
        _contextTarget = target;
        _contextMenu.Clear();
        _contextMenu.AddItem(new UIMenuItem("Create Folder", () => CreateFolderRequested?.Invoke()));
        if (target is EditorActorFolder folder)
        {
            _contextMenu.AddItem(new UIMenuItem("New Subfolder", () => CreateSubfolderRequested?.Invoke(folder)));
            _contextMenu.AddItem(new UIMenuItem("Make Current Folder", () => MakeCurrentFolderRequested?.Invoke(folder)));
            _contextMenu.AddItem(new UIMenuItem("Select Descendant Actors", () => SelectFolderActorsRequested?.Invoke(folder)));
        }
        else if (target is Spark.Engine.Actors.Actor actor)
        {
            _contextMenu.AddItem(new UIMenuItem("Focus Selected", () => FocusActorRequested?.Invoke(actor)) { Shortcut = "F" });
            _contextMenu.AddItem(new UIMenuItem("Duplicate", () => DuplicateActorRequested?.Invoke(actor)) { Shortcut = "Ctrl+D" });
            _contextMenu.AddItem(new UIMenuItem("Detach", () => DetachActorRequested?.Invoke(actor))
            {
                IsEnabled = actor.RootComponent?.AttachParent != null,
            });
            _contextMenu.AddItem(new UIMenuItem("Move to Current Folder", () => MoveActorToCurrentFolderRequested?.Invoke(actor)));
            _contextMenu.AddItem(new UIMenuItem("Select Children", () => SelectActorChildrenRequested?.Invoke(actor)));
        }
        _contextMenu.AddItem(new UIMenuItem("Clear Current Folder", () => ClearCurrentFolderRequested?.Invoke()));
        _contextMenu.AddSeparator();
        _contextMenu.AddItem(new UIMenuItem("Rename", () =>
        {
            if (_contextTarget != null)
                BeginRename(_contextTarget);
        }) { Shortcut = "F2", IsEnabled = target is Spark.Engine.Actors.Actor or EditorActorFolder });
        _contextMenu.AddItem(new UIMenuItem("Delete", () =>
        {
            if (_contextTarget != null)
                DeleteRequested?.Invoke(_contextTarget);
        }) { Shortcut = "Delete", IsEnabled = target is Spark.Engine.Actors.Actor or EditorActorFolder });
        _contextMenu.Canvas = FindCanvas();
        _contextMenu.Show(position);
    }

    private void ShowBackgroundContextMenu(System.Numerics.Vector2 position)
    {
        _contextTarget = null;
        _contextMenu.Clear();
        _contextMenu.AddItem(new UIMenuItem("New Folder", () => CreateFolderRequested?.Invoke()));
        _contextMenu.AddItem(new UIMenuItem("Clear Current Folder", () => ClearCurrentFolderRequested?.Invoke()));
        _contextMenu.Canvas = FindCanvas();
        _contextMenu.Show(position);
    }
}
