using System.Runtime.CompilerServices;
using Spark.Engine.Actors;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>场景大纲中的稳定编辑器 Folder；它不是 Actor，也不参与运行时生命周期。</summary>
public sealed class EditorActorFolder
{
    public Guid FolderGuid { get; }
    public Guid? ParentFolderGuid { get; internal set; }
    public string Name { get; internal set; }

    public EditorActorFolder(Guid folderGuid, Guid? parentFolderGuid, string name)
    {
        FolderGuid = folderGuid;
        ParentFolderGuid = parentFolderGuid;
        Name = name;
    }
}

/// <summary>编辑器 Level 元数据；Guid 为空的 Actor 隐式属于 Persistent Level。</summary>
public sealed record EditorWorldLevel(Guid LevelGuid, string Name);

/// <summary>编辑器 Data Layer 元数据；Actor 可以同时属于多个 Data Layer。</summary>
public sealed record EditorWorldDataLayer(Guid DataLayerGuid, string Name);

/// <summary>未加载 Actor 的轻量描述；它没有可编辑 Actor 实例，也不进入 RuntimeWorld。</summary>
public sealed record EditorUnloadedActorDescriptor(
    Guid ActorGuid,
    string Label,
    string ActorType,
    Guid? LevelGuid,
    IReadOnlyList<Guid> DataLayerGuids);

public enum EditorVisibilityState
{
    Visible,
    Hidden,
    Mixed,
}

/// <summary>
/// 与一个 EditorWorld 绑定的 Outliner 数据。Folder 和 Actor 归属会进入 SceneDocument；
/// 当前 Folder、Eye 临时隐藏状态只在当前编辑器会话中生效。
/// </summary>
public sealed class EditorWorldOutlinerData
{
    private static readonly ConditionalWeakTable<World, EditorWorldOutlinerData> WorldData = new();
    private readonly World _world;
    private readonly List<EditorActorFolder> _folders = [];
    private readonly Dictionary<Guid, Guid> _actorFolders = [];
    private readonly HashSet<Guid> _temporarilyHiddenActors = [];
    private readonly List<EditorWorldLevel> _levels = [];
    private readonly List<EditorWorldDataLayer> _dataLayers = [];
    private readonly List<EditorUnloadedActorDescriptor> _unloadedActors = [];
    private readonly Dictionary<Guid, Guid> _actorLevels = [];
    private readonly Dictionary<Guid, HashSet<Guid>> _actorDataLayers = [];

    public static EditorWorldOutlinerData For(World world)
        => WorldData.GetValue(world ?? throw new ArgumentNullException(nameof(world)), key => new(key));

    private EditorWorldOutlinerData(World world) => _world = world;

    public IReadOnlyList<EditorActorFolder> Folders => _folders;
    public IReadOnlyList<EditorWorldLevel> Levels => _levels;
    public IReadOnlyList<EditorWorldDataLayer> DataLayers => _dataLayers;
    public IReadOnlyList<EditorUnloadedActorDescriptor> UnloadedActors => _unloadedActors;
    public Guid? CurrentFolderGuid { get; private set; }
    public long Revision { get; private set; }
    public event Action? Changed;

    public EditorActorFolder? FindFolder(Guid folderGuid)
        => _folders.FirstOrDefault(folder => folder.FolderGuid == folderGuid);

    public Guid? GetActorFolder(Guid actorGuid)
        => _actorFolders.TryGetValue(actorGuid, out var folderGuid) && FindFolder(folderGuid) != null
            ? folderGuid
            : null;

    public Guid? GetActorLevel(Guid actorGuid)
        => _actorLevels.TryGetValue(actorGuid, out var levelGuid) &&
           _levels.Any(level => level.LevelGuid == levelGuid) ? levelGuid : null;

    public IReadOnlyList<Guid> GetActorDataLayers(Guid actorGuid)
        => _actorDataLayers.TryGetValue(actorGuid, out var layers)
            ? layers.OrderBy(value => value).ToArray()
            : Array.Empty<Guid>();

