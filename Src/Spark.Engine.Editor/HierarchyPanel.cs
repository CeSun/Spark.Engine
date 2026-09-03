using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.UI;
using Spark.Engine.Worlds;
using System.Numerics;

namespace Spark.Engine.Editor;

/// <summary>
/// UE 风格场景大纲：默认显示 Actor 及跨 Actor 的空间挂载层级；Component 仅作为可选开发者视图。
/// 结构变化（Actor/Component 增删）时签名比对后重建，并按稳定 Guid 保留展开和选择状态。
/// </summary>
public sealed class HierarchyPanel
{
    private World _world;
    private readonly UITreeView _tree;

    private string _lastSignature = string.Empty;
    private bool _suppressSelectionChanged;
    private readonly Dictionary<Guid, bool> _actorExpansion = [];
    private bool _displayingFilteredTree;
    private IReadOnlyList<object> _selectedTargets = Array.Empty<object>();
    private object? _primaryTarget;
    private string _searchText = string.Empty;
    private bool _showInternalActors;
    private bool _showComponents;
    private bool _onlySelected;

    /// <summary>选中项变化：参数为选中的 Actor/Component 或 null。</summary>
    public Action<object?>? SelectionChanged { get; set; }

    /// <summary>选择集合变化；第二个参数为最后操作的主选目标。</summary>
    public Action<IReadOnlyList<object>, object?>? SelectionSetChanged { get; set; }

    /// <summary>层级项拖放完成；参数为源目标、放置目标和画布位置。</summary>
    public Action<object, object, System.Numerics.Vector2>? ItemDropped { get; set; }

