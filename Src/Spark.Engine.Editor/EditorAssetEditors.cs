using System.Numerics;
using System.Reflection;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Input;
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
    Actor,
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
    private readonly EditorAssetThumbnailCache _thumbnailCache = new();
    private readonly IAssetRegistry? _assetRegistry;

    public EditorAssetEditorHost(UIElement sceneEditor, Action<UITabItem, Vector2>? tabDetachRequested = null,
        IAssetRegistry? assetRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(sceneEditor);
        _assetRegistry = assetRegistry;
        _tabs.AddTab(new UITabItem("Scene", sceneEditor));
        _tabs.TabClosed = tab =>
        {
            if (!_assetTabs.Remove(tab, out var assetGuid))
                return;
            _sessions.Remove(assetGuid);
            if (tab.Content is IDisposable disposable)
                disposable.Dispose();
        };
        _tabs.TabDragStarted = (tab, position) => tabDetachRequested?.Invoke(tab, position);
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

    /// <summary>把 Content Browser 的资源拖放转发给当前资源编辑器。</summary>
    public bool TryAcceptAssetDrop(AssetRecord record, Vector2 position)
        => _tabs.SelectedTab?.Content is EditorActorAssetEditorPanel actorEditor &&
           actorEditor.TryAcceptAssetDrop(record, position);

    public int TabCount => _tabs.Tabs.Count;

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
        var (kind, editor) = CreateEditor(record, resource, save, _thumbnailCache, _assetRegistry);
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

    public EditorAssetEditorDocument OpenActor(ActorAsset asset, Action? save = null)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var record = new AssetRecord
        {
            AssetGuid = asset.AssetGuid,
            AssetType = typeof(ActorAsset).AssemblyQualifiedName ?? typeof(ActorAsset).FullName ?? nameof(ActorAsset),
            ContentPath = asset.Document.Name + ".asset",
            Resource = asset,
            ImportStatus = AssetImportStatus.Imported,
        };
        return Open(record, asset, save);
    }

    public bool DetachTab(UITabItem tab)
    {
        var index = IndexOf(tab);
        if (index < 0)
            return false;
        _tabs.RemoveTab(index);
        return true;
    }

    public bool RestoreTab(UITabItem tab)
    {
        if (_tabs.Tabs.Any(existing => ReferenceEquals(existing, tab)))
            return false;
        _tabs.AddTab(tab);
        _tabs.SelectedIndex = IndexOf(tab);
        return true;
    }

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
        AssetRecord record, SceneResource resource, Action? save, EditorAssetThumbnailCache thumbnailCache,
        IAssetRegistry? assetRegistry) => resource switch
        {
            StaticMesh mesh => (EditorAssetEditorKind.StaticMesh,
                new EditorStaticMeshAssetEditorPanel(record, mesh, thumbnailCache)),
            Material material => (EditorAssetEditorKind.Material,
                new EditorMaterialAssetEditorPanel(record, material, save, thumbnailCache)),
            Texture2D texture => (EditorAssetEditorKind.Texture2D,
                new EditorTextureAssetEditorPanel(record, texture, thumbnailCache)),
            ActorAsset actor => (EditorAssetEditorKind.Actor,
                new EditorActorAssetEditorPanel(record, actor, save, assetRegistry)),
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

internal sealed class EditorStaticMeshAssetEditorPanel : EditorAssetEditorPanel, IDisposable
{
    private readonly UIImage _preview;

    public EditorStaticMeshAssetEditorPanel(AssetRecord record, StaticMesh mesh, EditorAssetThumbnailCache thumbnailCache)
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
        var thumbnail = thumbnailCache.GetOrCreate(record, mesh);
        _preview = new UIImage((uint)thumbnail.Width, (uint)thumbnail.Height, thumbnail.Pixels)
        {
            FixedSize = new UISize(160f, 160f),
        };
        Root.AddChild(_preview);
    }

    public void Dispose() => _preview.Dispose();
}

internal sealed class EditorMaterialAssetEditorPanel : EditorAssetEditorPanel, IDisposable
{
    private readonly UIPropertyGrid _properties;
    private readonly UIImage _preview;

