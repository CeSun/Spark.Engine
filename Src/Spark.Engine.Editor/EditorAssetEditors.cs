using System.Numerics;
using Spark.Engine.Resources;
using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>内容浏览器可打开的资源编辑器类型。</summary>
public enum EditorAssetEditorKind
{
    StaticMesh,
    Material,
    Texture2D,
    Generic,
}

/// <summary>已打开资源编辑器的稳定、无 UI 状态。</summary>
public sealed record EditorAssetEditorDocument(
    Guid AssetGuid,
    string Title,
    EditorAssetEditorKind Kind);

/// <summary>承载场景视口和资源编辑器的文档标签页。</summary>
internal sealed class EditorAssetEditorHost : UIElement
{
    private sealed record Session(EditorAssetEditorDocument Document, UITabItem Tab);

    private readonly UITabView _tabs = new() { FixedSize = new UISize(0f, 0f) };
    private readonly Dictionary<Guid, Session> _sessions = new();
    private readonly Dictionary<UITabItem, Guid> _assetTabs = new();

    public EditorAssetEditorHost(UIElement sceneEditor)
    {
        ArgumentNullException.ThrowIfNull(sceneEditor);
        _tabs.AddTab(new UITabItem("Scene", sceneEditor));
        _tabs.TabClosed = tab =>
        {
            if (!_assetTabs.Remove(tab, out var assetGuid))
                return;
            _sessions.Remove(assetGuid);
            if (tab.Content is IDisposable disposable)
                disposable.Dispose();
        };
        AddChild(_tabs);
    }

    public IReadOnlyList<EditorAssetEditorDocument> Documents => _tabs.Tabs
        .Where(_assetTabs.ContainsKey)
        .Select(tab => _sessions[_assetTabs[tab]].Document)
        .ToArray();

    public EditorAssetEditorDocument? ActiveDocument
        => _tabs.SelectedTab is { } tab && _assetTabs.TryGetValue(tab, out var assetGuid)
            ? _sessions[assetGuid].Document
            : null;

    public EditorAssetEditorDocument Open(AssetRecord record, SceneResource resource, Action? save)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(resource);
        if (_sessions.TryGetValue(record.AssetGuid, out var existing))
        {
            _tabs.SelectedIndex = IndexOf(existing.Tab);
            return existing.Document;
        }

        var title = Path.GetFileNameWithoutExtension(
            record.ContentPath ?? record.SourcePath ?? record.AssetGuid.ToString("N"));
        var (kind, editor) = CreateEditor(record, resource, save);
        var document = new EditorAssetEditorDocument(record.AssetGuid, title, kind);
        var tab = new UITabItem(title, editor, canClose: true);
        var session = new Session(document, tab);
        _sessions.Add(record.AssetGuid, session);
        _assetTabs.Add(tab, record.AssetGuid);
        _tabs.AddTab(tab);
        _tabs.SelectedIndex = IndexOf(tab);
        return document;
    }

    public bool Close(Guid assetGuid)
    {
        if (!_sessions.TryGetValue(assetGuid, out var session))
            return false;
        _tabs.CloseTab(IndexOf(session.Tab));
        return !_sessions.ContainsKey(assetGuid);
    }

    public void ShowScene() => _tabs.SelectedIndex = 0;

    private int IndexOf(UITabItem tab)
    {
        for (var index = 0; index < _tabs.Tabs.Count; index++)
        {
            if (ReferenceEquals(_tabs.Tabs[index], tab))
                return index;
        }
        return -1;
    }

    private static (EditorAssetEditorKind Kind, UIElement Editor) CreateEditor(
        AssetRecord record, SceneResource resource, Action? save) => resource switch
        {
            StaticMesh mesh => (EditorAssetEditorKind.StaticMesh,
                new EditorStaticMeshAssetEditorPanel(record, mesh)),
            Material material => (EditorAssetEditorKind.Material,
                new EditorMaterialAssetEditorPanel(record, material, save)),
            Texture2D texture => (EditorAssetEditorKind.Texture2D,
                new EditorTextureAssetEditorPanel(record, texture)),
            _ => (EditorAssetEditorKind.Generic,
                new EditorGenericAssetEditorPanel(record, resource)),
        };

    protected override UISize OnMeasure(UISize availableSize)
    {
        _tabs.Measure(availableSize);
        return _tabs.DesiredSize;
    }

    protected override void OnArrange() => _tabs.Arrange(ContentRect);
}

