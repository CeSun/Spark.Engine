using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>Outliner 内置列的稳定标识；扩展列应使用带命名空间的自有标识。</summary>
public static class EditorOutlinerColumnIds
{
    public const string Label = "label";
    public const string Type = "type";
    public const string Socket = "socket";
    public const string Id = "id";
    public const string Level = "level";
    public const string DataLayer = "data-layer";
}

public static class EditorOutlinerNodeIds
{
    public static string Actor(Guid actorGuid) => $"actor:{actorGuid:N}";
    public static string Folder(Guid folderGuid) => $"folder:{folderGuid:N}";
}

/// <summary>可注册的 Outliner 信息列。</summary>
public sealed class EditorOutlinerColumnDescriptor
{
    public string Id { get; }
    public string DisplayName { get; }
    public bool DefaultVisible { get; }
    public float DefaultWidth { get; }
    public bool Searchable { get; }
    public Func<object, string> GetText { get; }
    public Func<object, IComparable?> GetSortKey { get; }

    public EditorOutlinerColumnDescriptor(string id, string displayName,
        Func<object, string> getText, bool defaultVisible = false, float defaultWidth = 90f,
        bool searchable = true, Func<object, IComparable?>? getSortKey = null)
    {
        Id = ValidateId(id);
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Column display name cannot be empty.", nameof(displayName))
            : displayName.Trim();
        GetText = getText ?? throw new ArgumentNullException(nameof(getText));
        DefaultVisible = defaultVisible;
        DefaultWidth = float.IsFinite(defaultWidth)
            ? System.Math.Clamp(defaultWidth, 48f, 320f)
            : throw new ArgumentOutOfRangeException(nameof(defaultWidth));
        Searchable = searchable;
        GetSortKey = getSortKey ?? (target => GetText(target));
    }

    internal static string ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Outliner extension ID cannot be empty.", nameof(id));
        var normalized = id.Trim().ToLowerInvariant();
        if (normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw new ArgumentException("Outliner extension IDs may only contain letters, digits, '.', '-' and '_'.", nameof(id));
        return normalized;
    }
}

/// <summary>可注册且能由每个 Outliner 实例独立启用的过滤条件。</summary>
public sealed class EditorOutlinerFilterDescriptor
{
    public string Id { get; }
    public string DisplayName { get; }
    public Func<object, bool> Predicate { get; }

    public EditorOutlinerFilterDescriptor(string id, string displayName, Func<object, bool> predicate)
    {
        Id = EditorOutlinerColumnDescriptor.ValidateId(id);
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Filter display name cannot be empty.", nameof(displayName))
            : displayName.Trim();
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }
}

/// <summary>扩展上下文菜单执行时的只读快照。</summary>
public sealed record EditorOutlinerContext(
    object? Target,
    IReadOnlyList<object> Selection,
    World World,
    bool IsReadOnly);

public sealed record EditorOutlinerNodeContext(World World, EditorWorldOutlinerData Outliner);

/// <summary>扩展节点的稳定描述；ParentId 为空时作为根节点显示。</summary>
public sealed class EditorOutlinerNodeDescriptor
{
    public string StableId { get; }
    public object Target { get; }
    public string? ParentId { get; }
    public string Label { get; }
    public bool IsSelectable { get; }

    public EditorOutlinerNodeDescriptor(string stableId, object target, string label,
        string? parentId = null, bool isSelectable = false)
    {
        StableId = string.IsNullOrWhiteSpace(stableId)
            ? throw new ArgumentException("Outliner node ID cannot be empty.", nameof(stableId))
            : stableId.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Label = string.IsNullOrWhiteSpace(label)
            ? throw new ArgumentException("Outliner node label cannot be empty.", nameof(label))
            : label.Trim();
        ParentId = string.IsNullOrWhiteSpace(parentId) ? null : parentId.Trim();
        IsSelectable = isSelectable;
    }
}

/// <summary>按 World 提供扩展节点；Revision 变化时 Outliner 才重新查询节点。</summary>
public sealed class EditorOutlinerNodeProviderDescriptor
{
    public string Id { get; }
    public Func<EditorOutlinerNodeContext, IEnumerable<EditorOutlinerNodeDescriptor>> GetNodes { get; }
    public Func<EditorOutlinerNodeContext, long> GetRevision { get; }

