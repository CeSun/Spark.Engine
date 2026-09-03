using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器底部内容浏览器：目录过滤、资源搜索、类型过滤和资源诊断详情。</summary>
internal sealed class EditorContentBrowserPanel : UIElement
{
    private readonly UIStackPanel _root;
    private readonly EditorContentBrowserModel _model;
    private readonly UITreeView _folders;
    private readonly UIComboBox _typeFilter;
    private readonly UITextBox _search;
    private readonly UIListView _assets;
    private readonly UILabel _details;
    private readonly UILabel _count;
    private readonly UIButton _sceneReferencesButton;
    private readonly Dictionary<string, UITreeViewItem> _folderItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<UIListItem, EditorContentBrowserEntry> _assetItems = new();
    private readonly Dictionary<UIListItem, string> _childFolderItems = new();
    private Guid? _selectedGuid;
    private bool _suppressFilterEvents;
    private bool _suppressFolderEvents;

    public EditorContentBrowserPanel(IAssetRegistry registry)
    {
        _model = new EditorContentBrowserModel(registry);
        var theme = UITheme.Default;
        _root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 220f),
            BackgroundColor = theme.PanelBackground,
        };

        var header = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 30f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            Spacing = 6f,
            BackgroundColor = theme.StatusBarBackground,
        };
        header.AddChild(new UILabel { Text = "CONTENT BROWSER", TextColor = theme.TextDimColor });
        _search = new UITextBox
        {
            FixedSize = new UISize(220f, 24f),
            PlaceholderText = "Search assets...",
            TextChanged = _ => Rebuild(),
        };
        header.AddChild(_search);
        _typeFilter = new UIComboBox { FixedSize = new UISize(150f, 24f) };
        _typeFilter.SelectedItemChanged = selected =>
        {
            if (_suppressFilterEvents)
                return;
            _model.SelectedType = selected ?? EditorContentBrowserModel.AllTypes;
            Rebuild();
        };
        header.AddChild(_typeFilter);
        var refresh = new UIButton { Text = "Refresh", FixedSize = new UISize(72f, 24f), Clicked = Refresh };
        header.AddChild(refresh);
        _sceneReferencesButton = new UIButton
        {
            Text = "Scene refs: Off",
            FixedSize = new UISize(106f, 24f),
            Clicked = ToggleSceneReferences,
        };
        header.AddChild(_sceneReferencesButton);
        _count = new UILabel { TextColor = theme.TextDimColor };
        header.AddChild(_count);
        _root.AddChild(header);

        var body = new UIStackPanel { Orientation = UIOrientation.Horizontal, FixedSize = new UISize(0f, 0f) };
        _folders = new UITreeView
        {
            FixedSize = new UISize(190f, 0f),
            BackgroundColor = theme.WindowBackground,
        };
        _folders.SelectionChanged = item =>
        {
            if (_suppressFolderEvents)
                return;
            _model.SelectedDirectory = item?.Text == "All Assets" ? EditorContentBrowserModel.AllDirectories :
                FindFolderPath(item);
            _model.Refresh();
            RebuildAssets();
        };
        body.AddChild(_folders);

        var assetColumn = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 0f),
            Padding = UIEdgeInsets.HorizontalVertical(6f, 4f),
        };
        _assets = new UIListView
        {
            FixedSize = new UISize(0f, 0f),
            BackgroundColor = theme.WindowBackground,
        };
        _assets.SelectionChanged = item =>
        {
            _selectedGuid = item != null && _assetItems.TryGetValue(item, out var entry) ? entry.Record.AssetGuid : null;
            UpdateDetails(item != null && _assetItems.TryGetValue(item, out var selected) ? selected : null);
        };
        _assets.ItemActivated = item =>
        {
            if (_childFolderItems.TryGetValue(item, out var folder))
            {
                _model.SelectedDirectory = folder;
                _selectedGuid = null;
                Rebuild();
            }
            else if (_assetItems.TryGetValue(item, out var entry))
                AssetActivated?.Invoke(entry.Record);
        };
        assetColumn.AddChild(_assets);
        _details = new UILabel { Text = "Select an asset to inspect it.", TextColor = theme.TextDimColor, Padding = UIEdgeInsets.HorizontalVertical(4f, 4f) };
        assetColumn.AddChild(_details);
        body.AddChild(assetColumn);
        _root.AddChild(body);
        AddChild(_root);

        Rebuild();
    }

    /// <summary>双击或回车激活资源；宿主可据此接入 StaticMesh 创建/拖放。</summary>
    public event Action<AssetRecord>? AssetActivated;
    public EditorContentBrowserModel Model => _model;

    private void ToggleSceneReferences()
    {
        _model.IncludeSceneReferences = !_model.IncludeSceneReferences;
        _sceneReferencesButton.Text = _model.IncludeSceneReferences ? "Scene refs: On" : "Scene refs: Off";
        Rebuild();
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _root.Measure(availableSize);
        return _root.DesiredSize;
    }

    protected override void OnArrange() => _root.Arrange(ContentRect);

    public void Refresh()
    {
        _model.SearchText = _search.Text;
        if (!_model.Refresh())
            return;
        Rebuild();
    }

    private void Rebuild()
    {
        _model.SearchText = _search.Text;
        _model.Refresh();
        RebuildFolders();
        RebuildAssets();
        _suppressFilterEvents = true;
        try
        {
            _typeFilter.Clear();
            foreach (var type in _model.Types)
                _typeFilter.AddItem(type.Length == 0 ? "All Types" : type);
            var selectedTypeIndex = _model.Types
                .Select((type, index) => (type, index))
                .FirstOrDefault(pair => string.Equals(pair.type, _model.SelectedType, StringComparison.OrdinalIgnoreCase)).index;
            _typeFilter.SelectedIndex = selectedTypeIndex;
        }
        finally
        {
            _suppressFilterEvents = false;
        }
    }

    private void RebuildFolders()
    {
        _suppressFolderEvents = true;
        try
        {
            _folders.Clear();
            _folderItems.Clear();
            var root = new UITreeViewItem("All Assets") { IsExpanded = true };
            _folderItems[EditorContentBrowserModel.AllDirectories] = root;
            _folders.AddRoot(root);

            foreach (var directory in _model.Directories.Where(path => path.Length > 0))
            {
                UITreeViewItem? parent = root;
                var currentPath = string.Empty;
                foreach (var segment in directory.Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    currentPath = currentPath.Length == 0 ? segment : currentPath + "/" + segment;
                    if (!_folderItems.TryGetValue(currentPath, out var item))
                    {
                        item = new UITreeViewItem(segment);
                        parent!.AddSubItem(item);
                        _folderItems[currentPath] = item;
                    }
                    parent = item;
                }
            }
            _folders.ExpandAll();
            _folders.SelectItem(_folderItems.TryGetValue(_model.SelectedDirectory, out var selected) ? selected : root);
        }
        finally
        {
            _suppressFolderEvents = false;
        }
    }

    private void RebuildAssets()
    {
        _assets.ClearItems();
        _assetItems.Clear();
        _childFolderItems.Clear();
        foreach (var folder in _model.ChildDirectories)
        {
            var name = folder[(folder.LastIndexOf('/') + 1)..];
            var item = _assets.AddItem($"[Folder] {name}");
            _childFolderItems[item] = folder;
        }
        foreach (var entry in _model.Entries)
        {
            var item = _assets.AddItem($"{entry.DisplayName}  [{entry.TypeName}]");
            _assetItems[item] = entry;
            if (_selectedGuid == entry.Record.AssetGuid)
                _assets.SelectItem(item);
        }
        _count.Text = $"{_model.Entries.Count} asset(s)";
        if (_selectedGuid is { } guid && !_model.Entries.Any(entry => entry.Record.AssetGuid == guid))
        {
            _selectedGuid = null;
            UpdateDetails(null);
        }
    }

    private void UpdateDetails(EditorContentBrowserEntry? entry)
    {
        _details.Text = entry == null
            ? "Select an asset to inspect it."
            : $"{entry.DisplayName} | {entry.TypeName} | {entry.StatusText} | {entry.Record.AssetGuid}\n{entry.Record.SourcePath ?? "(no source path)"}";
    }

    private string FindFolderPath(UITreeViewItem? item)
    {
        if (item == null)
            return EditorContentBrowserModel.AllDirectories;
        var segments = new Stack<string>();
        while (item != null && !ReferenceEquals(item, _folderItems[EditorContentBrowserModel.AllDirectories]))
        {
            segments.Push(item.Text);
            item = item.LogicalParent;
        }
        return string.Join('/', segments);
    }
}
