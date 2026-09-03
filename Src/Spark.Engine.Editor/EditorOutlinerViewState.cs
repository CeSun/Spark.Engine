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

public sealed record EditorOutlinerCustomFilter(string Name, string Query, List<string> ActorTypes);

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
    public float TypeColumnWidth { get; set; } = 92f;
    public float SocketColumnWidth { get; set; } = 90f;
    public float IdColumnWidth { get; set; } = 88f;
    public EditorOutlinerColumn SortColumn { get; set; } = EditorOutlinerColumn.Label;
    public bool SortAscending { get; set; } = true;
    public float ScrollOffsetX { get; set; }
    public float ScrollOffsetY { get; set; }
    public Guid? CurrentFolderGuid { get; set; }
    public HashSet<string> ActorTypes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<Guid, bool> ActorExpansion { get; set; } = [];
    public Dictionary<Guid, bool> FolderExpansion { get; set; } = [];
    public List<EditorOutlinerCustomFilter> CustomFilters { get; set; } = [];
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
        state.CustomFilters = (state.CustomFilters ?? [])
            .Where(filter => filter != null)
            .Select(filter => new EditorOutlinerCustomFilter(
                filter.Name ?? string.Empty,
                filter.Query ?? string.Empty,
                filter.ActorTypes ?? []))
            .ToList();
        state.TypeColumnWidth = NormalizeWidth(state.TypeColumnWidth, 92f);
        state.SocketColumnWidth = NormalizeWidth(state.SocketColumnWidth, 90f);
        state.IdColumnWidth = NormalizeWidth(state.IdColumnWidth, 88f);
        state.ScrollOffsetX = System.Math.Max(0f, state.ScrollOffsetX);
        state.ScrollOffsetY = System.Math.Max(0f, state.ScrollOffsetY);
        return state;
    }

    private static float NormalizeWidth(float value, float fallback)
        => float.IsFinite(value) && value >= 48f ? System.Math.Min(value, 320f) : fallback;
}