    public EditorOutlinerNodeProviderDescriptor(string id,
        Func<EditorOutlinerNodeContext, IEnumerable<EditorOutlinerNodeDescriptor>> getNodes,
        Func<EditorOutlinerNodeContext, long>? getRevision = null)
    {
        Id = EditorOutlinerColumnDescriptor.ValidateId(id);
        GetNodes = getNodes ?? throw new ArgumentNullException(nameof(getNodes));
        GetRevision = getRevision ?? (context => context.Outliner.Revision);
    }
}

/// <summary>可注册的 Outliner 上下文菜单动作。</summary>
public sealed class EditorOutlinerContextActionDescriptor
{
    public string Id { get; }
    public string Label { get; }
    public bool MutatesWorld { get; }
    public Func<EditorOutlinerContext, bool> CanExecute { get; }
    public Action<EditorOutlinerContext> Execute { get; }

    public EditorOutlinerContextActionDescriptor(string id, string label,
        Action<EditorOutlinerContext> execute, bool mutatesWorld = true,
        Func<EditorOutlinerContext, bool>? canExecute = null)
    {
        Id = EditorOutlinerColumnDescriptor.ValidateId(id);
        Label = string.IsNullOrWhiteSpace(label)
            ? throw new ArgumentException("Context action label cannot be empty.", nameof(label))
            : label.Trim();
        Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        MutatesWorld = mutatesWorld;
        CanExecute = canExecute ?? (_ => true);
    }
}

/// <summary>
/// 编辑器实例独占的 Outliner 扩展注册表。注册项以稳定 ID 去重，UI 无需引用插件的具体 Actor 类型。
/// </summary>
public sealed class EditorOutlinerExtensionRegistry
{
    private readonly Dictionary<string, EditorOutlinerColumnDescriptor> _columns =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EditorOutlinerFilterDescriptor> _filters =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EditorOutlinerContextActionDescriptor> _contextActions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EditorOutlinerNodeProviderDescriptor> _nodeProviders =
        new(StringComparer.OrdinalIgnoreCase);

    public EditorOutlinerExtensionRegistry()
    {
        RegisterBuiltInColumns();
        RegisterNodeProvider(new EditorOutlinerNodeProviderDescriptor("spark.unloaded-actors",
            context => context.Outliner.UnloadedActors.Select(actor => new EditorOutlinerNodeDescriptor(
                $"unloaded:{actor.ActorGuid:N}", actor, actor.Label)),
            context => context.Outliner.Revision));
    }

    public IReadOnlyList<EditorOutlinerColumnDescriptor> Columns => _columns.Values.ToArray();
    public IReadOnlyList<EditorOutlinerFilterDescriptor> Filters => _filters.Values.ToArray();
    public IReadOnlyList<EditorOutlinerContextActionDescriptor> ContextActions => _contextActions.Values.ToArray();
    public IReadOnlyList<EditorOutlinerNodeProviderDescriptor> NodeProviders => _nodeProviders.Values.ToArray();
    public long Revision { get; private set; }