    public EditorMaterialAssetEditorPanel(AssetRecord record, Material material, Action? save,
        EditorAssetThumbnailCache thumbnailCache)
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
        var thumbnail = thumbnailCache.GetOrCreate(record, material);
        _preview = new UIImage((uint)thumbnail.Width, (uint)thumbnail.Height, thumbnail.Pixels)
        {
            FixedSize = new UISize(160f, 160f),
        };
        Root.AddChild(_preview);
        _properties = new UIPropertyGrid
        {
            FixedSize = new UISize(0f, 0f),
            Target = material,
        };
        Root.AddChild(_properties);
    }

    public void Dispose() => _preview.Dispose();
}

internal sealed class EditorTextureAssetEditorPanel : EditorAssetEditorPanel, IDisposable
{
    private readonly UIImage _preview;

    public EditorTextureAssetEditorPanel(AssetRecord record, Texture2D texture,
        EditorAssetThumbnailCache thumbnailCache)
        : base(record, "Texture Editor")
    {
        Root.AddChild(new UILabel
        {
            Text = $"Dimensions: {texture.Width} x {texture.Height}\nFormat: RGBA8",
            TextColor = UITheme.Default.TextColor,
        });
        var thumbnail = thumbnailCache.GetOrCreate(record, texture);
        _preview = new UIImage((uint)thumbnail.Width, (uint)thumbnail.Height, thumbnail.Pixels)
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

/// <summary>UE 风格 Actor 编辑器：组件树、组件属性和轻量预览。</summary>
internal sealed class EditorActorAssetEditorPanel : EditorAssetEditorPanel
{
    private readonly ActorAsset _asset;
    private readonly UITreeView _components;
    private readonly UIPropertyGrid _properties;
    private readonly UIStackPanel _resourceRows;
    private readonly List<EditorResourcePropertyField> _resourceFields = [];
    private readonly IAssetRegistry? _assetRegistry;
    private readonly Actor _actor;
    private readonly ActorPreviewPanel _preview;
    private readonly UILabel _dirtyLabel;
    private readonly UIMenuPanel _addMenu = new() { MinWidth = 190f, MaxWidth = 260f };
    private readonly Dictionary<UITreeViewItem, ActorComponent> _componentMap = new();
    private ActorComponent? _selected;

    public EditorActorAssetEditorPanel(AssetRecord record, ActorAsset asset, Action? save,
        IAssetRegistry? assetRegistry)
        : base(record, "Actor Editor")
    {
        _asset = asset;
        _assetRegistry = assetRegistry;
        _dirtyLabel = new UILabel { TextColor = UITheme.Default.TextDimColor, Text = "" };
        var actor = asset.EditableActor ?? CreatePreviewActor(asset.Document, assetRegistry);
        _actor = actor;
        var toolbar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 30f),
            Spacing = 6f,
        };
        if (save != null)
        {
            toolbar.AddChild(new UIButton
            {
                Text = "Save Actor Asset",
                FixedSize = new UISize(140f, 26f),
                Clicked = () =>
                {
                    asset.SyncFromActor(actor);
                    save();
                },
            });
        }
        var addComponent = new UIButton { Text = "+ Add Component", FixedSize = new UISize(140f, 26f) };
        addComponent.Clicked = () =>
        {
            _addMenu.Clear();
            AddComponentItem(actor, "Scene Component", () => new SceneComponent());
            AddComponentItem(actor, "Static Mesh Component", () => new StaticMeshComponent());
            AddComponentItem(actor, "Camera Component", () => new CameraComponent());
            AddComponentItem(actor, "Point Light Component", () => new PointLightComponent());
            AddComponentItem(actor, "Directional Light Component", () => new DirectionalLightComponent());
            _addMenu.Canvas = addComponent.FindCanvas();
            _addMenu.Show(new Vector2(addComponent.Bounds.X, addComponent.Bounds.Bottom));
        };
        toolbar.AddChild(addComponent);

        var removeComponent = new UIButton { Text = "Remove Component", FixedSize = new UISize(150f, 26f) };
        removeComponent.Clicked = () =>
        {
            if (_selected == null || actor.Components.Count() <= 1)
                return;
            if (actor.RemoveOwnedComponent(_selected))
            {
                _selected = null;
                RebuildComponents(actor);
                UpdatePreview(actor);
                MarkDirty();
            }
        };
        var setRoot = new UIButton { Text = "Make Root", FixedSize = new UISize(100f, 26f) };
        setRoot.Clicked = () =>
        {
            if (_selected is SceneComponent scene)
            {
                actor.SetRootComponent(scene);
                RebuildComponents(actor);
                UpdatePreview(actor);
                MarkDirty();
            }
        };
        toolbar.AddChild(removeComponent);
        toolbar.AddChild(setRoot);
        Root.AddChild(toolbar);

        var columns = new UIStackPanel { Orientation = UIOrientation.Horizontal, FixedSize = new UISize(0f, 0f), Spacing = 8f };
        _properties = new UIPropertyGrid { FixedSize = new UISize(360f, 0f), LabelWidth = 120f };
        _components = new UITreeView { FixedSize = new UISize(260f, 0f), BackgroundColor = UITheme.Default.WindowBackground };
        _components.SelectionChanged = item =>
        {
            _selected = item != null && _componentMap.TryGetValue(item, out var component) ? component : null;
            _properties.Target = _selected;
            RebuildResourceFields();
            UpdatePreview(actor);
        };
        _components.ItemDropped = (sourceItem, targetItem, _) =>
        {
            if (!_componentMap.TryGetValue(sourceItem, out var source) || source is not SceneComponent sourceScene ||
                !_componentMap.TryGetValue(targetItem, out var target) || target is not SceneComponent targetScene)
                return;

            try
            {
                if (ReferenceEquals(sourceScene, targetScene))
                    return;
                // 拖到组件上：模拟 UE Attach，保持组件当前世界变换。
                sourceScene.AttachToComponent(targetScene, AttachmentTransformRules.KeepWorldTransform);

                RebuildComponents(actor);
                var selectedItem = _componentMap.FirstOrDefault(pair => ReferenceEquals(pair.Value, source)).Key;
                _components.SelectItem(selectedItem);
                UpdatePreview(actor);
                MarkDirty();
            }
            catch (Exception ex)
            {
                _dirtyLabel.Text = $"Hierarchy change failed: {ex.Message}";
            }
        };
        _components.ItemDroppedOnBackground = (sourceItem, _) =>
        {
            if (!_componentMap.TryGetValue(sourceItem, out var source) || source is not SceneComponent sourceScene ||
                sourceScene.AttachParent == null)
                return;
            sourceScene.DetachFromComponent(DetachmentTransformRules.KeepWorldTransform);
            RebuildComponents(actor);
            var selectedItem = _componentMap.FirstOrDefault(pair => ReferenceEquals(pair.Value, source)).Key;
            _components.SelectItem(selectedItem);
            UpdatePreview(actor);
            MarkDirty();
        };
        _properties.PropertyChanged = (_, _) =>
        {
            UpdatePreview(actor);
            MarkDirty();
        };
        columns.AddChild(_components);
        _preview = new ActorPreviewPanel(actor)
        {
            FixedSize = new UISize(0f, 0f),
        };
        _preview.ComponentSelected = component =>
        {
            _selected = component;
            var selectedItem = _componentMap.FirstOrDefault(pair => ReferenceEquals(pair.Value, component)).Key;
            _components.SelectItem(selectedItem);
            _properties.Target = component;
            RebuildResourceFields();
            UpdatePreview(actor);
        };
        _preview.ComponentMoved = component =>
        {
            _selected = component;
            _properties.Target = component;
            UpdatePreview(actor);
            MarkDirty();
        };
        columns.AddChild(_preview);
        var details = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(360f, 0f),
            Spacing = 6f,
        };
        details.AddChild(_properties);
        details.AddChild(new UILabel { Text = "Asset References", TextColor = UITheme.Default.TextDimColor });
        _resourceRows = new UIStackPanel { Orientation = UIOrientation.Vertical, Spacing = 2f, FixedSize = new UISize(0f, 0f) };
        details.AddChild(_resourceRows);
        columns.AddChild(details);
        Root.AddChild(columns);
        Root.AddChild(_dirtyLabel);
        RebuildComponents(actor);
        UpdatePreview(actor);