    public string GetActorLevelName(Guid actorGuid)
        => GetActorLevel(actorGuid) is { } levelGuid
            ? _levels.First(level => level.LevelGuid == levelGuid).Name
            : "Persistent Level";

    public string GetActorDataLayerNames(Guid actorGuid)
        => string.Join(", ", GetActorDataLayers(actorGuid)
            .Select(id => _dataLayers.FirstOrDefault(layer => layer.DataLayerGuid == id)?.Name)
            .Where(name => name != null));

    public bool IsActorTemporarilyHidden(Guid actorGuid) => _temporarilyHiddenActors.Contains(actorGuid);

    public void SetCurrentFolder(Guid? folderGuid)
    {
        var next = folderGuid.HasValue && FindFolder(folderGuid.Value) != null ? folderGuid : null;
        if (CurrentFolderGuid == next)
            return;
        CurrentFolderGuid = next;
        NotifyChanged();
    }

    public void SetActorTemporarilyHidden(Guid actorGuid, bool hidden)
    {
        var changed = hidden
            ? _temporarilyHiddenActors.Add(actorGuid)
            : _temporarilyHiddenActors.Remove(actorGuid);
        if (changed)
        {
            ApplyActorPreviewVisibility(actorGuid, hidden);
            NotifyChanged();
        }
    }

    public EditorVisibilityState GetFolderVisibility(Guid folderGuid, IEnumerable<Actor> actors)
    {
        var actorGuids = GetActorsInFolderSubtree(folderGuid, actors).Select(actor => actor.ActorGuid).ToArray();
        if (actorGuids.Length == 0)
            return EditorVisibilityState.Visible;
        var hiddenCount = actorGuids.Count(_temporarilyHiddenActors.Contains);
        return hiddenCount switch
        {
            0 => EditorVisibilityState.Visible,
            var count when count == actorGuids.Length => EditorVisibilityState.Hidden,
            _ => EditorVisibilityState.Mixed,
        };
    }

    public IReadOnlyList<Actor> GetActorsInFolderSubtree(Guid folderGuid, IEnumerable<Actor> actors)
        => EnumerateActorsInFolderSubtree(folderGuid, actors).ToArray();

    public void SetFolderTemporarilyHidden(Guid folderGuid, IEnumerable<Actor> actors, bool hidden)
    {
        var changed = false;
        foreach (var actor in EnumerateActorsInFolderSubtree(folderGuid, actors))
        {
            var actorChanged = hidden
                ? _temporarilyHiddenActors.Add(actor.ActorGuid)
                : _temporarilyHiddenActors.Remove(actor.ActorGuid);
            changed |= actorChanged;
            if (actorChanged)
                ApplyActorPreviewVisibility(actor.ActorGuid, hidden);
        }
        if (changed)
            NotifyChanged();
    }

    internal void AddFolder(EditorActorFolder folder)
    {
        ValidateFolder(folder, replacingFolderGuid: null);
        folder.Name = NormalizeName(folder.Name);
        _folders.Add(folder);
        NotifyChanged();
    }

    internal void RemoveFolder(Guid folderGuid)
    {
        var folder = FindFolder(folderGuid) ?? throw new InvalidOperationException("Folder no longer exists.");
        var parentGuid = folder.ParentFolderGuid;
        for (var index = 0; index < _folders.Count; index++)
        {
            if (_folders[index].ParentFolderGuid == folderGuid)
                _folders[index].ParentFolderGuid = parentGuid;
        }
        foreach (var actorGuid in _actorFolders.Where(pair => pair.Value == folderGuid).Select(pair => pair.Key).ToArray())
        {
            if (parentGuid.HasValue)
                _actorFolders[actorGuid] = parentGuid.Value;
            else
                _actorFolders.Remove(actorGuid);
        }
        _folders.Remove(folder);
        if (CurrentFolderGuid == folderGuid)
            CurrentFolderGuid = parentGuid;
        NotifyChanged();
    }