    public HierarchyPanel(World world)
    {
        _world = world;
        _tree = new UITreeView
        {
            BackgroundColor = new(0f, 0f, 0f, 0f),
            AllowMultipleSelection = true,
        };
        _tree.FixedSize = new UISize(0f, 0f); // ≤0 = 拉伸填满（否则高度只有一行 ItemHeight）
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
            if (((WorldTreeItem)source).Target is Actor sourceActor &&
                ((WorldTreeItem)target).Target is Actor targetActor)
                ItemDropped?.Invoke(sourceActor, targetActor, position);
        };
    }

    /// <summary>树控件本身（挂进编辑器布局）。</summary>
    public UIElement Element => _tree;

    public string SearchText
    {
        get => _searchText;
        set
        {
            var next = value?.Trim() ?? string.Empty;
            if (string.Equals(_searchText, next, StringComparison.Ordinal))
                return;
            _searchText = next;
            InvalidateFilter();
        }
    }

    public bool ShowInternalActors
    {
        get => _showInternalActors;
        set
        {
            if (_showInternalActors == value)
                return;
            _showInternalActors = value;
            InvalidateFilter();
        }
    }

    public bool ShowComponents
    {
        get => _showComponents;
        set
        {
            if (_showComponents == value)
                return;
            _showComponents = value;
            InvalidateFilter();
        }
    }

    public bool OnlySelected
    {
        get => _onlySelected;
        set
        {
            if (_onlySelected == value)
                return;
            _onlySelected = value;
            InvalidateFilter();
        }
    }

    public void SetWorld(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _selectedTargets = Array.Empty<object>();
        _primaryTarget = null;
        _actorExpansion.Clear();
        _displayingFilteredTree = false;
        _lastSignature = string.Empty;
        _tree.Clear();
    }

    /// <summary>当前选中的目标（Actor 或 Component；无选中为 null）。</summary>
    public object? SelectedTarget => _primaryTarget;

    public IReadOnlyList<object> SelectedTargets => _selectedTargets;

    /// <summary>每帧调用：结构签名变化时重建树（O(n) 快速比对，展开/选中态跨重建保留）。</summary>
    public void Refresh()
    {
        var signature = BuildSignature();
        if (signature == _lastSignature)
            return;

        _lastSignature = signature;
        Rebuild();
    }

    private string BuildSignature()
    {
        // 内容无关的结构指纹：Actor 引用序列 + 每个 Actor 的组件引用序列
        // （World.Update 后 Add/Remove 已生效；组件只增不减，引用序列足够判断结构变化）
        var sb = new System.Text.StringBuilder();
        sb.Append(_searchText).Append('|')
            .Append(_showInternalActors).Append('|')
            .Append(_showComponents).Append('|')
            .Append(_onlySelected).Append('|');
        if (_onlySelected)
        {
            foreach (var target in _selectedTargets)
                sb.Append(GetStableSelectionId(target)).Append(',');
            sb.Append('|');
        }
        var visibleActors = GetVisibleActors();
        var visibleSet = visibleActors.ToHashSet();
        foreach (var actor in visibleActors)
        {
            sb.Append(actor.ActorGuid).Append(':').Append(actor.Name)
                .Append('#').Append(actor.GetType().FullName)
                .Append('/').Append(actor.RootComponent?.ComponentGuid)
                .Append('/').Append(actor.RootComponent?.GetType().FullName)
                .Append('>').Append(GetOutlinerParent(actor, visibleSet)?.ActorGuid)
                .Append(';');
            foreach (var component in VisibleComponents(actor))
            {
                sb.Append(component.ComponentGuid).Append(':').Append(component.GetType().Name);
                if (component is SceneComponent scene)
                {
                    sb.Append('>').Append(scene.AttachParent?.ComponentGuid)
                        .Append('@').Append(scene.AttachSocketName);
                }
                sb.Append(',');
            }
        }
        return sb.ToString();
    }

    private void Rebuild()
    {
        var selected = _selectedTargets;
        var primary = _primaryTarget;
        var scrollOffset = _tree.ScrollOffset;
        if (!_displayingFilteredTree)
            CaptureExpansionState(_tree.Roots);
        var isContextFilter = _searchText.Length != 0 || _onlySelected;

        _suppressSelectionChanged = true;
        try
        {
            _tree.Clear();
            var actors = GetVisibleActors();
            var actorSet = actors.ToHashSet();
            var actorItems = new Dictionary<Actor, WorldTreeItem>();
            foreach (var actor in actors)
            {
                var displayName = string.IsNullOrWhiteSpace(actor.Name) ? actor.GetType().Name : actor.Name;
                var actorItem = CreateTreeItem(actor, displayName);
                actorItem.IsExpanded = !_actorExpansion.TryGetValue(actor.ActorGuid, out var expanded) || expanded;
                foreach (var component in VisibleComponents(actor))
                    actorItem.AddSubItem(CreateTreeItem(component, GetComponentLabel(component)));
                actorItems.Add(actor, actorItem);
            }

            foreach (var actor in actors)
            {
                var actorItem = actorItems[actor];
                var parent = GetOutlinerParent(actor, actorSet);
                if (parent != null)
                    actorItems[parent].AddSubItem(actorItem);
                else
                    _tree.AddRoot(actorItem);
            }

            if (isContextFilter)
            {
                foreach (var item in actorItems.Values.Where(item => !item.IsLeaf))
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
        }
    }

    private IReadOnlyList<Actor> GetVisibleActors()
    {
        var candidates = _world.Actors
            .Where(actor => _showInternalActors || EditorActorPolicy.IsVisibleInOutliner(actor))
            .ToArray();
        var candidateSet = candidates.ToHashSet();
        var directlyVisible = candidates
            .Where(actor => !_onlySelected || IsActorSelected(actor))
            .Where(MatchesSearch)
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

    private IEnumerable<ActorComponent> VisibleComponents(Actor actor)
    {
        if (!_showComponents)
            return Array.Empty<ActorComponent>();
        var components = actor.Components;
        if (_searchText.Length == 0 || MatchesActor(actor))
            return components;
        return components.Where(component => MatchesText(component.GetType().Name));
    }

    private bool MatchesSearch(Actor actor)
        => _searchText.Length == 0 || MatchesActor(actor) ||
           actor.Components.Any(component => MatchesText(component.GetType().Name));

    private bool MatchesActor(Actor actor)
        => MatchesText(actor.Name) || MatchesText(actor.GetType().Name) || MatchesText(GetActorTypeLabel(actor));

    private bool MatchesText(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(_searchText, StringComparison.OrdinalIgnoreCase);

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
            _ => target.GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    private static WorldTreeItem CreateTreeItem(object target, string text)
    {
        var selectable = EditorActorPolicy.CanSelect(target);
        var editable = EditorActorPolicy.CanEdit(target);
        var isActor = target is Actor;
        var isSpatialActor = target is Actor { RootComponent: not null };
        return new WorldTreeItem(target, text)
        {
            IsSelectable = selectable,
            IsDraggable = isSpatialActor && editable,
            IsDropTarget = isSpatialActor && editable,
            Focusable = selectable,
            TextColor = selectable ? UITheme.Default.TextColor : UITheme.Default.TextDimColor,
            IconColor = isActor ? GetActorIconColor((Actor)target, selectable) : null,
        };
    }

    private void InvalidateFilter() => _lastSignature = string.Empty;

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
                _actorExpansion[actor.ActorGuid] = item.IsExpanded;
            CaptureExpansionState(item.SubItems);
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
        if (_onlySelected && selectionChanged && !_suppressSelectionChanged)
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
        => !_showComponents && target is ActorComponent component ? component.Owner : target;

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
        /// <summary>绑定的 Actor 或 Component。</summary>
        public object Target { get; }

        public WorldTreeItem(object target, string text) : base(text)
        {
            Target = target;
        }
    }
}