        void AddComponentItem(Actor target, string title, Func<ActorComponent> factory)
            => _addMenu.AddItem(new UIMenuItem(title, () =>
            {
                var component = factory();
                target.AddOwnedComponent(component);
                if (component is SceneComponent scene && target.RootComponent != null &&
                    !ReferenceEquals(scene, target.RootComponent))
                {
                    // UE 语义：有选中的空间组件时挂到选中项；否则挂到 Root。
                    var parent = _selected as SceneComponent ?? target.RootComponent;
                    if (!ReferenceEquals(parent, scene))
                        scene.AttachToComponent(parent, AttachmentTransformRules.KeepRelativeTransform);
                }
                RebuildComponents(target, component);
                UpdatePreview(target);
                MarkDirty();
            }));
    }

    private void RebuildComponents(Actor actor, ActorComponent? selectedComponent = null)
    {
        _componentMap.Clear();
        var roots = new List<UITreeViewItem>();
        var items = new Dictionary<ActorComponent, UITreeViewItem>();
        foreach (var component in actor.Components.OrderBy(component => component.ComponentGuid))
        {
            var rootMark = ReferenceEquals(component, actor.RootComponent) ? " [Root]" : string.Empty;
            var item = new UITreeViewItem($"{component.GetType().Name}{rootMark}")
            {
                IsExpanded = true,
                IconColor = component is SceneComponent
                    ? new Vector4(0.25f, 0.65f, 1f, 1f)
                    : new Vector4(0.62f, 0.64f, 0.7f, 1f),
                IsDropTarget = component is SceneComponent,
            };
            items[component] = item;
            _componentMap[item] = component;
        }
        foreach (var component in actor.Components.OrderBy(component => component.ComponentGuid))
        {
            if (component is SceneComponent scene && scene.AttachParent != null && items.TryGetValue(scene.AttachParent, out var parent))
                parent.AddSubItem(items[component]);
            else
                roots.Add(items[component]);
        }
        _components.SetRoots(roots);
        if (selectedComponent != null)
        {
            var selectedItem = _componentMap.FirstOrDefault(pair => ReferenceEquals(pair.Value, selectedComponent)).Key;
            _components.SelectItem(selectedItem);
        }
    }

    public bool TryAcceptAssetDrop(AssetRecord record, Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(record);
        foreach (var field in _resourceFields)
        {
            if (!field.TryAcceptDrop(record, position))
                continue;
            UpdatePreview(_actor);
            MarkDirty();
            return true;
        }
        return false;
    }

    private void RebuildResourceFields()
    {
        foreach (var field in _resourceFields)
            field.ClosePicker();
        _resourceFields.Clear();
        _resourceRows.ClearChildren();
        if (_assetRegistry == null || _selected == null)
            return;

        foreach (var property in _selected.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.CanRead && property.CanWrite &&
                         typeof(SceneResource).IsAssignableFrom(property.PropertyType) &&
                         property.GetCustomAttribute<ScenePropertyAttribute>() != null)
                     .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var slot = new EditorResourcePropertySlot(_selected, property);
            var field = new EditorResourcePropertyField(
                property.Name,
                property.PropertyType,
                new[] { slot },
                _assetRegistry,
                (slots, resource) =>
                {
                    try
                    {
                        foreach (var selectedSlot in slots)
                            selectedSlot.Property.SetValue(selectedSlot.Target, resource);
                        foreach (var resourceField in _resourceFields)
                            resourceField.Refresh();
                        UpdatePreview(_actor);
                        MarkDirty();
                    }
                    catch (Exception ex)
                    {
                        _dirtyLabel.Text = $"Asset assignment failed: {ex.Message}";
                    }
                },
                _ => { },
                _ => { });
            _resourceFields.Add(field);
            _resourceRows.AddChild(field);
        }
    }

    private void UpdatePreview(Actor actor)
    {
        _preview.SetState(actor, _selected);
        _dirtyLabel.Text = $"Preview: {actor.Name}  ·  Components: {actor.Components.Count()}  ·  Selected: {_selected?.GetType().Name ?? "None"}";
    }

    private void MarkDirty() => _dirtyLabel.Text = _dirtyLabel.Text.TrimEnd() + "  ·  Unsaved changes";

    private static Actor CreatePreviewActor(SceneActorDocument document, IAssetRegistry? assetRegistry)
    {
        var factory = new RuntimeActorFactory();
        var actor = factory.CreateActor(document);
        var sceneComponents = new Dictionary<Guid, SceneComponent>();
        foreach (var record in document.Components)
        {
            var component = factory.CreateComponent(record);
            component.ComponentGuid = record.ComponentGuid;
            try
            {
                ScenePropertySerializer.Restore(component, record.Properties, (guid, expectedType) =>
                {
                    if (assetRegistry == null)
                        throw new InvalidDataException("Actor preview has no asset registry.");
                    var resource = assetRegistry.Resolve(guid);
                    if (!expectedType.IsInstanceOfType(resource))
                        throw new InvalidDataException($"Asset '{guid}' is {resource.GetType().Name}, expected {expectedType.Name}.");
                    return resource;
                });
            }
            catch (InvalidDataException)
            {
                // 缺失资源不阻止 Actor 编辑器打开；先恢复可独立显示的变换/标量属性。
                ScenePropertySerializer.RestorePreview(component, record.Properties);
            }
            actor.AddOwnedComponent(component);
            if (component is SceneComponent scene)
            {
                scene.RelativeLocation = record.RelativeLocation;
                scene.RelativeRotation = record.RelativeRotation;
                scene.RelativeScale = record.RelativeScale;
                sceneComponents[scene.ComponentGuid] = scene;
            }
        }
        if (document.RootComponentGuid is { } rootGuid && sceneComponents.TryGetValue(rootGuid, out var root))
            actor.SetRootComponent(root);
        foreach (var record in document.Components)
        {
            if (record.ParentComponentGuid is { } parentGuid && sceneComponents.TryGetValue(record.ComponentGuid, out var child) &&
                sceneComponents.TryGetValue(parentGuid, out var parent))
                child.AttachToComponent(parent, AttachmentTransformRules.KeepRelativeTransform, record.AttachSocketName);
        }
        return actor;
    }
}