    internal void RestoreFolderRemoval(EditorActorFolder folder,
        IReadOnlyList<Guid> childFolderGuids, IReadOnlyList<Guid> actorGuids, bool wasCurrent)
    {
        ValidateFolder(folder, replacingFolderGuid: null);
        _folders.Add(folder);
        for (var index = 0; index < _folders.Count; index++)
        {
            if (childFolderGuids.Contains(_folders[index].FolderGuid))
                _folders[index].ParentFolderGuid = folder.FolderGuid;
        }
        foreach (var actorGuid in actorGuids)
            _actorFolders[actorGuid] = folder.FolderGuid;
        if (wasCurrent)
            CurrentFolderGuid = folder.FolderGuid;
        NotifyChanged();
    }

    internal void RenameFolder(Guid folderGuid, string name)
    {
        var index = FindFolderIndex(folderGuid);
        var folder = _folders[index];
        var nextName = NormalizeName(name);
        ValidateFolder(new EditorActorFolder(folder.FolderGuid, folder.ParentFolderGuid, nextName), folderGuid);
        folder.Name = nextName;
        NotifyChanged();
    }

    internal void MoveFolder(Guid folderGuid, Guid? parentFolderGuid)
    {
        var index = FindFolderIndex(folderGuid);
        var folder = _folders[index];
        var next = new EditorActorFolder(folder.FolderGuid, parentFolderGuid, folder.Name);
        ValidateFolder(next, folderGuid);
        for (var ancestor = parentFolderGuid; ancestor.HasValue; ancestor = FindFolder(ancestor.Value)?.ParentFolderGuid)
        {
            if (ancestor.Value == folderGuid)
                throw new InvalidOperationException("A Folder cannot be moved into itself or one of its descendants.");
        }
        folder.ParentFolderGuid = parentFolderGuid;
        NotifyChanged();
    }

    internal void SetActorFolder(Guid actorGuid, Guid? folderGuid)
    {
        if (folderGuid.HasValue && FindFolder(folderGuid.Value) == null)
            throw new InvalidOperationException("Destination Folder no longer exists.");
        if (GetActorFolder(actorGuid) == folderGuid)
            return;
        if (folderGuid.HasValue)
            _actorFolders[actorGuid] = folderGuid.Value;
        else
            _actorFolders.Remove(actorGuid);
        NotifyChanged();
    }

    internal void RestorePersistentData(IEnumerable<EditorActorFolder> folders,
        IEnumerable<(Guid ActorGuid, Guid? FolderGuid)> actorFolders,
        IEnumerable<EditorWorldLevel>? levels = null,
        IEnumerable<EditorWorldDataLayer>? dataLayers = null,
        IEnumerable<(Guid ActorGuid, Guid? LevelGuid, IReadOnlyList<Guid> DataLayerGuids)>? actorOrganization = null,
        IEnumerable<EditorUnloadedActorDescriptor>? unloadedActors = null)
    {
        _folders.Clear();
        _actorFolders.Clear();
        _levels.Clear();
        _dataLayers.Clear();
        _actorLevels.Clear();
        _actorDataLayers.Clear();
        _unloadedActors.Clear();
        foreach (var folder in folders)
        {
            if (folder.FolderGuid == Guid.Empty || string.IsNullOrWhiteSpace(folder.Name) ||
                _folders.Any(existing => existing.FolderGuid == folder.FolderGuid))
                throw new InvalidDataException("Scene contains invalid or duplicate Editor Folder data.");
            _folders.Add(new EditorActorFolder(folder.FolderGuid, folder.ParentFolderGuid, folder.Name.Trim()));
        }
        ValidateRestoredFolders();
        foreach (var (actorGuid, folderGuid) in actorFolders)
        {
            if (folderGuid.HasValue && FindFolder(folderGuid.Value) == null)
                throw new InvalidDataException($"Actor '{actorGuid}' references missing Editor Folder '{folderGuid}'.");
            if (folderGuid.HasValue)
                _actorFolders[actorGuid] = folderGuid.Value;
        }
        RestoreWorldOrganization(levels ?? [], dataLayers ?? [], actorOrganization ?? [], unloadedActors ?? []);
        NotifyChanged();
    }

