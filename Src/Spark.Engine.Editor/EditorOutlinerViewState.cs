using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spark.Engine.Editor;

public enum EditorOutlinerColumn
{
    Label,
    Type,
    Socket,
    Id,
}

public enum EditorOutlinerWorldSource
{
    ActiveWorld,
    EditorWorld,
}

public sealed record EditorOutlinerCustomFilter(string Name, string Query, List<string> ActorTypes)
{
    public List<string> ExtensionFilterIds { get; init; } = [];
}

/// <summary>一个 Outliner 实例独占的查询、列、排序和导航状态。</summary>
public sealed class EditorOutlinerViewState
{
    public string SearchText { get; set; } = string.Empty;
    public bool ShowInternalActors { get; set; }
    public bool ShowDeveloperComponents { get; set; }
    public bool OnlySelected { get; set; }
    public bool HideTemporarilyHidden { get; set; }
    public bool ShowTypeColumn { get; set; } = true;
    public bool ShowSocketColumn { get; set; }
    public bool ShowIdColumn { get; set; }
    public bool AlwaysFrameSelection { get; set; } = true;
    public EditorOutlinerWorldSource WorldSource { get; set; } = EditorOutlinerWorldSource.ActiveWorld;
    public float TypeColumnWidth { get; set; } = 92f;
    public float SocketColumnWidth { get; set; } = 90f;
    public float IdColumnWidth { get; set; } = 88f;
    public EditorOutlinerColumn SortColumn { get; set; } = EditorOutlinerColumn.Label;
    /// <summary>非内置列的排序 ID；为空时沿用旧版 SortColumn，兼容已有项目配置。</summary>
    public string? ExtensionSortColumnId { get; set; }
    public bool SortAscending { get; set; } = true;
    public float ScrollOffsetX { get; set; }
    public float ScrollOffsetY { get; set; }
    public Guid? CurrentFolderGuid { get; set; }
    public HashSet<string> ActorTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<Guid, bool> ActorExpansion { get; set; } = [];
    public Dictionary<Guid, bool> FolderExpansion { get; set; } = [];
    public Dictionary<Guid, bool> RuntimeActorExpansion { get; set; } = [];
    public float RuntimeScrollOffsetX { get; set; }
    public float RuntimeScrollOffsetY { get; set; }
    public List<EditorOutlinerCustomFilter> CustomFilters { get; set; } = [];
    public Dictionary<string, bool> ExtensionColumnVisibility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> ExtensionColumnWidths { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> EnabledExtensionFilters { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    [System.Text.Json.Serialization.JsonIgnore]
    public string SortColumnId
    {
        get => string.IsNullOrWhiteSpace(ExtensionSortColumnId)
            ? SortColumn switch
            {
                EditorOutlinerColumn.Type => EditorOutlinerColumnIds.Type,
                EditorOutlinerColumn.Socket => EditorOutlinerColumnIds.Socket,
                EditorOutlinerColumn.Id => EditorOutlinerColumnIds.Id,
                _ => EditorOutlinerColumnIds.Label,
            }
            : ExtensionSortColumnId;
        set
        {
            var normalized = EditorOutlinerColumnDescriptor.ValidateId(value);
            ExtensionSortColumnId = normalized switch
            {
                EditorOutlinerColumnIds.Label => SetBuiltInSort(EditorOutlinerColumn.Label),
                EditorOutlinerColumnIds.Type => SetBuiltInSort(EditorOutlinerColumn.Type),
                EditorOutlinerColumnIds.Socket => SetBuiltInSort(EditorOutlinerColumn.Socket),
                EditorOutlinerColumnIds.Id => SetBuiltInSort(EditorOutlinerColumn.Id),
                _ => normalized,
            };
        }
    }

    public bool IsColumnVisible(EditorOutlinerColumnDescriptor column)
        => column.Id switch
        {
            EditorOutlinerColumnIds.Label => true,
            EditorOutlinerColumnIds.Type => ShowTypeColumn,
            EditorOutlinerColumnIds.Socket => ShowSocketColumn,
            EditorOutlinerColumnIds.Id => ShowIdColumn,
            _ => ExtensionColumnVisibility.TryGetValue(column.Id, out var visible)
                ? visible : column.DefaultVisible,
        };

    public void SetColumnVisible(EditorOutlinerColumnDescriptor column, bool visible)
    {
        switch (column.Id)
        {
            case EditorOutlinerColumnIds.Type: ShowTypeColumn = visible; break;
            case EditorOutlinerColumnIds.Socket: ShowSocketColumn = visible; break;
            case EditorOutlinerColumnIds.Id: ShowIdColumn = visible; break;
            case EditorOutlinerColumnIds.Label: break;
            default: ExtensionColumnVisibility[column.Id] = visible; break;
        }
    }

    public float GetColumnWidth(EditorOutlinerColumnDescriptor column)
        => column.Id switch
        {
            EditorOutlinerColumnIds.Type => TypeColumnWidth,
            EditorOutlinerColumnIds.Socket => SocketColumnWidth,
            EditorOutlinerColumnIds.Id => IdColumnWidth,
            _ => ExtensionColumnWidths.TryGetValue(column.Id, out var width)
                ? width : column.DefaultWidth,
        };

    public void SetColumnWidth(EditorOutlinerColumnDescriptor column, float width)
    {
        width = System.Math.Clamp(width, 48f, 320f);
        switch (column.Id)
        {
            case EditorOutlinerColumnIds.Type: TypeColumnWidth = width; break;
            case EditorOutlinerColumnIds.Socket: SocketColumnWidth = width; break;
            case EditorOutlinerColumnIds.Id: IdColumnWidth = width; break;
            default: ExtensionColumnWidths[column.Id] = width; break;
        }
    }

    private string? SetBuiltInSort(EditorOutlinerColumn column)
    {
        SortColumn = column;
        return null;
    }
}

public sealed class EditorOutlinerViewStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Path { get; }

    public EditorOutlinerViewStateStore(string path)
        => Path = System.IO.Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));

    public static EditorOutlinerViewStateStore ForProject(string? projectDirectory, string slot = "primary")
    {
        var identity = string.IsNullOrWhiteSpace(projectDirectory)
            ? "projectless"
            : System.IO.Path.GetFullPath(projectDirectory).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new EditorOutlinerViewStateStore(System.IO.Path.Combine(
            root, "Spark.Engine", "Editor", "Outliner", $"{hash}-{slot}.json"));
    }

    public EditorOutlinerViewState Load()
    {
        try
        {
            if (!File.Exists(Path))
                return new EditorOutlinerViewState();
            return Normalize(JsonSerializer.Deserialize<EditorOutlinerViewState>(File.ReadAllText(Path), JsonOptions)
                ?? new EditorOutlinerViewState());
        }
        catch (JsonException)
        {
            return new EditorOutlinerViewState();
        }
        catch (IOException)
        {
            return new EditorOutlinerViewState();
        }
        catch (UnauthorizedAccessException)
        {
            return new EditorOutlinerViewState();
        }
    }

    public void Save(EditorOutlinerViewState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temporary, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    private static EditorOutlinerViewState Normalize(EditorOutlinerViewState state)
    {
        state.SearchText ??= string.Empty;
        state.ActorTypes = new HashSet<string>(state.ActorTypes ?? [], StringComparer.OrdinalIgnoreCase);
        state.ActorExpansion ??= [];
        state.FolderExpansion ??= [];
        state.RuntimeActorExpansion ??= [];
        state.ExtensionColumnVisibility = new Dictionary<string, bool>(
            state.ExtensionColumnVisibility ?? [], StringComparer.OrdinalIgnoreCase);
        state.ExtensionColumnWidths = new Dictionary<string, float>(
            state.ExtensionColumnWidths ?? [], StringComparer.OrdinalIgnoreCase);
        state.EnabledExtensionFilters = new HashSet<string>(
            state.EnabledExtensionFilters ?? [], StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(state.ExtensionSortColumnId))
            state.ExtensionSortColumnId = EditorOutlinerColumnDescriptor.ValidateId(state.ExtensionSortColumnId);
        state.CustomFilters = (state.CustomFilters ?? [])
            .Where(filter => filter != null)
            .Select(filter => new EditorOutlinerCustomFilter(
                filter.Name ?? string.Empty,
                filter.Query ?? string.Empty,
                filter.ActorTypes ?? [])
            {
                ExtensionFilterIds = filter.ExtensionFilterIds ?? [],
            })
            .ToList();
        state.TypeColumnWidth = NormalizeWidth(state.TypeColumnWidth, 92f);
        state.SocketColumnWidth = NormalizeWidth(state.SocketColumnWidth, 90f);
        state.IdColumnWidth = NormalizeWidth(state.IdColumnWidth, 88f);
        foreach (var columnId in state.ExtensionColumnWidths.Keys.ToArray())
            state.ExtensionColumnWidths[columnId] = NormalizeWidth(state.ExtensionColumnWidths[columnId], 90f);
        state.ScrollOffsetX = System.Math.Max(0f, state.ScrollOffsetX);
        state.ScrollOffsetY = System.Math.Max(0f, state.ScrollOffsetY);
        state.RuntimeScrollOffsetX = System.Math.Max(0f, state.RuntimeScrollOffsetX);
        state.RuntimeScrollOffsetY = System.Math.Max(0f, state.RuntimeScrollOffsetY);
        if (!Enum.IsDefined(state.WorldSource))
            state.WorldSource = EditorOutlinerWorldSource.ActiveWorld;
        return state;
    }

    private static float NormalizeWidth(float value, float fallback)
        => float.IsFinite(value) && value >= 48f ? System.Math.Min(value, 320f) : fallback;
}