/// <summary>Actor 编辑器的轻量预览视口：绘制 XY 平面、坐标轴和组件位置标记。</summary>
internal sealed class ActorPreviewPanel : UIElement
{
    private Actor _actor;
    private ActorComponent? _selected;
    private Vector3 _cameraTarget;
    private float _cameraYaw = 0.65f;
    private float _cameraPitch = 0.45f;
    private float _cameraDistance = 10f;
    private Vector2 _lastPointer;
    private bool _orbiting;
    private bool _panning;
    private bool _moving;

    public Action<ActorComponent>? ComponentSelected { get; set; }
    public Action<SceneComponent>? ComponentMoved { get; set; }

    public ActorPreviewPanel(Actor actor)
    {
        _actor = actor;
        ClipToBounds = true;
    }

    public void SetState(Actor actor, ActorComponent? selected)
    {
        _actor = actor;
        _selected = selected;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        // 预览位于 Actor 编辑器的中间列，未指定固定尺寸时沿两个轴 Fill，
        // 由父级水平布局把剩余宽高分配给它，随窗口/分栏自动适应。
        if (FixedSize is not { } fixedSize)
            return new UISize(0f, 0f);
        return new UISize(fixedSize.Width > 0f ? fixedSize.Width : 0f,
            fixedSize.Height > 0f ? fixedSize.Height : 0f);
    }