    private void RestoreWorldOrganization(IEnumerable<EditorWorldLevel> levels,
        IEnumerable<EditorWorldDataLayer> dataLayers,
        IEnumerable<(Guid ActorGuid, Guid? LevelGuid, IReadOnlyList<Guid> DataLayerGuids)> actorOrganization,
        IEnumerable<EditorUnloadedActorDescriptor> unloadedActors)
    {
        foreach (var level in levels)
        {
            if (level.LevelGuid == Guid.Empty || string.IsNullOrWhiteSpace(level.Name) ||
                _levels.Any(existing => existing.LevelGuid == level.LevelGuid))
                throw new InvalidDataException("Scene contains invalid or duplicate Level metadata.");
            _levels.Add(level with { Name = level.Name.Trim() });
        }
        foreach (var layer in dataLayers)
        {
            if (layer.DataLayerGuid == Guid.Empty || string.IsNullOrWhiteSpace(layer.Name) ||
                _dataLayers.Any(existing => existing.DataLayerGuid == layer.DataLayerGuid))
                throw new InvalidDataException("Scene contains invalid or duplicate Data Layer metadata.");
            _dataLayers.Add(layer with { Name = layer.Name.Trim() });
        }
        var levelIds = _levels.Select(level => level.LevelGuid).ToHashSet();
        var layerIds = _dataLayers.Select(layer => layer.DataLayerGuid).ToHashSet();
        foreach (var (actorGuid, levelGuid, actorLayers) in actorOrganization)
        {
            if (levelGuid.HasValue && !levelIds.Contains(levelGuid.Value))
                throw new InvalidDataException($"Actor '{actorGuid}' references missing Level '{levelGuid}'.");
            var normalizedLayers = actorLayers.Distinct().ToHashSet();
            if (!normalizedLayers.IsSubsetOf(layerIds))
                throw new InvalidDataException($"Actor '{actorGuid}' references a missing Data Layer.");
            if (levelGuid.HasValue)
                _actorLevels[actorGuid] = levelGuid.Value;
            if (normalizedLayers.Count != 0)
                _actorDataLayers[actorGuid] = normalizedLayers;
        }
        var loadedActorIds = _world.EnumerateActors(includePendingActors: true)
            .Select(actor => actor.ActorGuid).ToHashSet();
        foreach (var descriptor in unloadedActors)
        {
            if (descriptor.ActorGuid == Guid.Empty || loadedActorIds.Contains(descriptor.ActorGuid) ||
                _unloadedActors.Any(existing => existing.ActorGuid == descriptor.ActorGuid) ||
                string.IsNullOrWhiteSpace(descriptor.Label))
                throw new InvalidDataException("Scene contains invalid or duplicate unloaded Actor metadata.");
            if (descriptor.LevelGuid.HasValue && !levelIds.Contains(descriptor.LevelGuid.Value))
                throw new InvalidDataException($"Unloaded Actor '{descriptor.ActorGuid}' references a missing Level.");
            if (!descriptor.DataLayerGuids.All(layerIds.Contains))
                throw new InvalidDataException($"Unloaded Actor '{descriptor.ActorGuid}' references a missing Data Layer.");
            _unloadedActors.Add(descriptor with
            {
                Label = descriptor.Label.Trim(),
                ActorType = descriptor.ActorType?.Trim() ?? string.Empty,
                DataLayerGuids = descriptor.DataLayerGuids.Distinct().OrderBy(value => value).ToArray(),
            });
        }
    }

    internal void RestoreSessionStateFrom(EditorWorldOutlinerData source, IEnumerable<Actor> actors)
    {
        ArgumentNullException.ThrowIfNull(source);
        var actorGuids = actors.Select(actor => actor.ActorGuid).ToHashSet();
        _temporarilyHiddenActors.Clear();
        _temporarilyHiddenActors.UnionWith(source._temporarilyHiddenActors.Where(actorGuids.Contains));
        foreach (var actor in actors)
        {
            actor.SetTemporarilyHiddenInEditor(_temporarilyHiddenActors.Contains(actor.ActorGuid));
            foreach (var component in actor.Components)
                component.RefreshSceneProxy();
        }
        CurrentFolderGuid = source.CurrentFolderGuid is { } current && FindFolder(current) != null
            ? current
            : null;
        NotifyChanged();
    }

