using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.UI;
using Spark.Engine.Worlds;
using System.Numerics;

namespace Spark.Engine.Editor;

/// <summary>
/// UE 风格场景大纲：默认显示 Actor 及跨 Actor 的空间挂载层级；Component 仅作为可选开发者视图。
/// 结构变化通过 World/Outliner 版本 O(1) 检测，并按稳定 Guid 保留展开和选择状态。
/// </summary>
public sealed class HierarchyPanel
{
    private World _world;
    private EditorWorldOutlinerData _outliner;
    private readonly EditorOutlinerViewState _viewState;
    private readonly EditorOutlinerExtensionRegistry _extensions;
    private EditorOutlinerQuery _query;
    private readonly UITreeView _tree;
    private readonly Dictionary<object, WorldTreeItem> _itemCache = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, WorldTreeItem> _extensionItemCache = new(StringComparer.Ordinal);
    private readonly Dictionary<object, EditorOutlinerSearchRecord> _searchIndex = new(ReferenceEqualityComparer.Instance);

    private long _lastWorldRevision = -1;
    private long _lastOutlinerRevision = -1;
    private long _viewRevision;
    private long _lastViewRevision = -1;
    private long _lastExtensionRevision = -1;
    private long _lastProviderRevision = -1;
    private bool _suppressSelectionChanged;
    private bool _suppressViewStateChanged;
    private bool _displayingFilteredTree;
    private IReadOnlyList<object> _selectedTargets = Array.Empty<object>();
    private object? _primaryTarget;

    /// <summary>选中项变化：参数为选中的 Actor/Component 或 null。</summary>
    public Action<object?>? SelectionChanged { get; set; }

    /// <summary>选择集合变化；第二个参数为最后操作的主选目标。</summary>
    public Action<IReadOnlyList<object>, object?>? SelectionSetChanged { get; set; }

    /// <summary>层级项拖放完成；参数为源目标、放置目标和画布位置。</summary>
    public Action<object, object, System.Numerics.Vector2>? ItemDropped { get; set; }
    public Action<object, System.Numerics.Vector2>? ItemContextRequested { get; set; }
    public Action<object>? VisibilityToggled { get; set; }
    public Func<object, string, bool>? RenameSubmitted { get; set; }
    public Action<object>? DeleteRequested { get; set; }
    public Action<System.Numerics.Vector2>? BackgroundContextRequested { get; set; }
    public Action<object, System.Numerics.Vector2>? ItemDroppedOnBackground { get; set; }
    public Action? ViewStateChanged { get; set; }
    public bool IsReadOnly { get; private set; }
    public bool IsRuntimeView { get; private set; }
    public int RebuildCount { get; private set; }
    public int ItemCreationCount { get; private set; }
    public World World => _world;

    public HierarchyPanel(World world, EditorWorldOutlinerData? outliner = null,
        EditorOutlinerViewState? viewState = null, EditorOutlinerExtensionRegistry? extensions = null)
    {
        _world = world;
        _outliner = outliner ?? EditorWorldOutlinerData.For(world);
        _viewState = viewState ?? new EditorOutlinerViewState();
        _extensions = extensions ?? new EditorOutlinerExtensionRegistry();
        _query = EditorOutlinerQuery.Parse(_viewState.SearchText);
        _tree = new UITreeView
        {
            BackgroundColor = new(0f, 0f, 0f, 0f),
            AllowMultipleSelection = true,
            AutoScrollSelection = _viewState.AlwaysFrameSelection,
        };
        _tree.FixedSize = new UISize(0f, 0f); // ≤0 = 拉伸填满（否则高度只有一行 ItemHeight）
        _tree.ScrollOffset = GetStoredScrollOffset();
        if (!IsReadOnly && _viewState.CurrentFolderGuid is { } currentFolder && _outliner.FindFolder(currentFolder) != null)
            _outliner.SetCurrentFolder(currentFolder);
        else
            _viewState.CurrentFolderGuid = _outliner.CurrentFolderGuid;
        _tree.ViewStateChanged += CaptureViewState;
        _tree.SelectionSetChanged += items =>
        {
            if (_suppressSelectionChanged)
                return;
            var targets = items.OfType<WorldTreeItem>().Select(item => item.Target).ToArray();
            var primary = (_tree.SelectedItem as WorldTreeItem)?.Target;
            _selectedTargets = targets;
            _primaryTarget = primary;
            SelectionChanged?.Invoke(primary);
            SelectionSetChanged?.Invoke(targets, primary);
        };
        _tree.ItemDropped += (source, target, position) =>
        {
            var sourceTarget = ((WorldTreeItem)source).Target;
            var targetTarget = ((WorldTreeItem)target).Target;
            if (sourceTarget is not ActorComponent && targetTarget is not ActorComponent)
                ItemDropped?.Invoke(sourceTarget, targetTarget, position);
        };
        _tree.ItemContextRequested += (item, position) =>
            ItemContextRequested?.Invoke(((WorldTreeItem)item).Target, position);
        _tree.ItemVisibilityClicked += item =>
            VisibilityToggled?.Invoke(((WorldTreeItem)item).Target);
        _tree.BackgroundContextRequested += position => BackgroundContextRequested?.Invoke(position);
        _tree.ItemDroppedOnBackground += (item, position) =>
            ItemDroppedOnBackground?.Invoke(((WorldTreeItem)item).Target, position);
        _tree.ItemKeyPressed += (item, key, _) =>
        {
            if (key == Spark.Engine.Input.Key.F2 && item is WorldTreeItem worldItem)
                BeginRename(worldItem.Target);
            else if (!IsReadOnly && key == Spark.Engine.Input.Key.Delete && item is WorldTreeItem deleteItem)
                DeleteRequested?.Invoke(deleteItem.Target);
        };
    }