    public void RegisterColumn(EditorOutlinerColumnDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!_columns.TryAdd(descriptor.Id, descriptor))
            throw new InvalidOperationException($"An Outliner column named '{descriptor.Id}' is already registered.");
        Revision++;
    }

    public bool UnregisterColumn(string id)
    {
        var normalized = EditorOutlinerColumnDescriptor.ValidateId(id);
        if (normalized is EditorOutlinerColumnIds.Label or EditorOutlinerColumnIds.Type or
            EditorOutlinerColumnIds.Socket or EditorOutlinerColumnIds.Id or
            EditorOutlinerColumnIds.Level or EditorOutlinerColumnIds.DataLayer)
            return false;
        var removed = _columns.Remove(normalized);
        if (removed) Revision++;
        return removed;
    }

    public void RegisterFilter(EditorOutlinerFilterDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!_filters.TryAdd(descriptor.Id, descriptor))
            throw new InvalidOperationException($"An Outliner filter named '{descriptor.Id}' is already registered.");
        Revision++;
    }

    public bool UnregisterFilter(string id)
    {
        var removed = _filters.Remove(EditorOutlinerColumnDescriptor.ValidateId(id));
        if (removed) Revision++;
        return removed;
    }

    public void RegisterContextAction(EditorOutlinerContextActionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!_contextActions.TryAdd(descriptor.Id, descriptor))
            throw new InvalidOperationException($"An Outliner context action named '{descriptor.Id}' is already registered.");
        Revision++;
    }

    public bool UnregisterContextAction(string id)
    {
        var removed = _contextActions.Remove(EditorOutlinerColumnDescriptor.ValidateId(id));
        if (removed) Revision++;
        return removed;
    }

    public void RegisterNodeProvider(EditorOutlinerNodeProviderDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!_nodeProviders.TryAdd(descriptor.Id, descriptor))
            throw new InvalidOperationException($"An Outliner node provider named '{descriptor.Id}' is already registered.");
        Revision++;
    }

    public bool UnregisterNodeProvider(string id)
    {
        var removed = _nodeProviders.Remove(EditorOutlinerColumnDescriptor.ValidateId(id));
        if (removed) Revision++;
        return removed;
    }

    public EditorOutlinerColumnDescriptor? FindColumn(string id)
        => _columns.GetValueOrDefault(id);

    private void RegisterBuiltInColumns()
    {
        RegisterColumn(new EditorOutlinerColumnDescriptor(EditorOutlinerColumnIds.Label, "Label",
            target => target switch
            {
                Actor actor => string.IsNullOrWhiteSpace(actor.Name) ? actor.GetType().Name : actor.Name,
                EditorActorFolder folder => folder.Name,
                ActorComponent component => component.GetType().Name,
                EditorUnloadedActorDescriptor actor => actor.Label,
                _ => string.Empty,
            }, defaultVisible: true, defaultWidth: 180f));
        RegisterColumn(new EditorOutlinerColumnDescriptor(EditorOutlinerColumnIds.Type, "Type",
            target => target switch
            {
                Actor actor when actor.GetType() != typeof(Actor) => actor.GetType().Name,
                Actor actor => TrimComponentSuffix(actor.RootComponent?.GetType().Name) ?? nameof(Actor),
                EditorActorFolder => "Folder",
                ActorComponent component => component.GetType().Name,
                EditorUnloadedActorDescriptor actor => actor.ActorType,
                _ => string.Empty,
            }, defaultVisible: true, defaultWidth: 92f));
        RegisterColumn(new EditorOutlinerColumnDescriptor(EditorOutlinerColumnIds.Socket, "Socket",
            target => target switch
            {
                Actor { RootComponent.AttachParent: { } parent } actor =>
                    actor.RootComponent!.AttachSocketName is { } socket
                        ? $"{parent.GetType().Name}:{socket}"
                        : parent.GetType().Name,
                SceneComponent component => component.AttachSocketName ?? string.Empty,
                _ => string.Empty,
            }, defaultWidth: 90f));
        RegisterColumn(new EditorOutlinerColumnDescriptor(EditorOutlinerColumnIds.Id, "ID",
            target => target switch
            {
                Actor actor => actor.ActorGuid.ToString(),
                EditorActorFolder folder => folder.FolderGuid.ToString(),
                ActorComponent component => component.ComponentGuid.ToString(),
                EditorUnloadedActorDescriptor actor => actor.ActorGuid.ToString(),
                _ => string.Empty,
            }, defaultWidth: 88f));
        RegisterColumn(new EditorOutlinerColumnDescriptor(EditorOutlinerColumnIds.Level, "Level",
            target => target is Actor { World: { } world } actor
                ? EditorWorldOutlinerData.For(world).GetActorLevelName(actor.ActorGuid)
                : string.Empty,
            defaultWidth: 110f));
        RegisterColumn(new EditorOutlinerColumnDescriptor(EditorOutlinerColumnIds.DataLayer, "Data Layer",
            target => target is Actor { World: { } world } actor
                ? EditorWorldOutlinerData.For(world).GetActorDataLayerNames(actor.ActorGuid)
                : string.Empty,
            defaultWidth: 120f));
    }

    private static string? TrimComponentSuffix(string? value)
        => value?.EndsWith("Component", StringComparison.Ordinal) == true
            ? value[..^"Component".Length]
            : value;
}