    private IEnumerable<Actor> EnumerateActorsInFolderSubtree(Guid folderGuid, IEnumerable<Actor> actors)
    {
        var folders = new HashSet<Guid> { folderGuid };
        var added = true;
        while (added)
        {
            added = false;
            foreach (var folder in _folders)
            {
                if (folder.ParentFolderGuid is { } parent && folders.Contains(parent))
                    added |= folders.Add(folder.FolderGuid);
            }
        }
        return actors.Where(actor => GetActorFolder(actor.ActorGuid) is { } actorFolder && folders.Contains(actorFolder));
    }

    private void ValidateFolder(EditorActorFolder folder, Guid? replacingFolderGuid)
    {
        if (folder.FolderGuid == Guid.Empty)
            throw new ArgumentException("FolderGuid cannot be empty.", nameof(folder));
        if (_folders.Any(existing => existing.FolderGuid == folder.FolderGuid &&
            existing.FolderGuid != replacingFolderGuid))
            throw new InvalidOperationException($"FolderGuid '{folder.FolderGuid}' already exists.");
        var normalizedName = NormalizeName(folder.Name);
        if (folder.ParentFolderGuid == folder.FolderGuid)
            throw new InvalidOperationException("A Folder cannot be its own parent.");
        if (folder.ParentFolderGuid.HasValue && FindFolder(folder.ParentFolderGuid.Value) == null)
            throw new InvalidOperationException("Parent Folder no longer exists.");
        if (_folders.Any(existing => existing.FolderGuid != replacingFolderGuid &&
            existing.ParentFolderGuid == folder.ParentFolderGuid &&
            string.Equals(existing.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A sibling Folder named '{normalizedName}' already exists.");
    }

    private void ValidateRestoredFolders()
    {
        foreach (var folder in _folders)
        {
            if (folder.ParentFolderGuid.HasValue && FindFolder(folder.ParentFolderGuid.Value) == null)
                throw new InvalidDataException($"Folder '{folder.FolderGuid}' references a missing parent.");
            var visited = new HashSet<Guid>();
            for (EditorActorFolder? current = folder; current != null;
                 current = current.ParentFolderGuid is { } parent ? FindFolder(parent) : null)
            {
                if (!visited.Add(current.FolderGuid))
                    throw new InvalidDataException("Editor Folder hierarchy contains a cycle.");
            }
        }
        if (_folders.GroupBy(folder => (folder.ParentFolderGuid, folder.Name), new FolderNameComparer()).Any(group => group.Count() > 1))
            throw new InvalidDataException("Sibling Editor Folders must have unique names.");
    }

    private int FindFolderIndex(Guid folderGuid)
    {
        var index = _folders.FindIndex(folder => folder.FolderGuid == folderGuid);
        return index >= 0 ? index : throw new InvalidOperationException("Folder no longer exists.");
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException("Folder name cannot be empty.", nameof(name));
        if (normalized.IndexOfAny(['/', '\\']) >= 0)
            throw new ArgumentException("Folder name cannot contain path separators.", nameof(name));
        return normalized;
    }

    private void NotifyChanged()
    {
        Revision++;
        Changed?.Invoke();
    }

    private void ApplyActorPreviewVisibility(Guid actorGuid, bool hidden)
    {
        var actor = _world.EnumerateActors(includePendingActors: true)
            .FirstOrDefault(candidate => candidate.ActorGuid == actorGuid);
        if (actor == null)
            return;
        actor.SetTemporarilyHiddenInEditor(hidden);
        foreach (var component in actor.Components)
            component.RefreshSceneProxy();
    }

    private sealed class FolderNameComparer : IEqualityComparer<(Guid? ParentFolderGuid, string Name)>
    {
        public bool Equals((Guid? ParentFolderGuid, string Name) x, (Guid? ParentFolderGuid, string Name) y)
            => x.ParentFolderGuid == y.ParentFolderGuid && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((Guid? ParentFolderGuid, string Name) obj)
            => HashCode.Combine(obj.ParentFolderGuid, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}