    /// <summary>树控件本身（挂进编辑器布局）。</summary>
    public UIElement Element => _tree;

    public string SearchText
    {
        get => _viewState.SearchText;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            if (string.Equals(_viewState.SearchText, next, StringComparison.Ordinal))
                return;
            _viewState.SearchText = next;
            _query = EditorOutlinerQuery.Parse(next);
            InvalidateFilter();
        }
    }

    public bool ShowInternalActors
    {
        get => _viewState.ShowInternalActors;
        set
        {
            if (_viewState.ShowInternalActors == value)
                return;
            _viewState.ShowInternalActors = value;
            InvalidateFilter();
        }
    }

    public bool ShowComponents
    {
        get => _viewState.ShowDeveloperComponents;
        set
        {
            if (_viewState.ShowDeveloperComponents == value)
                return;
            _viewState.ShowDeveloperComponents = value;
            InvalidateFilter();
        }
    }

    public bool OnlySelected
    {
        get => _viewState.OnlySelected;
        set
        {
            if (_viewState.OnlySelected == value)
                return;
            _viewState.OnlySelected = value;
            InvalidateFilter();
        }
    }

    public bool HideTemporarilyHidden
    {
        get => _viewState.HideTemporarilyHidden;
        set { if (_viewState.HideTemporarilyHidden != value) { _viewState.HideTemporarilyHidden = value; InvalidateFilter(); } }
    }

    public bool AlwaysFrameSelection
    {
        get => _viewState.AlwaysFrameSelection;
        set { _viewState.AlwaysFrameSelection = value; _tree.AutoScrollSelection = value; }
    }

    public IReadOnlyList<string> AvailableActorTypes => _world.EnumerateActors(includePendingActors: true)
        .Where(actor => ShowInternalActors || EditorActorPolicy.IsVisibleInOutliner(actor))
        .Select(GetActorTypeLabel).Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlySet<string> ActorTypeFilters => _viewState.ActorTypes;

    public string SortColumnId => _viewState.SortColumnId;
    public bool SortAscending => _viewState.SortAscending;

    public void SortBy(EditorOutlinerColumn column)
        => SortBy(column switch
        {
            EditorOutlinerColumn.Type => EditorOutlinerColumnIds.Type,
            EditorOutlinerColumn.Socket => EditorOutlinerColumnIds.Socket,
            EditorOutlinerColumn.Id => EditorOutlinerColumnIds.Id,
            _ => EditorOutlinerColumnIds.Label,
        });

    public void SortBy(string columnId)
    {
        if (_extensions.FindColumn(columnId) == null)
            return;
        if (string.Equals(_viewState.SortColumnId, columnId, StringComparison.OrdinalIgnoreCase))
            _viewState.SortAscending = !_viewState.SortAscending;
        else
        {
            _viewState.SortColumnId = columnId;
            _viewState.SortAscending = true;
        }
        InvalidateView();
        ViewStateChanged?.Invoke();
    }

    public void ToggleActorTypeFilter(string actorType)
    {
        if (!_viewState.ActorTypes.Remove(actorType))
            _viewState.ActorTypes.Add(actorType);
        InvalidateFilter();
    }

    public void ClearActorTypeFilters()
    {
        if (_viewState.ActorTypes.Count == 0) return;
        _viewState.ActorTypes.Clear();
        InvalidateFilter();
    }

    public bool IsExtensionFilterEnabled(string filterId)
        => _viewState.EnabledExtensionFilters.Contains(filterId);

    public void ToggleExtensionFilter(string filterId)
    {
        if (!_viewState.EnabledExtensionFilters.Remove(filterId))
            _viewState.EnabledExtensionFilters.Add(filterId);
        InvalidateFilter();
    }

    public void ClearExtensionFilters()
    {
        if (_viewState.EnabledExtensionFilters.Count == 0)
            return;
        _viewState.EnabledExtensionFilters.Clear();
        InvalidateFilter();
    }

    public void SetWorld(World world, bool isReadOnly = false, bool isRuntimeView = false)
    {
        if (!ReferenceEquals(_world, world))
            CaptureViewState();
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _outliner = EditorWorldOutlinerData.For(world);
        IsReadOnly = isReadOnly;
        IsRuntimeView = isRuntimeView;
        _selectedTargets = Array.Empty<object>();
        _primaryTarget = null;
        _displayingFilteredTree = false;
        _itemCache.Clear();
        _extensionItemCache.Clear();
        _searchIndex.Clear();
        _lastWorldRevision = -1;
        _lastOutlinerRevision = -1;
        _lastViewRevision = -1;
        _lastExtensionRevision = -1;
        _lastProviderRevision = -1;
        _suppressSelectionChanged = true;
        try { _tree.Clear(); }
        finally { _suppressSelectionChanged = false; }
        _tree.ScrollOffset = GetStoredScrollOffset();
        if (!IsReadOnly && _viewState.CurrentFolderGuid is { } currentFolder && _outliner.FindFolder(currentFolder) != null)
            _outliner.SetCurrentFolder(currentFolder);
    }

    /// <summary>当前选中的目标（Actor 或 Component；无选中为 null）。</summary>
    public object? SelectedTarget => _primaryTarget;

    public IReadOnlyList<object> SelectedTargets => _selectedTargets;

    /// <summary>每帧调用：只比较 World、View 与扩展版本；无变化时不会扫描 World 或重建树。</summary>
    public void Refresh()
    {
        if (!IsReadOnly && _viewState.CurrentFolderGuid != _outliner.CurrentFolderGuid)
        {
            _viewState.CurrentFolderGuid = _outliner.CurrentFolderGuid;
            ViewStateChanged?.Invoke();
        }
        var providerRevision = GetProviderRevision();
        if (_world.StructureRevision == _lastWorldRevision &&
            _outliner.Revision == _lastOutlinerRevision &&
            _viewRevision == _lastViewRevision &&
            _extensions.Revision == _lastExtensionRevision &&
            providerRevision == _lastProviderRevision)
            return;

        if (_world.StructureRevision != _lastWorldRevision || _outliner.Revision != _lastOutlinerRevision ||
            _extensions.Revision != _lastExtensionRevision)
            _searchIndex.Clear();
        _lastWorldRevision = _world.StructureRevision;
        _lastOutlinerRevision = _outliner.Revision;
        _lastViewRevision = _viewRevision;
        _lastExtensionRevision = _extensions.Revision;
        _lastProviderRevision = providerRevision;
        Rebuild();
    }

    private long GetProviderRevision()
    {
        var context = new EditorOutlinerNodeContext(_world, _outliner);
        long revision = 17;
        foreach (var provider in _extensions.NodeProviders)
        {
            try { revision = unchecked(revision * 31 + provider.GetRevision(context)); }
            catch { revision = unchecked(revision * 31); }
        }
        return revision;
    }

    private void Rebuild()
    {
        RebuildCount++;
        var selected = _selectedTargets;
        var primary = _primaryTarget;
        var isContextFilter = !_query.IsEmpty || OnlySelected || HideTemporarilyHidden ||
            _viewState.ActorTypes.Count != 0 || _viewState.EnabledExtensionFilters.Count != 0;
        var scrollOffset = _displayingFilteredTree && !isContextFilter
            ? GetStoredScrollOffset()
            : _tree.ScrollOffset;
        if (!_displayingFilteredTree)
        {
            CaptureExpansionState(_tree.Roots);
            SetStoredScrollOffset(_tree.ScrollOffset);
        }

        _suppressSelectionChanged = true;
        _suppressViewStateChanged = true;
        try
        {
            _tree.Clear();
            var actors = GetVisibleActors();
            var actorSet = actors.ToHashSet();
            var folderItems = new Dictionary<Guid, WorldTreeItem>();
            var rootItems = new List<WorldTreeItem>();
            var folders = GetVisibleFolders(actors);
            foreach (var folder in folders.OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase))
            {
                var item = CreateTreeItem(folder, folder.Name);
                item.IsExpanded = !_viewState.FolderExpansion.TryGetValue(folder.FolderGuid, out var expanded) || expanded;
                folderItems.Add(folder.FolderGuid, item);
            }
            var actorItems = new Dictionary<Actor, WorldTreeItem>();
            foreach (var actor in actors)
            {
                var displayName = string.IsNullOrWhiteSpace(actor.Name) ? actor.GetType().Name : actor.Name;
                var actorItem = CreateTreeItem(actor, displayName);
                actorItem.IsExpanded = !GetActorExpansion().TryGetValue(actor.ActorGuid, out var expanded) || expanded;
                foreach (var component in VisibleComponents(actor))
                    actorItem.AddSubItem(CreateTreeItem(component, GetComponentLabel(component)));
                actorItems.Add(actor, actorItem);
            }

            foreach (var folder in folders.OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase))
            {
                var item = folderItems[folder.FolderGuid];
                if (folder.ParentFolderGuid is { } parentGuid && folderItems.TryGetValue(parentGuid, out var parentItem))
                    parentItem.AddSubItem(item);
                else
                    rootItems.Add(item);
            }

            foreach (var actor in actors)
            {
                var actorItem = actorItems[actor];
                var parent = GetOutlinerParent(actor, actorSet);
                if (parent != null)
                    actorItems[parent].AddSubItem(actorItem);
                else if (_outliner.GetActorFolder(actor.ActorGuid) is { } folderGuid &&
                    folderItems.TryGetValue(folderGuid, out var folderItem))
                    folderItem.AddSubItem(actorItem);
                else
                    rootItems.Add(actorItem);
            }

            var itemsById = new Dictionary<string, WorldTreeItem>(StringComparer.Ordinal);
            foreach (var pair in folderItems)
                itemsById[EditorOutlinerNodeIds.Folder(pair.Key)] = pair.Value;
            foreach (var pair in actorItems)
                itemsById[EditorOutlinerNodeIds.Actor(pair.Key.ActorGuid)] = pair.Value;
            var extensionNodes = GetExtensionNodes()
                .Where(node => !itemsById.ContainsKey(node.StableId))
                .ToArray();
            var extensionNodesById = extensionNodes.ToDictionary(node => node.StableId, StringComparer.Ordinal);
            foreach (var node in extensionNodes)
            {
                if (!itemsById.ContainsKey(node.StableId))
                    itemsById.Add(node.StableId, CreateExtensionTreeItem(node));
            }
            foreach (var node in extensionNodes)
            {
                if (!itemsById.TryGetValue(node.StableId, out var item))
                    continue;
                if (node.ParentId != null && !HasExtensionParentCycle(node, extensionNodesById) &&
                    itemsById.TryGetValue(node.ParentId, out var parent) &&
                    !ReferenceEquals(parent, item))
                    parent.AddSubItem(item);
                else
                    rootItems.Add(item);
            }

            foreach (var item in rootItems)
                SortChildren(item);
            rootItems.Sort(CompareItems);
            _tree.SetRoots(rootItems);
            PruneItemCache();
            PruneExtensionItemCache(extensionNodes.Select(node => node.StableId));

            if (isContextFilter)
            {
                foreach (var item in actorItems.Values.Concat(folderItems.Values).Where(item => !item.IsLeaf))
                    item.IsExpanded = true;
                _tree.RebuildFlatList();
            }

            SelectTargets(selected, primary);
            _tree.ScrollOffset = scrollOffset;
        }
        finally
        {
            _displayingFilteredTree = isContextFilter;
            _suppressSelectionChanged = false;
            _suppressViewStateChanged = false;
        }
    }

    private IReadOnlyList<EditorOutlinerNodeDescriptor> GetExtensionNodes()
    {
        var result = new List<EditorOutlinerNodeDescriptor>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var context = new EditorOutlinerNodeContext(_world, _outliner);
        foreach (var provider in _extensions.NodeProviders)
        {
            IEnumerable<EditorOutlinerNodeDescriptor> nodes;
            try { nodes = provider.GetNodes(context) ?? []; }
            catch { continue; }
            try
            {
                foreach (var node in nodes)
                {
                    if (node != null && ids.Add(node.StableId))
                        result.Add(node);
                }
            }
            catch
            {
                // 一个扩展失败时仍保留内置 Actor/Folder 树。
            }
        }
        if (_query.IsEmpty && !OnlySelected && _viewState.EnabledExtensionFilters.Count == 0)
            return result;
        var nodesById = result.ToDictionary(node => node.StableId, StringComparer.Ordinal);
        var visibleIds = result
            .Where(node => (!OnlySelected || _selectedTargets.Any(target => ReferenceEquals(target, node.Target))) &&
                (_query.IsEmpty || _query.Matches(GetSearchRecord(node.Target))) &&
                MatchesExtensionFilters(node.Target))
            .Select(node => node.StableId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in visibleIds.ToArray())
        {
            for (var parentId = nodesById[id].ParentId;
                 parentId != null && nodesById.TryGetValue(parentId, out var parent);
                 parentId = parent.ParentId)
            {
                if (!visibleIds.Add(parentId))
                    break;
            }
        }
        return result.Where(node => visibleIds.Contains(node.StableId)).ToArray();
    }

    private static bool HasExtensionParentCycle(EditorOutlinerNodeDescriptor node,
        IReadOnlyDictionary<string, EditorOutlinerNodeDescriptor> nodes)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { node.StableId };
        for (var parentId = node.ParentId; parentId != null && nodes.TryGetValue(parentId, out var parent);
             parentId = parent.ParentId)
        {
            if (!visited.Add(parentId))
                return true;
        }
        return false;
    }

    private IReadOnlyList<Actor> GetVisibleActors()
    {
        var candidates = _world.EnumerateActors(includePendingActors: true)
            .Where(actor => ShowInternalActors || EditorActorPolicy.IsVisibleInOutliner(actor))
            .Where(actor => !HideTemporarilyHidden || !_outliner.IsActorTemporarilyHidden(actor.ActorGuid))
            .ToArray();
        var candidateSet = candidates.ToHashSet();
        var matchingFolderGuids = _query.IsEmpty
            ? new HashSet<Guid>()
            : _outliner.Folders.Where(MatchesFolder)
                .Select(folder => folder.FolderGuid).ToHashSet();
        var directlyVisible = candidates
            .Where(actor => _viewState.ActorTypes.Count == 0 || _viewState.ActorTypes.Contains(GetActorTypeLabel(actor)))
            .Where(MatchesExtensionFilters)
            .Where(actor => !OnlySelected || IsActorSelected(actor))
            .Where(actor => MatchesSearch(actor) || IsInFolderSubtree(actor, matchingFolderGuids))
            .ToHashSet();

        // 过滤命中的 Actor 仍需显示挂载祖先，才能保留空间层级上下文。
        foreach (var actor in directlyVisible.ToArray())
        {
            for (var parent = GetOutlinerParent(actor, candidateSet);
                 parent != null;
                 parent = GetOutlinerParent(parent, candidateSet))
                directlyVisible.Add(parent);
        }

        return candidates.Where(directlyVisible.Contains).ToArray();
    }

    private IReadOnlyList<EditorActorFolder> GetVisibleFolders(IReadOnlyList<Actor> visibleActors)
    {
        if (_query.IsEmpty && !OnlySelected && !HideTemporarilyHidden && _viewState.ActorTypes.Count == 0 &&
            _viewState.EnabledExtensionFilters.Count == 0)
            return _outliner.Folders.ToArray();

        var visibleFolderGuids = new HashSet<Guid>();
        if (!_query.IsEmpty)
        {
            foreach (var folder in _outliner.Folders.Where(MatchesFolder))
                AddFolderAndAncestors(folder.FolderGuid, visibleFolderGuids);
        }
        foreach (var actor in visibleActors)
        {
            if (_outliner.GetActorFolder(actor.ActorGuid) is { } folderGuid)
                AddFolderAndAncestors(folderGuid, visibleFolderGuids);
        }
        foreach (var selectedFolder in _selectedTargets.OfType<EditorActorFolder>())
            AddFolderAndAncestors(selectedFolder.FolderGuid, visibleFolderGuids);
        return _outliner.Folders.Where(folder => visibleFolderGuids.Contains(folder.FolderGuid)).ToArray();
    }

    private bool IsInFolderSubtree(Actor actor, IReadOnlySet<Guid> folderGuids)
    {
        for (var folderGuid = _outliner.GetActorFolder(actor.ActorGuid); folderGuid.HasValue;)
        {
            if (folderGuids.Contains(folderGuid.Value))
                return true;
            folderGuid = _outliner.FindFolder(folderGuid.Value)?.ParentFolderGuid;
        }
        return false;
    }

    private void AddFolderAndAncestors(Guid folderGuid, ISet<Guid> result)
    {
        for (Guid? current = folderGuid; current.HasValue; current = _outliner.FindFolder(current.Value)?.ParentFolderGuid)
        {
            if (!result.Add(current.Value))
                break;
        }
    }

    private IEnumerable<ActorComponent> VisibleComponents(Actor actor)
    {
        if (!ShowComponents)
            return Array.Empty<ActorComponent>();
        var components = actor.Components;
        if (_query.IsEmpty || MatchesActor(actor))
            return components;
        return components.Where(component => _query.Matches(GetSearchRecord(component)));
    }

    private bool MatchesSearch(Actor actor) => _query.IsEmpty || MatchesActor(actor);

    private bool MatchesActor(Actor actor) => _query.Matches(GetSearchRecord(actor));

    private bool MatchesFolder(EditorActorFolder folder) => _query.Matches(GetSearchRecord(folder));

    private bool MatchesExtensionFilters(object target)
    {
        foreach (var filter in _extensions.Filters)
        {
            if (!_viewState.EnabledExtensionFilters.Contains(filter.Id))
                continue;
            try
            {
                if (!filter.Predicate(target))
                    return false;
            }
            catch
            {
                return false;
            }
        }
        return true;
    }

    private EditorOutlinerSearchRecord GetSearchRecord(object target)
    {
        if (_searchIndex.TryGetValue(target, out var record))
            return record;
        record = target switch
        {
            Actor actor => new EditorOutlinerSearchRecord(
            string.IsNullOrWhiteSpace(actor.Name) ? actor.GetType().Name : actor.Name,
            GetActorTypeLabel(actor), GetActorFolderPath(actor), actor.ActorGuid.ToString(),
            GetActorSocketLabel(actor), actor.Components.Select(component => component.GetType().Name).ToArray()),
            ActorComponent component => new EditorOutlinerSearchRecord(
            component.GetType().Name, component.GetType().Name,
            component.Owner == null ? string.Empty : GetActorFolderPath(component.Owner),
            component.ComponentGuid.ToString(), component is SceneComponent scene ? scene.AttachSocketName ?? string.Empty : string.Empty,
            [component.GetType().Name]),
            EditorActorFolder folder => new EditorOutlinerSearchRecord(
                folder.Name, "Folder", GetFolderPath(folder), folder.FolderGuid.ToString(),
                string.Empty, Array.Empty<string>()),
            EditorUnloadedActorDescriptor actor => new EditorOutlinerSearchRecord(
                actor.Label, actor.ActorType, string.Empty, actor.ActorGuid.ToString(),
                string.Empty, Array.Empty<string>()),
            _ => new EditorOutlinerSearchRecord(string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty, Array.Empty<string>()),
        };
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var extensionValues = new List<string>();
        foreach (var column in _extensions.Columns.Where(column => column.Searchable))
        {
            var value = GetColumnText(target, column);
            fields[column.Id] = value;
            if (column.Id is not EditorOutlinerColumnIds.Label and not EditorOutlinerColumnIds.Type and
                not EditorOutlinerColumnIds.Socket and not EditorOutlinerColumnIds.Id && value.Length != 0)
                extensionValues.Add(value);
        }
        record = record with { Fields = fields, ExtensionValues = extensionValues };
        _searchIndex[target] = record;
        return record;
    }

    private string GetActorFolderPath(Actor actor)
        => _outliner.GetActorFolder(actor.ActorGuid) is { } folderGuid && _outliner.FindFolder(folderGuid) is { } folder
            ? GetFolderPath(folder) : string.Empty;

    private string GetFolderPath(EditorActorFolder folder)
    {
        var names = new Stack<string>();
        for (EditorActorFolder? current = folder; current != null;
             current = current.ParentFolderGuid is { } parent ? _outliner.FindFolder(parent) : null)
            names.Push(current.Name);
        return string.Join('/', names);
    }

    private static string GetActorSocketLabel(Actor actor)
    {
        var root = actor.RootComponent;
        if (root?.AttachParent == null)
            return string.Empty;
        var component = root.AttachParent.GetType().Name;
        return root.AttachSocketName == null ? component : $"{component}:{root.AttachSocketName}";
    }

    private bool IsActorSelected(Actor actor)
        => _selectedTargets.Any(target => target switch
        {
            Actor selectedActor => ReferenceEquals(selectedActor, actor),
            ActorComponent component => ReferenceEquals(component.Owner, actor),
            _ => false,
        });

    private static string GetStableSelectionId(object target)
        => target switch
        {
            Actor actor => actor.ActorGuid.ToString("N"),
            ActorComponent component => component.ComponentGuid.ToString("N"),
            EditorActorFolder folder => folder.FolderGuid.ToString("N"),
            EditorUnloadedActorDescriptor actor => actor.ActorGuid.ToString("N"),
            _ => target.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    private WorldTreeItem CreateTreeItem(object target, string text)
    {
        var isFolder = target is EditorActorFolder;
        var selectable = isFolder || EditorActorPolicy.CanSelect(target);
        var editable = !IsReadOnly && (isFolder || EditorActorPolicy.CanEdit(target));
        var isActor = target is Actor;
        var isSpatialActor = target is Actor { RootComponent: not null };
        if (!_itemCache.TryGetValue(target, out var item))
        {
            item = new WorldTreeItem(target, text);
            _itemCache.Add(target, item);
            ItemCreationCount++;
        }
        item.ResetLogicalHierarchy();
        item.Text = text;
        item.IsSelectable = selectable;
        item.IsDraggable = (isActor || isFolder) && editable;
        item.IsDropTarget = (isSpatialActor || isFolder) && editable;
        item.Focusable = selectable;
        item.TextColor = selectable ? UITheme.Default.TextColor : UITheme.Default.TextDimColor;
        item.IconColor = isActor ? GetActorIconColor((Actor)target, selectable)
            : isFolder ? GetFolderIconColor((EditorActorFolder)target) : null;
        item.BadgeText = IsRuntimeView && isActor ? "PIE" : string.Empty;
        item.ShowVisibilityToggle = isActor || isFolder;
        item.ReserveVisibilityColumn = true;
        item.VisibilityState = GetVisibilityState(target);
        item.SecondaryCells = CreateSecondaryCells(target);
        return item;
    }

    private WorldTreeItem CreateExtensionTreeItem(EditorOutlinerNodeDescriptor node)
    {
        if (!_extensionItemCache.TryGetValue(node.StableId, out var item))
        {
            item = new WorldTreeItem(node.Target, node.Label);
            _extensionItemCache.Add(node.StableId, item);
            ItemCreationCount++;
        }
        item.ResetLogicalHierarchy();
        item.Text = node.Label;
        item.IsSelectable = node.IsSelectable;
        item.IsDraggable = false;
        item.IsDropTarget = false;
        item.Focusable = node.IsSelectable;
        item.TextColor = UITheme.Default.TextDimColor;
        item.IconColor = new Vector4(0.45f, 0.48f, 0.52f, 1f);
        item.BadgeText = node.Target is EditorUnloadedActorDescriptor ? "UNLOADED" : string.Empty;
        item.ShowVisibilityToggle = false;
        item.ReserveVisibilityColumn = true;
        item.SecondaryCells = CreateSecondaryCells(node.Target);
        return item;
    }

    private IReadOnlyList<UITreeViewCell> CreateSecondaryCells(object target)
    {
        var columns = _extensions.Columns
            .Where(column => column.Id != EditorOutlinerColumnIds.Label && _viewState.IsColumnVisible(column));
        var cells = new List<UITreeViewCell>();
        foreach (var column in columns)
            cells.Add(new UITreeViewCell(GetColumnText(target, column), _viewState.GetColumnWidth(column)));
        return cells;
    }

    private static string GetColumnText(object target, EditorOutlinerColumnDescriptor column)
    {
        try { return column.GetText(target) ?? string.Empty; }
        catch { return string.Empty; }
    }

    private string GetColumnText(object target, EditorOutlinerColumn column)
        => column switch
        {
            EditorOutlinerColumn.Label => target switch
            {
                Actor actor => string.IsNullOrWhiteSpace(actor.Name) ? actor.GetType().Name : actor.Name,
                EditorActorFolder folder => folder.Name,
                ActorComponent component => GetComponentLabel(component),
                _ => string.Empty,
            },
            EditorOutlinerColumn.Type => target switch
            {
                Actor actor => GetActorTypeLabel(actor),
                EditorActorFolder => "Folder",
                ActorComponent component => component.GetType().Name,
                _ => string.Empty,
            },
            EditorOutlinerColumn.Socket => target switch
            {
                Actor actor => GetActorSocketLabel(actor),
                SceneComponent component => component.AttachSocketName ?? string.Empty,
                _ => string.Empty,
            },
            EditorOutlinerColumn.Id => target switch
            {
                Actor actor => actor.ActorGuid.ToString(),
                EditorActorFolder folder => folder.FolderGuid.ToString(),
                ActorComponent component => component.ComponentGuid.ToString(),
                _ => string.Empty,
            },
            _ => string.Empty,
        };

    private void SortChildren(WorldTreeItem item)
    {
        foreach (var child in item.SubItems.OfType<WorldTreeItem>())
            SortChildren(child);
        item.SubItems.Sort((left, right) => CompareItems((WorldTreeItem)left, (WorldTreeItem)right));
    }

    private int CompareItems(WorldTreeItem left, WorldTreeItem right)
    {
        var category = GetSortCategory(left.Target).CompareTo(GetSortCategory(right.Target));
        if (category != 0)
            return category;
        var comparison = CompareColumnValues(left.Target, right.Target, _viewState.SortColumnId);
        if (!_viewState.SortAscending)
            comparison = -comparison;
        return comparison != 0
            ? comparison
            : StringComparer.Ordinal.Compare(GetStableSelectionId(left.Target), GetStableSelectionId(right.Target));
    }

    private int CompareColumnValues(object left, object right, string columnId)
    {
        var column = _extensions.FindColumn(columnId) ?? _extensions.FindColumn(EditorOutlinerColumnIds.Label)!;
        try
        {
            var leftKey = column.GetSortKey(left);
            var rightKey = column.GetSortKey(right);
            if (leftKey == null) return rightKey == null ? 0 : -1;
            if (rightKey == null) return 1;
            if (leftKey.GetType() == rightKey.GetType())
                return leftKey.CompareTo(rightKey);
            return StringComparer.OrdinalIgnoreCase.Compare(
                Convert.ToString(leftKey, System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToString(rightKey, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch
        {
            return 0;
        }
    }

    private static int GetSortCategory(object target)
        => target is EditorActorFolder ? 0 : target is Actor ? 1 :
            target is EditorUnloadedActorDescriptor ? 2 : 3;

    private void PruneItemCache()
    {
        var valid = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var actor in _world.EnumerateActors(includePendingActors: true))
        {
            valid.Add(actor);
            foreach (var component in actor.Components)
                valid.Add(component);
        }
        foreach (var folder in _outliner.Folders)
            valid.Add(folder);
        foreach (var target in _itemCache.Keys.Where(target => !valid.Contains(target)).ToArray())
            _itemCache.Remove(target);
    }

    private void PruneExtensionItemCache(IEnumerable<string> validIds)
    {
        var valid = validIds.ToHashSet(StringComparer.Ordinal);
        foreach (var id in _extensionItemCache.Keys.Where(id => !valid.Contains(id)).ToArray())
            _extensionItemCache.Remove(id);
    }

    private Vector4 GetFolderIconColor(EditorActorFolder folder)
        => _outliner.CurrentFolderGuid == folder.FolderGuid
            ? new Vector4(1f, 0.78f, 0.24f, 1f)
            : new Vector4(0.82f, 0.62f, 0.22f, 1f);

    private UITreeItemVisibilityState GetVisibilityState(object target)
    {
        var state = target switch
        {
            Actor actor => _outliner.IsActorTemporarilyHidden(actor.ActorGuid)
                ? EditorVisibilityState.Hidden : EditorVisibilityState.Visible,
            EditorActorFolder folder => _outliner.GetFolderVisibility(
                folder.FolderGuid, _world.EnumerateActors(includePendingActors: true)),
            _ => EditorVisibilityState.Visible,
        };
        return state switch
        {
            EditorVisibilityState.Hidden => UITreeItemVisibilityState.Hidden,
            EditorVisibilityState.Mixed => UITreeItemVisibilityState.Mixed,
            _ => UITreeItemVisibilityState.Visible,
        };
    }

    private void InvalidateFilter() => _viewRevision++;

    public void InvalidateView() => InvalidateFilter();

    private static WorldTreeItem? FindItem(IReadOnlyList<UITreeViewItem> items, object target)
    {
        foreach (var item in items)
        {
            if (item is WorldTreeItem worldItem && ReferenceEquals(worldItem.Target, target))
                return worldItem;
            if (FindItem(item.SubItems, target) is { } sub)
                return sub;
        }
        return null;
    }

    private static string GetComponentLabel(ActorComponent component)
    {
        if (component is not SceneComponent { AttachParent: { } parent } scene)
            return component.GetType().Name;
        var parentOwner = parent.Owner;
        var parentName = parentOwner == null
            ? parent.GetType().Name
            : string.IsNullOrWhiteSpace(parentOwner.Name) ? parentOwner.GetType().Name : parentOwner.Name;
        var socket = scene.AttachSocketName == null ? string.Empty : $":{scene.AttachSocketName}";
        return $"{component.GetType().Name} -> {parentName}/{parent.GetType().Name}{socket}";
    }

    private static Actor? GetOutlinerParent(Actor actor, IReadOnlySet<Actor> candidates)
    {
        var parent = actor.RootComponent?.AttachParent?.Owner;
        return parent != null && !ReferenceEquals(parent, actor) && candidates.Contains(parent)
            ? parent
            : null;
    }

    private static string GetActorTypeLabel(Actor actor)
    {
        if (actor.GetType() != typeof(Actor))
            return actor.GetType().Name;
        var componentType = actor.RootComponent?.GetType().Name;
        return componentType?.EndsWith("Component", StringComparison.Ordinal) == true
            ? componentType[..^"Component".Length]
            : componentType ?? nameof(Actor);
    }

    private static Vector4 GetActorIconColor(Actor actor, bool selectable)
    {
        if (!selectable)
            return UITheme.Default.TextDimColor;
        return actor.RootComponent switch
        {
            CameraComponent => new Vector4(0.35f, 0.65f, 1f, 1f),
            LightComponent => new Vector4(1f, 0.78f, 0.2f, 1f),
            StaticMeshComponent or SkeletalMeshComponent => new Vector4(0.3f, 0.75f, 0.58f, 1f),
            _ => new Vector4(0.62f, 0.65f, 0.7f, 1f),
        };
    }

    private void CaptureExpansionState(IEnumerable<UITreeViewItem> items)
    {
        foreach (var item in items)
        {
            if (item is WorldTreeItem { Target: Actor actor })
                GetActorExpansion()[actor.ActorGuid] = item.IsExpanded;
            else if (item is WorldTreeItem { Target: EditorActorFolder folder })
                _viewState.FolderExpansion[folder.FolderGuid] = item.IsExpanded;
            CaptureExpansionState(item.SubItems);
        }
    }

    private void CaptureViewState()
    {
        if (_suppressViewStateChanged)
            return;
        if (!_displayingFilteredTree)
        {
            CaptureExpansionState(_tree.Roots);
            SetStoredScrollOffset(_tree.ScrollOffset);
        }
        ViewStateChanged?.Invoke();
    }

    private Dictionary<Guid, bool> GetActorExpansion()
        => IsRuntimeView ? _viewState.RuntimeActorExpansion : _viewState.ActorExpansion;

    private Vector2 GetStoredScrollOffset()
        => IsRuntimeView
            ? new Vector2(_viewState.RuntimeScrollOffsetX, _viewState.RuntimeScrollOffsetY)
            : new Vector2(_viewState.ScrollOffsetX, _viewState.ScrollOffsetY);

    private void SetStoredScrollOffset(Vector2 value)
    {
        if (IsRuntimeView)
        {
            _viewState.RuntimeScrollOffsetX = value.X;
            _viewState.RuntimeScrollOffsetY = value.Y;
        }
        else
        {
            _viewState.ScrollOffsetX = value.X;
            _viewState.ScrollOffsetY = value.Y;
        }
    }

    public void SelectTarget(object? target)
    {
        if (target == null)
        {
            _tree.SelectItem(null);
            return;
        }

        if (FindItem(_tree.Roots, target) is { } item)
            _tree.SelectItem(item);
    }

    public void SelectTargets(IEnumerable<object> targets, object? primary = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var nextTargets = targets.Distinct().ToArray();
        var nextPrimary = primary != null && nextTargets.Any(target => ReferenceEquals(target, primary))
            ? primary
            : nextTargets.LastOrDefault();
        var selectionChanged = !ReferenceEquals(_primaryTarget, nextPrimary) ||
            !SequenceEqualByReference(_selectedTargets, nextTargets);
        _selectedTargets = nextTargets;
        _primaryTarget = nextPrimary;
        if (OnlySelected && selectionChanged && !_suppressSelectionChanged)
        {
            InvalidateFilter();
            Refresh();
        }
        var displayTargets = _selectedTargets
            .Select(GetDisplaySelectionTarget)
            .Where(target => target != null)
            .Cast<object>()
            .Distinct()
            .ToArray();
        var items = displayTargets
            .Select(target => FindItem(_tree.Roots, target))
            .Where(item => item != null)
            .Cast<WorldTreeItem>()
            .ToArray();
        var displayPrimary = GetDisplaySelectionTarget(_primaryTarget);
        var primaryItem = displayPrimary == null ? null : FindItem(_tree.Roots, displayPrimary);
        if (selectionChanged && primaryItem != null && ExpandAncestors(primaryItem))
            _tree.RebuildFlatList();

        var wasSuppressed = _suppressSelectionChanged;
        _suppressSelectionChanged = true;
        try
        {
            _tree.SelectItems(items, primaryItem);
        }
        finally
        {
            _suppressSelectionChanged = wasSuppressed;
        }
    }

    private object? GetDisplaySelectionTarget(object? target)
        => !ShowComponents && target is ActorComponent component ? component.Owner : target;

    public bool BeginRename(object target)
    {
        if (IsReadOnly || target is not Actor && target is not EditorActorFolder)
            return false;
        var item = FindItem(_tree.Roots, target);
        return item != null && item.BeginInlineEdit(value => RenameSubmitted?.Invoke(target, value) ?? true);
    }

    public object? GetTargetAt(Vector2 position) => (_tree.GetItemAt(position) as WorldTreeItem)?.Target;

    private static bool ExpandAncestors(UITreeViewItem item)
    {
        var changed = false;
        for (var parent = item.LogicalParent; parent != null; parent = parent.LogicalParent)
        {
            if (parent.IsExpanded)
                continue;
            parent.IsExpanded = true;
            changed = true;
        }
        return changed;
    }

    private static bool SequenceEqualByReference(IReadOnlyList<object> left, IReadOnlyList<object> right)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (!ReferenceEquals(left[index], right[index]))
                return false;
        }
        return true;
    }

    /// <summary>持有引擎对象引用的树项（选中回调向上抛 Target）。</summary>
    public sealed class WorldTreeItem : UITreeViewItem
    {
        /// <summary>绑定的 Folder、Actor 或 Component。</summary>
        public object Target { get; }

        public WorldTreeItem(object target, string text) : base(text)
        {
            Target = target;
        }
    }
}
