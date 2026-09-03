using Spark.Engine.UI;
using Spark.Engine.Input;
using System.Numerics;

namespace Spark.Engine.Editor;

/// <summary>编辑器层级面板：组合标题和层级树，向宿主暴露刷新与选择操作。</summary>
internal sealed class EditorHierarchyPanel : UIElement
{
    private readonly HierarchyPanel _hierarchy;
    private EditorWorldOutlinerData _outliner;
    private readonly EditorOutlinerViewState _viewState;
    private readonly EditorOutlinerExtensionRegistry _extensions;
    private readonly EditorOutlinerViewStateStore? _viewStateStore;
    private readonly UIStackPanel _panel;
    private readonly UILabel _title;
    private readonly UITextBox _search;
    private readonly UIButton _viewOptionsButton;
    private readonly UIButton _filterButton;
    private readonly UIButton _addFolderButton;
    private readonly EditorOutlinerColumnHeader _columnHeader;
    private readonly UIMenuPanel _viewOptions = new() { MinWidth = 190f, MaxWidth = 240f };
    private readonly UIMenuPanel _filterMenu = new() { MinWidth = 210f, MaxWidth = 300f };
    private readonly UIMenuPanel _contextMenu = new() { MinWidth = 180f, MaxWidth = 260f };
    private object? _contextTarget;

