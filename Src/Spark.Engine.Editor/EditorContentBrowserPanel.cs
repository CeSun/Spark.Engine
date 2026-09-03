using System.Numerics;
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
    private readonly UITextBox _operationName;
    private readonly UIButton _createButton;
    private readonly UIMenuPanel _createMenu = new() { MinWidth = 160f, MaxWidth = 240f };
    private readonly UIMenuPanel _contextMenu = new() { MinWidth = 180f, MaxWidth = 280f };
    private readonly Dictionary<string, UITreeViewItem> _folderItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<UIListItem, EditorContentBrowserEntry> _assetItems = new();
    private readonly Dictionary<UIListItem, string> _childFolderItems = new();
    private Guid? _selectedGuid;
    private string? _selectedFolder;
    private bool _suppressFilterEvents;
    private bool _suppressFolderEvents;

    public EditorContentBrowserPanel(IAssetRegistry registry, string? contentDirectory = null)
    {
        _model = new EditorContentBrowserModel(registry, contentDirectory);
        var theme = UITheme.Default;
        _root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 0f),
            BackgroundColor = theme.PanelBackground,
        };

        var header = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 60f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Spacing = 4f,
            BackgroundColor = theme.StatusBarBackground,
        };
        var filters = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 24f),
            Spacing = 6f,
        };
        filters.AddChild(new UILabel
        {
            Text = "CONTENT BROWSER",
            TextColor = theme.TextDimColor,
            FixedSize = new UISize(150f, 24f),
            Padding = UIEdgeInsets.HorizontalVertical(0f, 2f),
        });
        _search = new UITextBox
        {
            FixedSize = new UISize(0f, 24f),
            PlaceholderText = "Search assets...",
            TextChanged = _ => Rebuild(),
        };
        filters.AddChild(_search);
        _typeFilter = new UIComboBox { FixedSize = new UISize(130f, 24f) };
        _typeFilter.SelectedItemChanged = selected =>
        {
            if (_suppressFilterEvents)
                return;
            _model.SelectedType = selected ?? EditorContentBrowserModel.AllTypes;
            Rebuild();
        };
        filters.AddChild(_typeFilter);
        var refresh = new UIButton { Text = "Refresh", FixedSize = new UISize(68f, 24f), Clicked = Refresh };
        filters.AddChild(refresh);
        _sceneReferencesButton = new UIButton
        {
            Text = "Scene refs: Off",
            FixedSize = new UISize(104f, 24f),
            Clicked = ToggleSceneReferences,
        };
        filters.AddChild(_sceneReferencesButton);
        header.AddChild(filters);

        var actions = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 24f),
            Spacing = 6f,
        };
        _operationName = new UITextBox
        {
            FixedSize = new UISize(0f, 24f),
            PlaceholderText = "Create / rename name...",
        };
        _operationName.Submitted = _ => RequestRename();
        actions.AddChild(_operationName);
        _createButton = new UIButton
        {
            Text = "Create",
            FixedSize = new UISize(58f, 24f),
            Clicked = ShowCreateMenu,
        };
        actions.AddChild(_createButton);
        actions.AddChild(new UIButton { Text = "Rename", FixedSize = new UISize(62f, 24f), Clicked = RequestRename });
        actions.AddChild(new UIButton { Text = "Copy", FixedSize = new UISize(48f, 24f), Clicked = RequestCopy });
        actions.AddChild(new UIButton { Text = "Delete", FixedSize = new UISize(56f, 24f), Clicked = RequestDelete });
        _count = new UILabel
        {
            TextColor = theme.TextDimColor,
            FixedSize = new UISize(86f, 24f),
            Padding = UIEdgeInsets.HorizontalVertical(4f, 2f),
        };
        actions.AddChild(_count);
        header.AddChild(actions);
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
        _folders.ItemDropped = (source, target, _) =>
        {
            var sourcePath = FindFolderPath(source);
            var targetPath = FindFolderPath(target);
            if (sourcePath.Length > 0 && !string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                FolderMoveRequested?.Invoke(sourcePath, targetPath);
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
            _selectedFolder = item != null && _childFolderItems.TryGetValue(item, out var folder) ? folder : null;
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
        _assets.ItemDropCompleted = (item, position, _) =>
        {
            if (TryFindFolderAt(position, out var treeFolder))
            {
                if (_assetItems.TryGetValue(item, out var treeMovedAsset))
                    AssetMoveRequested?.Invoke(treeMovedAsset.Record.AssetGuid, treeFolder);
                else if (_childFolderItems.TryGetValue(item, out var treeMovedFolder))
                    FolderMoveRequested?.Invoke(treeMovedFolder, treeFolder);
                return;
            }

            var target = _assets.Items.FirstOrDefault(candidate =>
                !ReferenceEquals(candidate, item) && candidate.Bounds.Contains(position));
            if (target != null && _childFolderItems.TryGetValue(target, out var targetFolder))
            {
                if (_assetItems.TryGetValue(item, out var movedAsset))
                    AssetMoveRequested?.Invoke(movedAsset.Record.AssetGuid, targetFolder);
                else if (_childFolderItems.TryGetValue(item, out var movedFolder))
                    FolderMoveRequested?.Invoke(movedFolder, targetFolder);
            }
            else if (_assetItems.TryGetValue(item, out var entry))
                AssetDropped?.Invoke(entry.Record, position);
        };
        _assets.ItemKeyPressed = (_, key, keysDown) =>
        {
            var control = keysDown.IsDown(Spark.Engine.Input.Key.LeftControl) ||
                          keysDown.IsDown(Spark.Engine.Input.Key.RightControl);
            if (key == Spark.Engine.Input.Key.F2)
                BeginRename();
            else if (key == Spark.Engine.Input.Key.Delete)
                RequestDelete();
            else if (control && key == Spark.Engine.Input.Key.D)
                RequestCopy();
        };
        _assets.ItemContextRequested = (_, position) => ShowContextMenu(position);
        assetColumn.AddChild(_assets);
        _details = new UILabel { Text = "Select an asset to inspect it.", TextColor = theme.TextDimColor, Padding = UIEdgeInsets.HorizontalVertical(4f, 4f) };
        assetColumn.AddChild(_details);
        body.AddChild(assetColumn);
        _root.AddChild(body);
        AddChild(_root);

        Rebuild();
    }

    /// <summary>双击或回车激活资源；宿主据此打开对应的资源编辑器。</summary>
    public event Action<AssetRecord>? AssetActivated;
    /// <summary>资源项在画布坐标处释放；文件夹项不会触发。</summary>
    public event Action<AssetRecord, Vector2>? AssetDropped;
    public event Action<string, string>? FolderCreateRequested;
    public event Action<string, string>? MaterialCreateRequested;
    public event Action<string, string>? FolderRenameRequested;
    public event Action<string, string>? FolderMoveRequested;
    public event Action<string, string>? FolderCopyRequested;
    public event Action<string>? FolderDeleteRequested;
    public event Action<Guid, string>? AssetRenameRequested;
    public event Action<Guid, string>? AssetMoveRequested;
    public event Action<Guid, string>? AssetCopyRequested;
    public event Action<Guid>? AssetDeleteRequested;
    public EditorContentBrowserModel Model => _model;

    public bool RevealAsset(Guid assetGuid)
    {
        var record = _model.FindAsset(assetGuid);
        if (record == null)
            return false;
        if (!record.IsPersistent)
        {
            _model.IncludeSceneReferences = true;
            _sceneReferencesButton.Text = "Scene refs: On";
            _model.SelectedDirectory = EditorContentBrowserModel.AllDirectories;
        }
        else
        {
            _model.SelectedDirectory = EditorContentBrowserModel.GetDirectory(
                record.ContentPath ?? record.SourcePath);
        }
        _search.Text = string.Empty;
        _model.SearchText = string.Empty;
        _model.SelectedType = EditorContentBrowserModel.AllTypes;
        _selectedGuid = assetGuid;
        _selectedFolder = null;
        Rebuild();
        return _model.Entries.Any(entry => entry.Record.AssetGuid == assetGuid);
    }

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
            if (string.Equals(_selectedFolder, folder, StringComparison.OrdinalIgnoreCase))
                _assets.SelectItem(item);
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
        if (_selectedFolder != null && !_model.ChildDirectories.Contains(_selectedFolder, StringComparer.OrdinalIgnoreCase))
            _selectedFolder = null;
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

    private bool TryFindFolderAt(Vector2 position, out string folder)
    {
        foreach (var pair in _folderItems)
        {
            if (pair.Value.Visible && pair.Value.Bounds.Contains(position))
            {
                folder = pair.Key;
                return true;
            }
        }
        folder = EditorContentBrowserModel.AllDirectories;
        return false;
    }

    private void RequestNewFolder()
    {
        var name = RequireOperationName("creating a folder");
        if (name != null)
            FolderCreateRequested?.Invoke(_model.SelectedDirectory, name);
    }

    private void RequestNewMaterial()
    {
        var name = RequireOperationName("creating a Material");
        if (name != null)
            MaterialCreateRequested?.Invoke(_model.SelectedDirectory, name);
    }

    private void ShowCreateMenu()
    {
        _createMenu.Clear();
        _createMenu.AddItem(new UIMenuItem("Folder", RequestNewFolder));
        _createMenu.AddItem(new UIMenuItem("Material", RequestNewMaterial));
        _createMenu.Canvas = FindCanvas();
        _createMenu.Show(new Vector2(_createButton.Bounds.X, _createButton.Bounds.Bottom));
    }

    private string? RequireOperationName(string operation)
    {
        var name = _operationName.Text.Trim();
        if (name.Length > 0)
            return name;
        _details.Text = $"Enter a name before {operation}.";
        _operationName.FindCanvas()?.Focus(_operationName);
        return null;
    }

    private void BeginRename()
    {
        if (_selectedGuid is { } guid)
        {
            var record = _model.Entries.FirstOrDefault(entry => entry.Record.AssetGuid == guid)?.Record;
            if (record != null)
                _operationName.Text = Path.GetFileNameWithoutExtension(EditorContentBrowserModel.GetDisplayName(record));
        }
        else
        {
            var folder = _selectedFolder ?? _model.SelectedDirectory;
            if (folder.Length > 0)
                _operationName.Text = folder[(folder.LastIndexOf('/') + 1)..];
        }
        _operationName.FindCanvas()?.Focus(_operationName);
        _operationName.SelectAll();
    }

    private void RequestRename()
    {
        var name = _operationName.Text.Trim();
        if (name.Length == 0)
            return;
        if (_selectedGuid is { } guid)
            AssetRenameRequested?.Invoke(guid, name);
        else
        {
            var folder = _selectedFolder ?? _model.SelectedDirectory;
            if (folder.Length > 0)
                FolderRenameRequested?.Invoke(folder, name);
        }
    }

    private void RequestCopy()
    {
        if (_selectedGuid is { } guid)
            AssetCopyRequested?.Invoke(guid, _model.SelectedDirectory);
        else if (_selectedFolder is { } folder)
            FolderCopyRequested?.Invoke(folder, _model.SelectedDirectory);
    }

    private void RequestDelete()
    {
        if (_selectedGuid is { } guid)
            AssetDeleteRequested?.Invoke(guid);
        else
        {
            var folder = _selectedFolder ?? _model.SelectedDirectory;
            if (folder.Length > 0)
                FolderDeleteRequested?.Invoke(folder);
        }
    }

    private void ShowContextMenu(Vector2 position)
    {
        var hasSelection = _selectedGuid.HasValue || _selectedFolder != null || _model.SelectedDirectory.Length > 0;
        var canCopy = _selectedGuid.HasValue || _selectedFolder != null;
        _contextMenu.Clear();
        _contextMenu.AddItem(new UIMenuItem("New Folder", RequestNewFolder));
        _contextMenu.AddItem(new UIMenuItem("New Material", RequestNewMaterial));
        _contextMenu.AddSeparator();
        _contextMenu.AddItem(new UIMenuItem("Rename", BeginRename)
        {
            Shortcut = "F2",
            IsEnabled = hasSelection,
        });
        _contextMenu.AddItem(new UIMenuItem("Copy", RequestCopy)
        {
            Shortcut = "Ctrl+D",
            IsEnabled = canCopy,
        });
        _contextMenu.AddItem(new UIMenuItem("Delete", RequestDelete)
        {
            Shortcut = "Delete",
            IsEnabled = hasSelection,
        });
        _contextMenu.Canvas = FindCanvas();
        _contextMenu.Show(position);
    }
}