    protected override void OnPaint(UIManager ui, int targetId)
    {
        var bounds = ContentRect;
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height),
            new Vector4(0.055f, 0.065f, 0.085f, 1f));
        if (bounds.Width <= 4f || bounds.Height <= 4f)
            return;

        var viewProjection = GetViewProjection(bounds);
        var center = new Vector2(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f);
        for (var i = -5; i <= 5; i++)
        {
            DrawProjectedLine(ui, targetId, viewProjection, bounds,
                new Vector3(i * 1.5f, 0f, -7.5f), new Vector3(i * 1.5f, 0f, 7.5f),
                new Vector4(0.12f, 0.14f, 0.18f, 1f));
            DrawProjectedLine(ui, targetId, viewProjection, bounds,
                new Vector3(-7.5f, 0f, i * 1.5f), new Vector3(7.5f, 0f, i * 1.5f),
                new Vector4(0.12f, 0.14f, 0.18f, 1f));
        }
        DrawProjectedLine(ui, targetId, viewProjection, bounds, Vector3.Zero, Vector3.UnitX * 3f,
            new Vector4(0.85f, 0.2f, 0.2f, 1f));
        DrawProjectedLine(ui, targetId, viewProjection, bounds, Vector3.Zero, Vector3.UnitY * 3f,
            new Vector4(0.25f, 0.85f, 0.35f, 1f));
        DrawProjectedLine(ui, targetId, viewProjection, bounds, Vector3.Zero, Vector3.UnitZ * 3f,
            new Vector4(0.25f, 0.45f, 1f, 1f));

        // 即使 Actor 只有非空间组件，也显示稳定的原点标记，避免预览区域看起来像空白。
        var origin = TryProject(Vector3.Zero, viewProjection, bounds, out var projectedOrigin)
            ? projectedOrigin
            : center;
        ui.DrawRect(targetId, origin - new Vector2(4f), new Vector2(8f),
            new Vector4(1f, 0.8f, 0.2f, 1f));

        foreach (var component in _actor.Components)
        {
            if (component is not SceneComponent scene)
                continue;
            if (!TryProject(Vector3.Transform(Vector3.Zero, scene.WorldTransform), viewProjection, bounds, out var point))
                continue;
            var selected = ReferenceEquals(component, _selected);
            var size = selected ? 9f : 6f;
            var color = selected
                ? new Vector4(1f, 0.8f, 0.2f, 1f)
                : ReferenceEquals(component, _actor.RootComponent)
                    ? new Vector4(0.25f, 0.65f, 1f, 1f)
                    : new Vector4(0.75f, 0.78f, 0.85f, 1f);
            ui.DrawRect(targetId, point - new Vector2(size * 0.5f), new Vector2(size), color);

            if (component is StaticMeshComponent meshComponent && meshComponent.Mesh != null)
                DrawStaticMeshWireframe(ui, targetId, meshComponent, viewProjection, bounds, color);
        }

        var text = GetTextRenderer();
        if (text != null)
            text.DrawText(ui, targetId, $"{_actor.Name} · Perspective · {_actor.Components.Count()} components",
                new Vector2(bounds.X + 8f, bounds.Y + 8f),
                UITheme.Default.TextDimColor);
    }

    private static void DrawProjectedLine(UIManager ui, int targetId, Matrix4x4 viewProjection, UIRect bounds,
        Vector3 start, Vector3 end, Vector4 color)
    {
        if (TryProject(start, viewProjection, bounds, out var a) && TryProject(end, viewProjection, bounds, out var b))
            ui.DrawLine(targetId, a, b, 1f, color);
    }

    private static void DrawStaticMeshWireframe(UIManager ui, int targetId, StaticMeshComponent component,
        Matrix4x4 viewProjection, UIRect bounds, Vector4 color)
    {
        var vertices = component.Mesh!.Vertices.Span;
        var indices = component.Mesh.Indices.Span;
        var maxIndex = System.Math.Min(indices.Length, 30_000); // 10k triangles 足够作为编辑器预览，避免 UI 基元爆炸。
        for (var index = 0; index + 2 < maxIndex; index += 3)
        {
            var ia = (int)indices[index];
            var ib = (int)indices[index + 1];
            var ic = (int)indices[index + 2];
            if ((uint)ia >= (uint)vertices.Length || (uint)ib >= (uint)vertices.Length || (uint)ic >= (uint)vertices.Length)
                continue;
            var a = Vector3.Transform(vertices[ia].Position, component.WorldTransform);
            var b = Vector3.Transform(vertices[ib].Position, component.WorldTransform);
            var c = Vector3.Transform(vertices[ic].Position, component.WorldTransform);
            DrawProjectedLine(ui, targetId, viewProjection, bounds, a, b, color);
            DrawProjectedLine(ui, targetId, viewProjection, bounds, b, c, color);
            DrawProjectedLine(ui, targetId, viewProjection, bounds, c, a, color);
        }
    }

    private Matrix4x4 GetViewProjection(UIRect bounds)
    {
        var pitch = System.Math.Clamp(_cameraPitch, -1.45f, 1.45f);
        var camera = _cameraTarget + new Vector3(
            MathF.Sin(_cameraYaw) * MathF.Cos(pitch) * _cameraDistance,
            MathF.Sin(pitch) * _cameraDistance,
            MathF.Cos(_cameraYaw) * MathF.Cos(pitch) * _cameraDistance);
        var aspect = MathF.Max(0.1f, bounds.Width / MathF.Max(1f, bounds.Height));
        return Matrix4x4.CreateLookAt(camera, _cameraTarget, Vector3.UnitY) *
            Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3f, aspect, 0.1f, 1000f);
    }

    private static bool TryProject(Vector3 point, Matrix4x4 viewProjection, UIRect bounds, out Vector2 screen)
    {
        var clip = Vector4.Transform(new Vector4(point, 1f), viewProjection);
        if (clip.W <= 0.001f)
        {
            screen = default;
            return false;
        }
        var ndc = new Vector3(clip.X, clip.Y, clip.Z) / clip.W;
        screen = new Vector2(bounds.X + (ndc.X + 1f) * 0.5f * bounds.Width,
            bounds.Y + (1f - ndc.Y) * 0.5f * bounds.Height);
        return ndc.Z >= -1f && ndc.Z <= 1f && bounds.Contains(screen);
    }

    protected override void OnMouseMove(Vector2 position) => _lastPointer = position;

    protected override void OnMouseDown(MouseButton button)
    {
        _lastPointer = _lastPointer == default ? new Vector2(Bounds.X, Bounds.Y) : _lastPointer;
        if (button == MouseButton.Right)
            _orbiting = true;
        else if (button == MouseButton.Middle)
            _panning = true;
        else if (button == MouseButton.Left)
        {
            var hit = FindComponentAt(_lastPointer);
            if (hit != null)
            {
                _selected = hit;
                ComponentSelected?.Invoke(hit);
                _moving = hit is SceneComponent;
            }
        }
    }

    protected override void OnMouseDrag(Vector2 position, MouseButton button)
    {
        var delta = position - _lastPointer;
        _lastPointer = position;
        if (_orbiting)
        {
            _cameraYaw -= delta.X * 0.01f;
            _cameraPitch = System.Math.Clamp(_cameraPitch - delta.Y * 0.01f, -1.45f, 1.45f);
            return;
        }
        if (_panning)
        {
            var speed = _cameraDistance * 0.0025f;
            var forward = Vector3.Normalize(_cameraTarget - GetCameraPosition());
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            var up = Vector3.Normalize(Vector3.Cross(forward, right));
            _cameraTarget += (-right * delta.X + up * delta.Y) * speed;
            return;
        }
        if (_moving && _selected is SceneComponent scene)
        {
            var speed = _cameraDistance * 0.003f;
            var forward = Vector3.Normalize(_cameraTarget - GetCameraPosition());
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
            var up = Vector3.Normalize(Vector3.Cross(forward, right));
            scene.RelativeLocation += (-right * delta.X + up * delta.Y) * speed;
            ComponentMoved?.Invoke(scene);
        }
    }

    protected override void OnMouseUp(MouseButton button, Vector2 position, KeyMask keysDown)
    {
        if (button == MouseButton.Right) _orbiting = false;
        if (button == MouseButton.Middle) _panning = false;
        if (button == MouseButton.Left) _moving = false;
    }

    protected override void OnMouseWheel(float delta)
    {
        _cameraDistance = System.Math.Clamp(_cameraDistance * MathF.Exp(-delta / 120f * 0.12f), 1.5f, 100f);
    }

    private Vector3 GetCameraPosition()
    {
        var pitch = System.Math.Clamp(_cameraPitch, -1.45f, 1.45f);
        return _cameraTarget + new Vector3(
            MathF.Sin(_cameraYaw) * MathF.Cos(pitch) * _cameraDistance,
            MathF.Sin(pitch) * _cameraDistance,
            MathF.Cos(_cameraYaw) * MathF.Cos(pitch) * _cameraDistance);
    }

    private SceneComponent? FindComponentAt(Vector2 position)
    {
        var projection = GetViewProjection(ContentRect);
        SceneComponent? closest = null;
        var distance = 14f * 14f;
        foreach (var component in _actor.Components.OfType<SceneComponent>())
        {
            if (!TryProject(Vector3.Transform(Vector3.Zero, component.WorldTransform), projection, ContentRect, out var point))
                continue;
            var candidateDistance = Vector2.DistanceSquared(point, position);
            if (candidateDistance <= distance)
            {
                distance = candidateDistance;
                closest = component;
            }
        }
        return closest;
    }
}