    public EditorHierarchyPanel(Spark.Engine.Worlds.World world, EditorWorldOutlinerData? outliner = null,
        EditorOutlinerViewState? viewState = null, EditorOutlinerViewStateStore? viewStateStore = null,
        EditorOutlinerExtensionRegistry? extensions = null)
    {
        _outliner = outliner ?? EditorWorldOutlinerData.For(world);
        _extensions = extensions ?? new EditorOutlinerExtensionRegistry();
        _viewStateStore = viewStateStore;
        _viewState = viewState ?? viewStateStore?.Load() ?? new EditorOutlinerViewState();
        _hierarchy = new HierarchyPanel(world, _outliner, _viewState, _extensions);
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
        _filterButton = new UIButton
        {
            Text = "Filter",
            FixedSize = new UISize(58f, 22f),
            Clicked = ShowFilterMenu,
        };
        _addFolderButton = new UIButton
        {
            Text = "+ Folder",
            FixedSize = new UISize(72f, 22f),
            Clicked = () => CreateFolderRequested?.Invoke(),
        };
        header.AddChild(_title);
        header.AddChild(_addFolderButton);
        header.AddChild(_filterButton);
        header.AddChild(_viewOptionsButton);
        header.SetColumn(_title, 0);
        header.SetColumn(_addFolderButton, 1);
        header.SetColumn(_filterButton, 2);
        header.SetColumn(_viewOptionsButton, 3);
        _panel.AddChild(header);

        _search = new UITextBox
        {
            FixedSize = new UISize(0f, 26f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            PlaceholderText = "Search actors...",
            Text = _viewState.SearchText,
            TextChanged = value =>
            {
                _hierarchy.SearchText = value;
                _hierarchy.Refresh();
                SaveViewState();
            },
        };
        _panel.AddChild(_search);
        _columnHeader = new EditorOutlinerColumnHeader(_viewState, _extensions,
            column =>
            {
                _hierarchy.SortBy(column);
                _hierarchy.Refresh();
                SaveViewState();
            },
            () =>
            {
                _hierarchy.InvalidateView();
                _hierarchy.Refresh();
                SaveViewState();
            },
            ShowViewOptionsAt);
        _panel.AddChild(_columnHeader);
        _panel.AddChild(_hierarchy.Element);
        AddChild(_panel);
        _hierarchy.ItemContextRequested = ShowContextMenu;
        _hierarchy.VisibilityToggled = target => VisibilityToggled?.Invoke(target);
        _hierarchy.RenameSubmitted = (target, value) => RenameSubmitted?.Invoke(target, value) ?? true;
        _hierarchy.DeleteRequested = target => DeleteRequested?.Invoke(target);
        _hierarchy.BackgroundContextRequested = ShowBackgroundContextMenu;
        _hierarchy.ItemDroppedOnBackground = (target, position) => ItemDroppedOnBackground?.Invoke(target, position);
        _hierarchy.ViewStateChanged = SaveViewState;
        UpdateFilterButton();
    }

    public string SearchText
    {
        get => _hierarchy.SearchText;
        set
        {
            _search.Text = value ?? string.Empty;
            _hierarchy.SearchText = _search.Text;
            _hierarchy.Refresh();
            SaveViewState();
        }
    }

    public bool ShowInternalActors
    {
        get => _hierarchy.ShowInternalActors;
        set
        {
            _hierarchy.ShowInternalActors = value;
            _hierarchy.Refresh();
            SaveViewState();
            UpdateFilterButton();
        }
    }

    public bool ShowComponents
    {
        get => _hierarchy.ShowComponents;
        set
        {
            _hierarchy.ShowComponents = value;
            _hierarchy.Refresh();
            SaveViewState();
        }
    }

    public bool HideTemporarilyHidden
    {
        get => _hierarchy.HideTemporarilyHidden;
        set { _hierarchy.HideTemporarilyHidden = value; _hierarchy.Refresh(); SaveViewState(); UpdateFilterButton(); }
    }

    public bool AlwaysFrameSelection
    {
        get => _hierarchy.AlwaysFrameSelection;
        set { _hierarchy.AlwaysFrameSelection = value; SaveViewState(); }
    }

    public bool OnlySelected
    {
        get => _hierarchy.OnlySelected;
        set
        {
            _hierarchy.OnlySelected = value;
            _hierarchy.Refresh();
            SaveViewState();
            UpdateFilterButton();
        }
    }

    public EditorOutlinerWorldSource WorldSource
    {
        get => _viewState.WorldSource;
        set
        {
            if (_viewState.WorldSource == value)
                return;
            _viewState.WorldSource = value;
            SaveViewState();
            WorldSourceChanged?.Invoke(value);
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
    public Action<EditorOutlinerWorldSource>? WorldSourceChanged { get; set; }
    public Action? CreateOutlinerRequested { get; set; }
    public Action? CloseOutlinerRequested { get; set; }
    public object? ActiveTarget => _hierarchy.SelectedTarget;
    public Spark.Engine.Worlds.World DisplayedWorld => _hierarchy.World;
    public bool IsReadOnly => _hierarchy.IsReadOnly;
    public bool IsRuntimeView => _hierarchy.IsRuntimeView;

    public void Refresh()
    {
        _title.Text = IsRuntimeView
            ? "OUTLINER · PLAY (READ ONLY)"
            : IsReadOnly
                ? "OUTLINER · EDITOR WORLD (PLAY LOCKED)"
            : _outliner.CurrentFolderGuid is { } current && _outliner.FindFolder(current) is { } folder
                ? $"OUTLINER · {folder.Name}"
                : "OUTLINER";
        _hierarchy.Refresh();
        UpdateFilterButton();
    }

    public void SetWorld(Spark.Engine.Worlds.World world, bool isReadOnly = false, bool isRuntimeView = false)
    {
        _outliner = EditorWorldOutlinerData.For(world);
        _hierarchy.SetWorld(world, isReadOnly, isRuntimeView);
        _addFolderButton.Visible = !isReadOnly;
        Refresh();
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
        => ShowViewOptionsAt(new System.Numerics.Vector2(
            System.Math.Max(Bounds.X, _viewOptionsButton.Bounds.Right - _viewOptions.MinWidth),
            _viewOptionsButton.Bounds.Bottom));

    private void ShowViewOptionsAt(System.Numerics.Vector2 position)
    {
        _viewOptions.Clear();
        _viewOptions.AddItem(new UIMenuItem($"{(WorldSource == EditorOutlinerWorldSource.ActiveWorld ? "[x]" : "[ ]")} Active World",
            () => WorldSource = EditorOutlinerWorldSource.ActiveWorld));
        _viewOptions.AddItem(new UIMenuItem($"{(WorldSource == EditorOutlinerWorldSource.EditorWorld ? "[x]" : "[ ]")} Editor World",
            () => WorldSource = EditorOutlinerWorldSource.EditorWorld));
        _viewOptions.AddSeparator();
        _viewOptions.AddItem(new UIMenuItem("New Outliner", () => CreateOutlinerRequested?.Invoke())
        {
            IsEnabled = CreateOutlinerRequested != null,
        });
        if (CloseOutlinerRequested != null)
            _viewOptions.AddItem(new UIMenuItem("Close This Outliner", () => CloseOutlinerRequested?.Invoke()));
        _viewOptions.AddSeparator();
        AddToggleOption("Show Components (Developer)", ShowComponents,
            value => ShowComponents = value);
        _viewOptions.AddSeparator();
        foreach (var column in _extensions.Columns.Where(column => column.Id != EditorOutlinerColumnIds.Label))
        {
            var captured = column;
            AddToggleOption($"{captured.DisplayName} Column", _viewState.IsColumnVisible(captured), value =>
            {
                _viewState.SetColumnVisible(captured, value);
                _hierarchy.InvalidateView();
                _hierarchy.Refresh();
                SaveViewState();
            });
        }
        _viewOptions.AddSeparator();
        AddToggleOption("Always Frame Selection", AlwaysFrameSelection,
            value => AlwaysFrameSelection = value);
        _viewOptions.Canvas = FindCanvas();
        _viewOptions.Show(position);
    }

    private void AddToggleOption(string label, bool value, Action<bool> setValue)
        => _viewOptions.AddItem(new UIMenuItem($"{(value ? "[x]" : "[ ]")} {label}",
            () => setValue(!value)));

    private void ShowFilterMenu()
    {
        _filterMenu.Clear();
        AddFilterToggle("Only Selected", OnlySelected, value => OnlySelected = value);
        AddFilterToggle("Hide Temporarily Hidden", HideTemporarilyHidden, value => HideTemporarilyHidden = value);
        AddFilterToggle("Show Internal Actors", ShowInternalActors, value => ShowInternalActors = value);
        foreach (var filter in _extensions.Filters)
        {
            var captured = filter;
            AddFilterToggle(captured.DisplayName, _hierarchy.IsExtensionFilterEnabled(captured.Id), _ =>
            {
                _hierarchy.ToggleExtensionFilter(captured.Id);
                _hierarchy.Refresh();
                SaveViewState();
                UpdateFilterButton();
            });
        }
        _filterMenu.AddSeparator();
        _filterMenu.AddItem(new UIMenuItem("Clear Filters", ClearFilters)
        {
            IsEnabled = GetActiveFilterCount() != 0,
        });
        _filterMenu.AddItem(new UIMenuItem("All Actor Types", () =>
        {
            _hierarchy.ClearActorTypeFilters();
            _hierarchy.Refresh();
            SaveViewState();
            UpdateFilterButton();
        }) { IsEnabled = _hierarchy.ActorTypeFilters.Count != 0 });
        foreach (var actorType in _hierarchy.AvailableActorTypes)
        {
            var captured = actorType;
            var selected = _hierarchy.ActorTypeFilters.Contains(captured);
            _filterMenu.AddItem(new UIMenuItem($"{(selected ? "[x]" : "[ ]")} {captured}", () =>
            {
                _hierarchy.ToggleActorTypeFilter(captured);
                _hierarchy.Refresh();
                SaveViewState();
                UpdateFilterButton();
            }));
        }
        _filterMenu.AddSeparator();
        _filterMenu.AddItem(new UIMenuItem("Save Current Filter", SaveCurrentFilter)
        {
            IsEnabled = SearchText.Length != 0 || _hierarchy.ActorTypeFilters.Count != 0 ||
                _viewState.EnabledExtensionFilters.Count != 0,
        });
        foreach (var filter in _viewState.CustomFilters)
        {
            var captured = filter;
            _filterMenu.AddItem(new UIMenuItem($"Apply: {captured.Name}", () => ApplyCustomFilter(captured)));
        }
        if (_viewState.CustomFilters.Count != 0)
            _filterMenu.AddItem(new UIMenuItem("Delete Saved Filters", DeleteSavedFilters));
        _filterMenu.Canvas = FindCanvas();
        var menuX = System.Math.Max(Bounds.X, _filterButton.Bounds.Right - _filterMenu.MinWidth);
        _filterMenu.Show(new System.Numerics.Vector2(menuX, _filterButton.Bounds.Bottom));
    }

    private void AddFilterToggle(string label, bool value, Action<bool> setValue)
        => _filterMenu.AddItem(new UIMenuItem($"{(value ? "[x]" : "[ ]")} {label}", () => setValue(!value)));

    private void SaveCurrentFilter()
    {
        var index = _viewState.CustomFilters.Count + 1;
        var name = SearchText.Length == 0 ? $"Actor Types {index}" : SearchText;
        _viewState.CustomFilters.Add(new EditorOutlinerCustomFilter(
            name, SearchText, _hierarchy.ActorTypeFilters.OrderBy(value => value).ToList())
        {
            ExtensionFilterIds = _viewState.EnabledExtensionFilters.OrderBy(value => value).ToList(),
        });
        SaveViewState();
    }

    private void ApplyCustomFilter(EditorOutlinerCustomFilter filter)
    {
        _viewState.ActorTypes.Clear();
        foreach (var actorType in filter.ActorTypes)
            _viewState.ActorTypes.Add(actorType);
        _viewState.EnabledExtensionFilters.Clear();
        foreach (var filterId in filter.ExtensionFilterIds)
            _viewState.EnabledExtensionFilters.Add(filterId);
        SearchText = filter.Query;
        _hierarchy.InvalidateView();
        _hierarchy.Refresh();
        SaveViewState();
        UpdateFilterButton();
    }

    private void ClearFilters()
    {
        OnlySelected = false;
        HideTemporarilyHidden = false;
        ShowInternalActors = false;
        _hierarchy.ClearActorTypeFilters();
        _hierarchy.ClearExtensionFilters();
        _hierarchy.Refresh();
        SaveViewState();
        UpdateFilterButton();
    }

    private void DeleteSavedFilters()
    {
        _viewState.CustomFilters.Clear();
        SaveViewState();
    }

    private int GetActiveFilterCount()
        => (OnlySelected ? 1 : 0) + (HideTemporarilyHidden ? 1 : 0) +
           (ShowInternalActors ? 1 : 0) + _hierarchy.ActorTypeFilters.Count +
           _viewState.EnabledExtensionFilters.Count;

    private void UpdateFilterButton()
    {
        var count = GetActiveFilterCount();
        _filterButton.Text = count == 0 ? "Filter" : $"Filter ({count})";
        _filterButton.FixedSize = new UISize(count == 0 ? 58f : 72f, 22f);
    }

    private void SaveViewState()
    {
        try { _viewStateStore?.Save(_viewState); }
        catch (IOException) { /* UI state persistence must not interrupt editing. */ }
        catch (UnauthorizedAccessException) { /* Read-only profile: keep in-memory state. */ }
    }

    private void ShowContextMenu(object target, System.Numerics.Vector2 position)
    {
        _contextTarget = target;
        _contextMenu.Clear();
        if (IsReadOnly)
            _contextMenu.AddItem(new UIMenuItem("Play World is read-only", () => { }) { IsEnabled = false });
        else
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
            if (!IsReadOnly)
            {
                _contextMenu.AddItem(new UIMenuItem("Duplicate", () => DuplicateActorRequested?.Invoke(actor)) { Shortcut = "Ctrl+D" });
                _contextMenu.AddItem(new UIMenuItem("Detach", () => DetachActorRequested?.Invoke(actor))
                {
                    IsEnabled = actor.RootComponent?.AttachParent != null,
                });
                _contextMenu.AddItem(new UIMenuItem("Move to Current Folder", () => MoveActorToCurrentFolderRequested?.Invoke(actor)));
                _contextMenu.AddItem(new UIMenuItem("Select Children", () => SelectActorChildrenRequested?.Invoke(actor)));
            }
        }
        if (!IsReadOnly)
            _contextMenu.AddItem(new UIMenuItem("Clear Current Folder", () => ClearCurrentFolderRequested?.Invoke()));
        AddExtensionContextActions(target);
        _contextMenu.AddSeparator();
        _contextMenu.AddItem(new UIMenuItem("Rename", () =>
        {
            if (_contextTarget != null)
                BeginRename(_contextTarget);
        }) { Shortcut = "F2", IsEnabled = !IsReadOnly && target is Spark.Engine.Actors.Actor or EditorActorFolder });
        _contextMenu.AddItem(new UIMenuItem("Delete", () =>
        {
            if (_contextTarget != null)
                DeleteRequested?.Invoke(_contextTarget);
        }) { Shortcut = "Delete", IsEnabled = !IsReadOnly && target is Spark.Engine.Actors.Actor or EditorActorFolder });
        _contextMenu.Canvas = FindCanvas();
        _contextMenu.Show(position);
    }

    private void AddExtensionContextActions(object? target)
    {
        if (_extensions.ContextActions.Count == 0)
            return;
        _contextMenu.AddSeparator();
        var context = new EditorOutlinerContext(target, _hierarchy.SelectedTargets, DisplayedWorld, IsReadOnly);
        foreach (var action in _extensions.ContextActions)
        {
            var captured = action;
            var enabled = !(IsReadOnly && captured.MutatesWorld);
            try { enabled &= captured.CanExecute(context); }
            catch { enabled = false; }
            _contextMenu.AddItem(new UIMenuItem(captured.Label, () =>
            {
                try { captured.Execute(context); }
                catch { /* 扩展动作不能中断编辑器输入循环。 */ }
            })
            {
                IsEnabled = enabled,
            });
        }
    }

    private void ShowBackgroundContextMenu(System.Numerics.Vector2 position)
    {
        _contextTarget = null;
        _contextMenu.Clear();
        if (IsReadOnly)
            _contextMenu.AddItem(new UIMenuItem("Play World is read-only", () => { }) { IsEnabled = false });
        else
        {
            _contextMenu.AddItem(new UIMenuItem("New Folder", () => CreateFolderRequested?.Invoke()));
            _contextMenu.AddItem(new UIMenuItem("Clear Current Folder", () => ClearCurrentFolderRequested?.Invoke()));
        }
        AddExtensionContextActions(null);
        _contextMenu.Canvas = FindCanvas();
        _contextMenu.Show(position);
    }
}

/// <summary>Outliner 表头：点击排序、拖动信息列左边界调整宽度、右键打开列菜单。</summary>
internal sealed class EditorOutlinerColumnHeader : UIElement
{
    private readonly EditorOutlinerViewState _state;
    private readonly EditorOutlinerExtensionRegistry _extensions;
    private readonly Action<string> _sort;
    private readonly Action _columnsChanged;
    private readonly Action<Vector2> _contextRequested;
    private Vector2 _pointer;
    private EditorOutlinerColumnDescriptor? _dragColumn;
    private float _dragStartX;
    private float _dragStartWidth;
    private bool _dragged;

    public EditorOutlinerColumnHeader(EditorOutlinerViewState state,
        EditorOutlinerExtensionRegistry extensions, Action<string> sort,
        Action columnsChanged, Action<Vector2> contextRequested)
    {
        _state = state;
        _extensions = extensions;
        _sort = sort;
        _columnsChanged = columnsChanged;
        _contextRequested = contextRequested;
        FixedSize = new UISize(0f, 23f);
        ClipToBounds = true;
    }

    protected override UISize OnMeasure(UISize availableSize)
        => new(FixedSize?.Width > 0f ? FixedSize.Value.Width : availableSize.Width, 23f);

    protected override void OnPaint(UIManager ui, int targetId)
    {
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height),
            new Vector4(0.105f, 0.115f, 0.13f, 1f));
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Bottom - 1f), new Vector2(Bounds.Width, 1f),
            new Vector4(0.28f, 0.3f, 0.33f, 1f));