internal abstract class EditorAssetEditorPanel : UIElement
{
    protected EditorAssetEditorPanel(AssetRecord record, string editorName)
    {
        Root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 0f),
            Padding = UIEdgeInsets.All(12f),
            Spacing = 8f,
            BackgroundColor = UITheme.Default.PanelBackground,
        };
        Root.AddChild(new UILabel
        {
            Text = editorName.ToUpperInvariant(),
            TextColor = UITheme.Default.TextDimColor,
        });
        Root.AddChild(new UILabel
        {
            Text = EditorContentBrowserModel.GetDisplayName(record),
            TextColor = UITheme.Default.TextColor,
        });
        Root.AddChild(new UILabel
        {
            Text = $"AssetGuid: {record.AssetGuid}\nPath: {record.ContentPath ?? record.SourcePath ?? "(in-memory asset)"}",
            TextColor = UITheme.Default.TextDimColor,
        });
        AddChild(Root);
    }

    protected UIStackPanel Root { get; }

    protected override UISize OnMeasure(UISize availableSize)
    {
        Root.Measure(availableSize);
        return Root.DesiredSize;
    }

    protected override void OnArrange() => Root.Arrange(ContentRect);
}

internal sealed class EditorStaticMeshAssetEditorPanel : EditorAssetEditorPanel
{
    public EditorStaticMeshAssetEditorPanel(AssetRecord record, StaticMesh mesh)
        : base(record, "Static Mesh Editor")
    {
        var bounds = mesh.Bounds;
        Root.AddChild(new UILabel
        {
            Text = $"Vertices: {mesh.Vertices.Length:N0}\n" +
                   $"Triangles: {mesh.Indices.Length / 3:N0}\n" +
                   $"Bounds center: {bounds.Center}\nBounds radius: {bounds.Radius:F3}",
            TextColor = UITheme.Default.TextColor,
        });
    }
}

internal sealed class EditorMaterialAssetEditorPanel : EditorAssetEditorPanel
{
    private readonly UIPropertyGrid _properties;

    public EditorMaterialAssetEditorPanel(AssetRecord record, Material material, Action? save)
        : base(record, "Material Editor")
    {
        if (save != null)
        {
            Root.AddChild(new UIButton
            {
                Text = "Save Asset",
                FixedSize = new UISize(110f, 26f),
                Clicked = save,
            });
        }
        _properties = new UIPropertyGrid
        {
            FixedSize = new UISize(0f, 0f),
            Target = material,
        };
        Root.AddChild(_properties);
    }
}

internal sealed class EditorTextureAssetEditorPanel : EditorAssetEditorPanel, IDisposable
{
    private readonly UIImage _preview;

    public EditorTextureAssetEditorPanel(AssetRecord record, Texture2D texture)
        : base(record, "Texture Editor")
    {
        Root.AddChild(new UILabel
        {
            Text = $"Dimensions: {texture.Width} x {texture.Height}\nFormat: RGBA8",
            TextColor = UITheme.Default.TextColor,
        });
        _preview = new UIImage(texture.Width, texture.Height, texture.PixelData.Span)
        {
            FixedSize = new UISize(0f, 0f),
        };
        Root.AddChild(_preview);
    }

    public void Dispose() => _preview.Dispose();
}

internal sealed class EditorGenericAssetEditorPanel : EditorAssetEditorPanel
{
    public EditorGenericAssetEditorPanel(AssetRecord record, SceneResource resource)
        : base(record, "Resource Editor")
    {
        Root.AddChild(new UILabel
        {
            Text = $"Resource type: {resource.GetType().FullName}",
            TextColor = UITheme.Default.TextColor,
        });
    }
}