        var columns = GetVisibleColumns();
        var secondaryWidth = columns.Sum(column => column.Width);
        var labelRight = Bounds.Right - secondaryWidth;
        DrawHeaderText(ui, targetId, EditorOutlinerColumnIds.Label, "Label", Bounds.X + 28f,
            System.Math.Max(0f, labelRight - Bounds.X - 32f));
        ui.DrawRect(targetId, new Vector2(Bounds.X + 24f, Bounds.Y), new Vector2(1f, Bounds.Height),
            new Vector4(0.28f, 0.3f, 0.33f, 0.8f));

        var x = labelRight;
        foreach (var column in columns)
        {
            ui.DrawRect(targetId, new Vector2(x, Bounds.Y), new Vector2(1f, Bounds.Height),
                new Vector4(0.28f, 0.3f, 0.33f, 0.8f));
            DrawHeaderText(ui, targetId, column.Descriptor.Id, column.Descriptor.DisplayName,
                x + 5f, column.Width - 9f);
            x += column.Width;
        }
    }

    private void DrawHeaderText(UIManager ui, int targetId, string columnId,
        string title, float x, float width)
    {
        if (width <= 0f)
            return;
        var suffix = string.Equals(_state.SortColumnId, columnId, StringComparison.OrdinalIgnoreCase)
            ? (_state.SortAscending ? " ^" : " v") : string.Empty;
        var text = ui.Text.Truncate(title + suffix, width);
        var y = Bounds.Y + (Bounds.Height - ui.Text.LineHeight) * 0.5f;
        ui.Text.DrawText(ui, targetId, text, new Vector2(x, y), UITheme.Default.TextDimColor);
    }

    protected override void OnMouseMove(Vector2 position) => _pointer = position;

    protected override void OnMouseDown(MouseButton button)
    {
        if (button != MouseButton.Left)
            return;
        _dragged = false;
        _dragColumn = FindDivider(_pointer);
        if (_dragColumn is { } column)
        {
            _dragStartX = _pointer.X;
            _dragStartWidth = _state.GetColumnWidth(column);
        }
    }

    protected override void OnMouseDrag(Vector2 position)
    {
        _pointer = position;
        if (_dragColumn is not { } column)
            return;
        if (System.Math.Abs(position.X - _dragStartX) >= 1f)
            _dragged = true;
        _state.SetColumnWidth(column,
            System.Math.Clamp(_dragStartWidth - (position.X - _dragStartX), 48f, 320f));
        _columnsChanged();
    }

    protected override void OnMouseUp(MouseButton button, Vector2 position, KeyMask keysDown)
    {
        _pointer = position;
        if (button == MouseButton.Right)
            _contextRequested(position);
        if (button == MouseButton.Left)
            _dragColumn = null;
    }

    protected override void OnMouseClick()
    {
        if (_dragged)
        {
            _dragged = false;
            return;
        }
        if (GetColumnAt(_pointer) is { } column)
            _sort(column.Id);
    }

    private EditorOutlinerColumnDescriptor? GetColumnAt(Vector2 point)
    {
        if (point.X < Bounds.X + 24f)
            return null;
        var columns = GetVisibleColumns();
        var x = Bounds.Right - columns.Sum(column => column.Width);
        if (point.X < x)
            return _extensions.FindColumn(EditorOutlinerColumnIds.Label);
        foreach (var column in columns)
        {
            if (point.X < x + column.Width)
                return column.Descriptor;
            x += column.Width;
        }
        return null;
    }

    private EditorOutlinerColumnDescriptor? FindDivider(Vector2 point)
    {
        var columns = GetVisibleColumns();
        var x = Bounds.Right - columns.Sum(column => column.Width);
        foreach (var column in columns)
        {
            if (System.Math.Abs(point.X - x) <= 4f)
                return column.Descriptor;
            x += column.Width;
        }
        return null;
    }

    private IReadOnlyList<(EditorOutlinerColumnDescriptor Descriptor, float Width)> GetVisibleColumns()
        => _extensions.Columns
            .Where(column => column.Id != EditorOutlinerColumnIds.Label && _state.IsColumnVisible(column))
            .Select(column => (column, _state.GetColumnWidth(column)))
            .ToArray();
}
