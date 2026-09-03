using System.Numerics;
using System.Reflection;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Input;
using Spark.Engine.Resources;
using Spark.Engine.Render;
using Spark.Engine.Render.Common;
using Spark.Engine.UI;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>
/// 检查器面板：选中层级树节点后，用 <see cref="UIPropertyGrid"/> 反射展示/编辑对象属性
/// （Transform、光照参数、材质参数等可读写属性；Mesh/Material 等引用类型不生成行）。
/// 每帧 Refresh：属性值实时回显（如动画驱动的变换），编辑中不覆盖输入。
/// </summary>
public sealed class EditorUi
{
    private readonly EditorHierarchyPanel _hierarchy;
    private readonly EditorInspectorPanel _inspector;
    private readonly EditorViewportPanel _viewport;
    private readonly EditorAssetEditorHost _assetEditorHost;
    private readonly EditorStatusBarPanel _statusBar;
    private readonly EditorToolbarPanel _toolbar;
    private readonly EditorDeleteConfirmationPanel _deleteConfirmation;
    private readonly EditorAssetDeleteConfirmationPanel _assetDeleteConfirmation;
    private readonly EditorCloseConfirmationPanel _closeConfirmation;
    private readonly EditorAssetErrorsPanel _assetErrors;
    private readonly EditorContentBrowserPanel _contentBrowser;
    private readonly EditorContext _context;
    private readonly IEditorSceneService? _sceneService;
    private readonly EditorAssetImportService _assetImportService = new();
    private readonly EditorAssetOperationService? _assetOperations;
    public EditorProject? Project { get; }
    private readonly TransformGizmoController _gizmo = new();
    private readonly EditorCameraController _cameraController = new();
    private GizmoOperation _gizmoOperation = GizmoOperation.Move;
    private GizmoSpace _gizmoSpace = GizmoSpace.World;
    private bool _transformToolActive = true;
    private bool _suppressViewportClick;
    private UIRenderView? _renderViewControl;
    private readonly CameraSnapshotSourceRegistry _cameraSnapshotSources;
    private readonly List<EditorViewportSession> _viewportSessions = [];
    private IReadOnlyList<object> _editorOutlinerSelection = Array.Empty<object>();
    private object? _editorOutlinerPrimary;
    private IReadOnlyList<object> _runtimeOutlinerSelection = Array.Empty<object>();
    private object? _runtimeOutlinerPrimary;
    private bool _skipOutlinerSelectionCapture;

    private object? _selectedTarget;

    /// <summary>编辑器根元素（挂到主窗口画布 Root）。</summary>
    public UIElement Root { get; }

    public IReadOnlyList<EditorViewportSession> ViewportSessions => _viewportSessions;

    public EditorUi(World world, Action? backToHub = null, IEditorSceneService? sceneService = null,
        WorldContext? worldContext = null, EditorProject? project = null,
        CameraSnapshotSourceRegistry? cameraSnapshotSources = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        _sceneService = sceneService;
        Project = project;
        _cameraSnapshotSources = cameraSnapshotSources ?? new CameraSnapshotSourceRegistry();
        _context = new EditorContext(world, worldContext);
        _assetOperations = project != null && _context.AssetRegistry is AssetRegistry mutableRegistry
            ? new EditorAssetOperationService(project, mutableRegistry)
            : null;
        // 让内容浏览器在编辑器首次打开时即可显示当前场景引用的资产。
        _context.RegisterWorldAssets();
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = UITheme.Default.WindowBackground, // 铺满窗口，遮挡底层 3D 场景
        };

        root.AddChild(new EditorMenuPanel(
            save: SaveScene,
            reload: ReloadScene,
            undo: Undo,
            redo: Redo,
            showAssetErrors: ShowAssetErrors,
            refreshAssets: RefreshContent,
            resetLayout: () => SetStatus("Layout reset requested."),
            backToHub));

        _toolbar = new EditorToolbarPanel(
            select: SetSelectTool,
            move: () => SetGizmoOperation(GizmoOperation.Move),
            rotate: () => SetGizmoOperation(GizmoOperation.Rotate),
            scale: () => SetGizmoOperation(GizmoOperation.Scale),
            addActor: AddActor,
            duplicate: DuplicateSelection,
            rename: RenameSelection,
            delete: DeleteSelection,
            play: TogglePlay,
            openControlTests: () => _openControlTests?.Invoke(),
            toggleSnap: ToggleGridSnap);
        root.AddChild(_toolbar);

        // UE 风格主工作区：层级 | 视口 | Details，所有区域均可拖动调整。
        _hierarchy = new EditorHierarchyPanel(_context.World, _context.Outliner,
            viewStateStore: project == null ? null : EditorOutlinerViewStateStore.ForProject(project.RootDirectory));
        _hierarchy.ItemDropped += HandleHierarchyDrop;
        _hierarchy.CreateFolderRequested = CreateOutlinerFolder;
        _hierarchy.DeleteRequested = DeleteOutlinerTarget;
        _hierarchy.VisibilityToggled = ToggleOutlinerVisibility;
        _hierarchy.RenameSubmitted = CommitOutlinerRename;
        _hierarchy.MakeCurrentFolderRequested = MakeCurrentOutlinerFolder;
        _hierarchy.ClearCurrentFolderRequested = ClearCurrentOutlinerFolder;
        _hierarchy.CreateSubfolderRequested = folder => CreateOutlinerFolder(folder.FolderGuid);
        _hierarchy.SelectFolderActorsRequested = SelectOutlinerFolderActors;
        _hierarchy.FocusActorRequested = actor => { _context.Selection.Selected = actor; FocusSelectionInViewport(); };
        _hierarchy.DuplicateActorRequested = actor => { _context.Selection.Selected = actor; DuplicateSelection(); };
        _hierarchy.DetachActorRequested = DetachOutlinerActor;
        _hierarchy.MoveActorToCurrentFolderRequested = MoveActorToCurrentFolder;
        _hierarchy.SelectActorChildrenRequested = SelectOutlinerActorChildren;
        _hierarchy.ItemDroppedOnBackground = HandleHierarchyBackgroundDrop;
        _hierarchy.WorldSourceChanged = _ => SwitchOutlinerWorld(captureCurrentSelection: true);

        _viewport = new EditorViewportPanel();
        _assetEditorHost = new EditorAssetEditorHost(_viewport);

        _inspector = new EditorInspectorPanel(
            RequestPropertyEdit,
            _context.AssetRegistry,
            (slots, resource) => RequestResourcePropertyEdit(slots, resource),
            assetGuid => RevealAsset(assetGuid),
            assetGuid => OpenAssetEditor(assetGuid));

        _contentBrowser = new EditorContentBrowserPanel(_context.AssetRegistry, project?.ContentDirectory);
        _contentBrowser.AssetActivated += HandleAssetActivated;
        _contentBrowser.AssetDropped += HandleAssetDropped;
        _contentBrowser.FolderCreateRequested += (parent, name) =>
            TryContentAction(() => CreateContentDirectory(parent, name));
        _contentBrowser.MaterialCreateRequested += (directory, name) =>
            TryContentAction(() => CreateContentMaterial(directory, name));
        _contentBrowser.FolderRenameRequested += (directory, name) =>
            TryContentAction(() => RenameContentDirectory(directory, name));
        _contentBrowser.FolderMoveRequested += (directory, destination) =>
            TryContentAction(() => MoveContentDirectory(directory, destination));
        _contentBrowser.FolderCopyRequested += (directory, destination) =>
            TryContentAction(() => CopyContentDirectory(directory, destination));
        _contentBrowser.AssetRenameRequested += (assetGuid, name) =>
            TryContentAction(() => RenameContentAsset(assetGuid, name));
        _contentBrowser.AssetMoveRequested += (assetGuid, destination) =>
            TryContentAction(() => MoveContentAsset(assetGuid, destination));
        _contentBrowser.AssetCopyRequested += (assetGuid, destination) =>
            TryContentAction(() => CopyContentAsset(assetGuid, destination));

        var viewportDetails = new UISplitPanel
        {
            Direction = UISplitDirection.Horizontal,
            SplitRatio = 0.68f,
            SplitterWidth = 4f,
            MinFirstSize = 360f,
            MinSecondSize = 280f,
            FixedSize = new UISize(0f, 0f),
        };
        viewportDetails.SetPanels(_assetEditorHost, _inspector);

        var mainColumns = new UISplitPanel
        {
            Direction = UISplitDirection.Horizontal,
            SplitRatio = 0.22f,
            SplitterWidth = 4f,
            MinFirstSize = 180f,
            MinSecondSize = 640f,
            FixedSize = new UISize(0f, 0f),
        };
        mainColumns.SetPanels(_hierarchy, viewportDetails);

        var workspace = new UISplitPanel
        {
            Direction = UISplitDirection.Vertical,
            SplitRatio = 0.68f,
            SplitterWidth = 4f,
            MinFirstSize = 280f,
            MinSecondSize = 170f,
            FixedSize = new UISize(0f, 0f),
        };
        workspace.SetPanels(mainColumns, _contentBrowser);
        root.AddChild(workspace);

        _deleteConfirmation = new EditorDeleteConfirmationPanel(ConfirmDeleteSelection);
        root.AddChild(_deleteConfirmation);

        _assetDeleteConfirmation = new EditorAssetDeleteConfirmationPanel();
        _contentBrowser.AssetDeleteRequested += assetGuid =>
        {
            var record = AssetRegistry.Records.FirstOrDefault(candidate => candidate.AssetGuid == assetGuid);
            if (record != null)
                _assetDeleteConfirmation.Request(EditorContentBrowserModel.GetDisplayName(record),
                    () => TryContentAction(() => DeleteContentAsset(assetGuid)));
        };
        _contentBrowser.FolderDeleteRequested += directory =>
            _assetDeleteConfirmation.Request(directory,
                () => TryContentAction(() => DeleteContentDirectory(directory)));
        root.AddChild(_assetDeleteConfirmation);

        _closeConfirmation = new EditorCloseConfirmationPanel(TrySaveScene);
        root.AddChild(_closeConfirmation);

        _assetErrors = new EditorAssetErrorsPanel(_context.AssetRegistry);
        root.AddChild(_assetErrors);

        // 状态栏
        _statusBar = new EditorStatusBarPanel();
        root.AddChild(_statusBar);

        Root = root;
        _hierarchy.SelectionSetChanged += (targets, primary) => SelectTargets(targets, primary);
        _context.Selection.Changed += _ => UpdateInspector();
        _context.DirtyChanged += _ => UpdateInspectorTitle();
        _context.WorldChanged += (_, _) => SwitchOutlinerWorld(captureCurrentSelection: false);
        _context.PlayStateChanged += _ => SwitchOutlinerWorld(
            captureCurrentSelection: !_skipOutlinerSelectionCapture);
    }

    /// <summary>当前编辑器 Play 状态，供宿主同步窗口标题或工具栏。</summary>
    public EditorPlayState PlayState => _context.PlayState;
    public object? SelectedTarget => _context.Selection.Selected;
    public IReadOnlyList<object> SelectedTargets => _context.Selection.Items;
    public IReadOnlyList<EditorInspectorResourceProperty> InspectorResourceProperties
        => _inspector.ResourceProperties;
    /// <summary>当前场景服务提供的最近场景路径；非 Binary 服务返回空列表。</summary>
    public IReadOnlyList<string> RecentScenePaths
        => (_sceneService as BinaryEditorSceneService)?.RecentFiles.Paths ?? Array.Empty<string>();
    public string? CurrentScenePath => (_sceneService as BinaryEditorSceneService)?.Path;
    public bool IsDirty => _context.IsDirty;

    /// <summary>
    /// 请求关闭编辑器。无脏数据时立即执行关闭回调；有脏数据时显示确认对话框。
    /// 宿主应在窗口关闭入口调用此方法。
    /// </summary>
    public bool RequestClose(Action close)
    {
        ArgumentNullException.ThrowIfNull(close);
        if (!_context.IsDirty)
        {
            close();
            return true;
        }

        _closeConfirmation.Request(close);
        SetStatus("Choose whether to save the current scene before closing.");
        return false;
    }
    public GizmoOperation ActiveGizmoOperation => _gizmoOperation;
    public GizmoSpace ActiveGizmoSpace => _gizmoSpace;
    public bool IsGizmoDragging => _gizmo.IsDragging;
    public bool GridSnapEnabled => _gizmo.SnapSettings.Enabled;
    public EditorCameraNavigationMode CameraNavigationMode => _cameraController.Mode;
    public Vector3 TranslationSnapIncrement
    {
        get => _gizmo.SnapSettings.TranslationIncrement;
        set => _gizmo.SnapSettings.TranslationIncrement = value;
    }
    public float RotationSnapIncrementDegrees
    {
        get => _gizmo.SnapSettings.RotationIncrementDegrees;
        set => _gizmo.SnapSettings.RotationIncrementDegrees = value;
    }
    public Vector3 ScaleSnapIncrement
    {
        get => _gizmo.SnapSettings.ScaleIncrement;
        set => _gizmo.SnapSettings.ScaleIncrement = value;
    }

    public void ToggleGridSnap()
    {
        _gizmo.SnapSettings.Enabled = !_gizmo.SnapSettings.Enabled;
        _toolbar.SetSnapEnabled(_gizmo.SnapSettings.Enabled);
        SetStatus(_gizmo.SnapSettings.Enabled ? "Grid snapping enabled." : "Grid snapping disabled.");
    }

    /// <summary>注册 RuntimeWorld 创建后的宿主行为恢复逻辑。</summary>
    public void SetRuntimeWorldInitializer(Action<World> initializer)
        => _context.RuntimeWorldInitializer = initializer ?? throw new ArgumentNullException(nameof(initializer));

    /// <summary>注册正式的 RuntimeWorld 行为扩展；行为在场景实例化完成后执行。</summary>
    public void RegisterRuntimeBehavior(Action<World, SceneDocument> behavior)
        => _context.RegisterRuntimeBehavior(behavior);

    /// <summary>编辑器使用的 AssetGuid 注册表，供导入器和宿主登记资源。</summary>
    public IAssetRegistry AssetRegistry => _context.AssetRegistry;
    /// <summary>内容浏览器查询模型，供宿主扩展拖放、预览或自定义资源操作。</summary>
    public EditorContentBrowserModel ContentBrowser => _contentBrowser.Model;
    public string OutlinerSearchText
    {
        get => _hierarchy.SearchText;
        set => _hierarchy.SearchText = value;
    }
    public bool OutlinerShowInternalActors
    {
        get => _hierarchy.ShowInternalActors;
        set => _hierarchy.ShowInternalActors = value;
    }
    public bool OutlinerShowComponents
    {
        get => _hierarchy.ShowComponents;
        set => _hierarchy.ShowComponents = value;
    }
    public bool OutlinerOnlySelected
    {
        get => _hierarchy.OnlySelected;
        set => _hierarchy.OnlySelected = value;
    }
    public EditorOutlinerWorldSource OutlinerWorldSource
    {
        get => _hierarchy.WorldSource;
        set => _hierarchy.WorldSource = value;
    }
    public World OutlinerWorld => _hierarchy.DisplayedWorld;
    public bool IsOutlinerReadOnly => _hierarchy.IsReadOnly;
    /// <summary>项目 Content 的集中式写操作服务；未配置项目时为 null。</summary>
    public EditorAssetOperationService? AssetOperations => _assetOperations;
    /// <summary>当前已经打开的资源编辑器文档。</summary>
    public IReadOnlyList<EditorAssetEditorDocument> OpenAssetEditors => _assetEditorHost.Documents;
    /// <summary>当前激活的资源编辑器；场景视口激活时为 null。</summary>
    public EditorAssetEditorDocument? ActiveAssetEditor => _assetEditorHost.ActiveDocument;

    /// <summary>解析指定资源并在中间文档区打开对应类型的资源编辑器。</summary>
    public bool OpenAssetEditor(Guid assetGuid)
    {
        var record = _context.AssetRegistry.Records.FirstOrDefault(candidate => candidate.AssetGuid == assetGuid);
        if (record == null)
        {
            SetStatus($"Asset '{assetGuid}' is not registered.");
            return false;
        }

        try
        {
            if (!_context.AssetRegistry.TryResolve(assetGuid, out var resource) || resource == null)
            {
                SetStatus($"Asset '{assetGuid}' could not be loaded.");
                return false;
            }

            var savePath = GetAssetSavePath(record);
            Action? save = savePath == null ? null : () => SaveAsset(record, resource, savePath);
            var document = _assetEditorHost.Open(record, resource, save);
            SetStatus($"Opened {document.Kind} '{document.Title}'.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Asset open failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>关闭指定资源的编辑器标签页。</summary>
    public bool CloseAssetEditor(Guid assetGuid) => _assetEditorHost.Close(assetGuid);

    /// <summary>切回场景视口标签页。</summary>
    public void ShowSceneEditor() => _assetEditorHost.ShowScene();

    public void SelectTargets(IEnumerable<object> targets, object? primary = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (primary is EditorActorFolder)
        {
            _context.Selection.Selected = null;
            return;
        }
        var selectable = targets.Where(EditorActorPolicy.CanSelect).ToArray();
        _context.Selection.Set(selectable,
            primary != null && EditorActorPolicy.CanSelect(primary) ? primary : null);
    }

    public bool AssignAssetToSelection(string propertyName, Guid? assetGuid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        SceneResource? resource = null;
        try
        {
            if (assetGuid is { } guid)
                resource = AssetRegistry.Resolve(guid);
            var slots = _context.Selection.Items.Select(target =>
                {
                    var property = target.GetType().GetProperty(
                        propertyName, BindingFlags.Public | BindingFlags.Instance);
                    return property != null && property.CanRead && property.CanWrite &&
                           typeof(SceneResource).IsAssignableFrom(property.PropertyType) &&
                           property.GetCustomAttribute<ScenePropertyAttribute>() != null
                        ? new EditorResourcePropertySlot(target, property)
                        : null;
                })
                .Where(slot => slot != null)
                .Cast<EditorResourcePropertySlot>()
                .ToArray();
            if (slots.Length != _context.Selection.Count || slots.Length == 0)
            {
                SetStatus($"Resource property '{propertyName}' is not common to the current selection.");
                return false;
            }
            if (resource != null && slots.Any(slot => !slot.Property.PropertyType.IsInstanceOfType(resource)))
            {
                SetStatus($"Asset '{assetGuid}' is incompatible with {propertyName}.");
                return false;
            }
            return RequestResourcePropertyEdit(slots, resource);
        }
        catch (Exception ex)
        {
            SetStatus($"Resource assignment failed: {ex.GetBaseException().Message}");
            return false;
        }
    }

    public bool RevealAsset(Guid assetGuid)
    {
        if (!_contentBrowser.RevealAsset(assetGuid))
        {
            SetStatus($"Asset '{assetGuid}' is not registered.");
            return false;
        }
        SetStatus($"Located asset '{assetGuid}'.");
        return true;
    }

    public string CreateContentDirectory(string? parentDirectory, string name)
    {
        try
        {
            var path = RequireAssetOperations().CreateDirectory(parentDirectory, name);
            _contentBrowser.Model.SelectedDirectory = path;
            _contentBrowser.Refresh();
            SetStatus($"Created folder '{path}'.");
            return path;
        }
        catch (Exception ex)
        {
            SetStatus($"Create folder failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>在指定 Content 目录创建空白 Material，并在浏览器中定位新资产。</summary>
    public AssetRecord CreateContentMaterial(string? directory, string name)
    {
        try
        {
            var record = RequireAssetOperations().CreateMaterial(directory, name);
            _contentBrowser.RevealAsset(record.AssetGuid);
            SetStatus($"Created Material '{EditorContentBrowserModel.GetDisplayName(record)}'.");
            return record;
        }
        catch (Exception ex)
        {
            SetStatus($"Create Material failed: {ex.Message}");
            throw;
        }
    }

    public string RenameContentDirectory(string directory, string newName)
    {
        try
        {
            var path = RequireAssetOperations().RenameDirectory(directory, newName);
            PreserveDirectoryAfterMove(directory, path);
            _contentBrowser.Refresh();
            SetStatus($"Renamed folder to '{path}'.");
            return path;
        }
        catch (Exception ex)
        {
            SetStatus($"Rename folder failed: {ex.Message}");
            throw;
        }
    }

    public string MoveContentDirectory(string directory, string destinationDirectory)
    {
        try
        {
            var path = RequireAssetOperations().MoveDirectory(directory, destinationDirectory);
            PreserveDirectoryAfterMove(directory, path);
            _contentBrowser.Refresh();
            SetStatus($"Moved folder to '{path}'.");
            return path;
        }
        catch (Exception ex)
        {
            SetStatus($"Move folder failed: {ex.Message}");
            throw;
        }
    }

    public string CopyContentDirectory(string directory, string destinationDirectory, string? copyName = null)
    {
        try
        {
            var path = RequireAssetOperations().CopyDirectory(directory, destinationDirectory, copyName);
            _contentBrowser.Refresh();
            SetStatus($"Copied folder to '{path}'.");
            return path;
        }
        catch (Exception ex)
        {
            SetStatus($"Copy folder failed: {ex.Message}");
            throw;
        }
    }

    public AssetRecord RenameContentAsset(Guid assetGuid, string newName)
        => RunAssetOperation(
            () => RequireAssetOperations().RenameAsset(assetGuid, newName),
            "Renamed asset");

    public AssetRecord MoveContentAsset(Guid assetGuid, string destinationDirectory)
        => RunAssetOperation(
            () => RequireAssetOperations().MoveAsset(assetGuid, destinationDirectory),
            "Moved asset");

    public AssetRecord CopyContentAsset(Guid assetGuid, string destinationDirectory, string? copyName = null)
        => RunAssetOperation(
            () => RequireAssetOperations().CopyAsset(assetGuid, destinationDirectory, copyName),
            "Copied asset");

    public EditorAssetDeleteResult DeleteContentAsset(Guid assetGuid)
    {
        try
        {
            var result = RequireAssetOperations().DeleteAsset(assetGuid, SceneDocument.Capture(_context.World));
            _assetEditorHost.Close(assetGuid);
            _contentBrowser.Refresh();
            SetStatus($"Deleted asset to recovery storage: {result.RecoveryPath}");
            return result;
        }
        catch (Exception ex)
        {
            SetStatus($"Delete asset failed: {ex.Message}");
            throw;
        }
    }

    public EditorAssetDeleteResult DeleteContentDirectory(string directory)
    {
        try
        {
            var result = RequireAssetOperations().DeleteDirectory(directory, SceneDocument.Capture(_context.World));
            foreach (var assetGuid in result.RemovedAssetGuids)
                _assetEditorHost.Close(assetGuid);
            if (ContentBrowser.SelectedDirectory.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
                ContentBrowser.SelectedDirectory.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase))
                ContentBrowser.SelectedDirectory = EditorContentBrowserModel.GetDirectory(directory);
            _contentBrowser.Refresh();
            SetStatus($"Deleted folder to recovery storage: {result.RecoveryPath}");
            return result;
        }
        catch (Exception ex)
        {
            SetStatus($"Delete folder failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>导入图片为内容浏览器当前目录下的引擎 Texture2D 资产。</summary>
    public AssetRecord ImportTexture(string sourcePath)
    {
        try
        {
            var record = _assetImportService.ImportTexture(sourcePath, RequireProject(), AssetRegistry,
                GetCurrentContentDirectory());
            _contentBrowser.Refresh();
            SetStatus($"Imported texture '{record.SourcePath}'.");
            return record;
        }
        catch (Exception ex)
        {
            SetStatus($"Texture import failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>导入 glTF/GLB StaticMesh 到内容浏览器当前目录，不修改当前场景。</summary>
    public GltfEditorImportResult ImportModel(string sourcePath)
    {
        try
        {
            var result = _assetImportService.ImportGltf(sourcePath, RequireProject(), _context,
                GetCurrentContentDirectory());
            _contentBrowser.Refresh();
            SetStatus($"Imported model '{result.SourcePath}' ({result.Assets.Count} mesh asset(s)).");
            return result;
        }
        catch (Exception ex)
        {
            SetStatus($"Model import failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>处理平台窗口拖入的源资源；导入后的文件落到内容浏览器当前目录。</summary>
    public void HandleFilesDropped(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
            return;
        if (_context.PlayState != EditorPlayState.Edit)
        {
            SetStatus("Stop Play before importing dropped files.");
            return;
        }

        var imported = 0;
        var skipped = 0;
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                switch (Path.GetExtension(path).ToLowerInvariant())
                {
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                        ImportTexture(path);
                        imported++;
                        break;
                    case ".gltf":
                    case ".glb":
                        ImportModel(path);
                        imported++;
                        break;
                    default:
                        skipped++;
                        break;
                }
            }
            catch
            {
                skipped++;
            }
        }

        SetStatus(imported == 0
            ? "No supported resource files were imported."
            : skipped == 0
                ? $"Imported {imported} resource file(s)."
                : $"Imported {imported} resource file(s); skipped {skipped}.");
    }

    private EditorProject RequireProject()
        => Project ?? throw new InvalidOperationException("Editor project root is not configured.");

    private EditorAssetOperationService RequireAssetOperations()
        => _assetOperations ?? throw new InvalidOperationException(
            "Editor asset operations require a configured project and mutable AssetRegistry.");

    private AssetRecord RunAssetOperation(Func<AssetRecord> operation, string successVerb)
    {
        try
        {
            var record = operation();
            _contentBrowser.Refresh();
            SetStatus($"{successVerb} '{EditorContentBrowserModel.GetDisplayName(record)}'.");
            return record;
        }
        catch (Exception ex)
        {
            SetStatus($"Asset operation failed: {ex.Message}");
            throw;
        }
    }

    private void PreserveDirectoryAfterMove(string oldDirectory, string newDirectory)
    {
        var selected = ContentBrowser.SelectedDirectory;
        if (selected.Equals(oldDirectory, StringComparison.OrdinalIgnoreCase))
            ContentBrowser.SelectedDirectory = newDirectory;
        else if (selected.StartsWith(oldDirectory + "/", StringComparison.OrdinalIgnoreCase))
            ContentBrowser.SelectedDirectory = newDirectory + selected[oldDirectory.Length..];
    }

    private static void TryContentAction(Action action)
    {
        try { action(); }
        catch
        {
            // Public operation methods already publish a precise status message.
            // UI callbacks stop the exception here so one invalid file operation cannot abort the editor tick.
        }
    }

    private string GetCurrentContentDirectory()
    {
        var project = RequireProject();
        var relativeDirectory = _contentBrowser.Model.SelectedDirectory;
        if (string.IsNullOrWhiteSpace(relativeDirectory))
            return project.ContentDirectory;
        if (Path.IsPathFullyQualified(relativeDirectory) ||
            relativeDirectory.Split('/', '\\').Any(segment => segment is "" or "." or ".."))
            throw new InvalidDataException("The selected content directory is invalid.");

        var directory = Path.GetFullPath(Path.Combine(project.ContentDirectory,
            relativeDirectory.Replace('/', Path.DirectorySeparatorChar)));
        var contentRoot = Path.GetFullPath(project.ContentDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The selected content directory is outside the project Content root.");
        return directory;
    }

    /// <summary>扫描指定目录中的引擎 `.asset` 文件并刷新内容浏览器。</summary>
    public int ScanAssetDirectory(string directory)
    {
        try
        {
            var count = (_context.AssetRegistry as AssetRegistry)?.ScanDirectory(directory) ?? 0;
            _contentBrowser.Refresh();
            SetStatus($"Content refreshed: {count} asset file(s).");
            return count;
        }
        catch (Exception ex)
        {
            SetStatus($"Content refresh failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>切换编辑器 Play/Stop，并保持运行时 World 与编辑 World 生命周期隔离。</summary>
    public void TogglePlay()
    {
        try
        {
            _cameraController.Cancel();
            if (_context.PlayState == EditorPlayState.Play)
            {
                CaptureOutlinerSelection();
                _context.Selection.Set(Array.Empty<object>());
                _skipOutlinerSelectionCapture = true;
                _context.Stop();
                _skipOutlinerSelectionCapture = false;
                SetStatus("Play stopped.");
            }
            else
            {
                _context.Play();
                SetStatus("Play started.");
            }
        }
        catch (Exception ex)
        {
            _skipOutlinerSelectionCapture = false;
            SetStatus($"Play failed: {ex.Message}");
        }
    }

    private Action? _openControlTests;

    /// <summary>绑定宿主提供的控件测试窗口入口。</summary>
    public void SetControlTestWindowLauncher(Action launcher)
    {
        _openControlTests = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    /// <summary>处理 Canvas 级编辑器快捷键。</summary>
    public void HandleGlobalKey(Key key, KeyMask keysDown, UIElement? focusedElement)
    {
        // 文本输入控件保留编辑快捷键，避免 Delete/F2 或 Ctrl+Z 修改场景。
        if (focusedElement is UITextBox)
            return;
        if (focusedElement is UITreeViewItem && key is Key.F2 or Key.Delete)
            return;

        bool ctrl = keysDown.IsDown(Key.LeftControl) || keysDown.IsDown(Key.RightControl);
        bool shift = keysDown.IsDown(Key.LeftShift) || keysDown.IsDown(Key.RightShift);
        bool alt = keysDown.IsDown(Key.LeftAlt) || keysDown.IsDown(Key.RightAlt);
        if (!shift && !alt && TryGetBookmarkSlot(key, out var bookmarkSlot))
        {
            HandleCameraBookmark(bookmarkSlot, ctrl);
            return;
        }
        switch (key)
        {
            case Key.Z when ctrl:
                Undo();
                break;
            case Key.Y when ctrl:
                Redo();
                break;
            case Key.S when ctrl:
                SaveScene();
                break;
            case Key.R when ctrl && shift:
                RefreshContent();
                break;
            case Key.R when ctrl:
                ReloadScene();
                break;
            case Key.Q when !ctrl && !shift && !alt:
                SetSelectTool();
                break;
            case Key.W when !ctrl && !shift && !alt:
                SetGizmoOperation(GizmoOperation.Move);
                break;
            case Key.E when !ctrl && !shift && !alt:
                SetGizmoOperation(GizmoOperation.Rotate);
                break;
            case Key.R when !ctrl && !shift && !alt:
                SetGizmoOperation(GizmoOperation.Scale);
                break;
            case Key.Delete:
                DeleteSelection();
                break;
            case Key.F2:
                RenameSelection();
                break;
            case Key.F:
                FocusSelectionInViewport();
                break;
        }
    }

    private void SetStatus(string message) => _statusBar.SetStatus(message);

    private void RefreshContent()
    {
        if (Project != null && _context.AssetRegistry is AssetRegistry registry)
        {
            var count = registry.ScanDirectory(Project.ContentDirectory);
            _contentBrowser.Refresh();
            SetStatus($"Content refreshed: {count} asset file(s).");
            return;
        }
        _contentBrowser.Refresh();
        SetStatus($"Content refreshed: {_contentBrowser.Model.Entries.Count} visible asset(s).");
    }

    private void HandleAssetActivated(AssetRecord record)
    {
        OpenAssetEditor(record.AssetGuid);
    }

    private string? GetAssetSavePath(AssetRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.CookedPath))
            return Path.GetFullPath(record.CookedPath);
        if (!string.IsNullOrWhiteSpace(record.LoaderSourcePath))
            return Path.GetFullPath(record.LoaderSourcePath);
        if (Project == null || string.IsNullOrWhiteSpace(record.ContentPath))
            return null;

        var relativePath = record.ContentPath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathFullyQualified(relativePath) ||
            relativePath.Split(Path.DirectorySeparatorChar).Any(segment => segment is "" or "." or ".."))
            return null;
        var path = Path.GetFullPath(Path.Combine(Project.ContentDirectory, relativePath));
        var root = Path.GetFullPath(Project.ContentDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private void SaveAsset(AssetRecord record, SceneResource resource, string path)
    {
        try
        {
            AssetFileCodec.Save(resource, path);
            record.MarkImported();
            _contentBrowser.Refresh();
            SetStatus($"Saved asset '{EditorContentBrowserModel.GetDisplayName(record)}'.");
        }
        catch (Exception ex)
        {
            SetStatus($"Asset save failed: {ex.Message}");
        }
    }

    private void HandleCameraBookmark(int slot, bool save)
    {
        if (_context.PlayState != EditorPlayState.Edit || _renderViewControl == null)
        {
            SetStatus("Camera bookmarks are available in the editor viewport.");
            return;
        }
        var camera = FindViewportCamera(_renderViewControl.RenderViewId);
        if (camera == null)
        {
            SetStatus("Editor viewport camera is unavailable.");
            return;
        }
        if (save)
        {
            _cameraController.SetBookmark(slot, camera);
            SetStatus($"Camera bookmark {slot} saved.");
            return;
        }
        SetStatus(_cameraController.RecallBookmark(slot, camera)
            ? $"Camera bookmark {slot} restored."
            : $"Camera bookmark {slot} is empty.");
    }

    private static bool TryGetBookmarkSlot(Key key, out int slot)
    {
        slot = (int)key - (int)Key.D0;
        return slot is >= 0 and < EditorCameraController.BookmarkCount;
    }

    private void ShowAssetErrors() => _assetErrors.Show();
    private void Undo()
    {
        try { SetStatus(_context.Undo() ? "Undo completed." : "Nothing to undo."); }
        catch (Exception ex) { SetStatus($"Undo failed: {ex.Message}"); }
    }

    private void Redo()
    {
        try { SetStatus(_context.Redo() ? "Redo completed." : "Nothing to redo."); }
        catch (Exception ex) { SetStatus($"Redo failed: {ex.Message}"); }
    }

    private void SaveScene() => TrySaveScene();

    private bool TrySaveScene()
    {
        if (_sceneService == null)
        {
            SetStatus("No scene service configured.");
            return false;
        }

        try
        {
            if (_sceneService.Save(_context.World))
            {
                _context.MarkSaved();
                SetStatus("Scene saved.");
                return true;
            }
            else
            {
                SetStatus("Scene save was cancelled.");
                return false;
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Scene save failed: {ex.Message}");
            return false;
        }
    }

    private void ReloadScene()
    {
        if (_sceneService == null)
        {
            SetStatus("No scene service configured.");
            return;
        }

        try
        {
            var document = _sceneService.Load();
            if (document != null)
            {
                _context.Reload(document);
                SetStatus("Scene reloaded.");
                Refresh();
            }
            else
            {
                SetStatus("Scene reload was cancelled.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Scene reload failed: {ex.Message}");
        }
    }

    private void RequestPropertyEdit(object target, string propertyName, object? oldValue, object? newValue)
    {
        if (!EditorActorPolicy.CanEdit(target))
        {
            SetStatus("The selected editor object is read-only.");
            return;
        }
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null || !property.CanWrite)
        {
            SetStatus($"Property '{propertyName}' is not editable.");
            return;
        }
        try
        {
            _context.Execute(new PropertyChangeCommand(target, property, oldValue, newValue));
            SetStatus($"Changed {propertyName}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Property change failed: {ex.Message}");
        }
    }

    private void AddActor()
    {
        var actor = new Actor { Name = NextActorName("Actor") };
        _context.Execute(new CreateActorsCommand(_context.World, new[] { actor },
            _context.Outliner, _context.Outliner.CurrentFolderGuid));
        _context.Selection.Selected = actor;
        SetStatus("Actor queued for creation.");
    }

    private void DuplicateSelection()
    {
        var sources = _context.Selection.Items.OfType<Actor>().Distinct().ToArray();
        if (sources.Length == 0)
        {
            SetStatus("Select one or more Actors to duplicate.");
            return;
        }
        if (sources.Any(actor => !EditorActorPolicy.CanDuplicate(actor)))
        {
            SetStatus("The selection contains an Actor that cannot be duplicated.");
            return;
        }
        try
        {
            var clones = _context.CloneActors(sources);
            var usedNames = _context.World.EnumerateActors(includePendingActors: true)
                .Select(actor => actor.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in clones)
            {
                var prefix = string.IsNullOrWhiteSpace(pair.Source.Name)
                    ? pair.Source.GetType().Name + " Copy"
                    : pair.Source.Name + " Copy";
                pair.Copy.Name = NextActorName(prefix, usedNames);
                usedNames.Add(pair.Copy.Name);
            }
            var copies = clones.Select(pair => pair.Copy).ToArray();
            _context.Execute(new CreateActorsCommand(_context.World, copies,
                _context.Outliner, _context.Outliner.CurrentFolderGuid));
            var primary = clones.FirstOrDefault(pair => ReferenceEquals(pair.Source, _selectedTarget)).Copy
                ?? copies[^1];
            _context.Selection.Set(copies, primary);
            SetStatus(copies.Length == 1 ? "Actor duplicated." : $"{copies.Length} Actors duplicated.");
        }
        catch (Exception ex)
        {
            SetStatus($"Duplicate failed: {ex.Message}");
        }
    }

    private void RenameSelection()
    {
        var target = _hierarchy.ActiveTarget is EditorActorFolder activeFolder ? activeFolder : _selectedTarget;
        if (target is not Actor && target is not EditorActorFolder)
        {
            SetStatus("Select an Actor or Folder to rename.");
            return;
        }
        if (target is Actor actor && !EditorActorPolicy.CanEdit(actor))
        {
            SetStatus("The selected Actor is read-only.");
            return;
        }
        if (!_hierarchy.BeginRename(target))
            SetStatus("Rename could not be started.");
    }

    private string NextActorName(string prefix)
    {
        var used = _context.World.EnumerateActors(includePendingActors: true)
            .Select(actor => actor.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return NextActorName(prefix, used);
    }

    private void CreateOutlinerFolder() => CreateOutlinerFolder(_context.Outliner.CurrentFolderGuid);

    private void CreateOutlinerFolder(Guid? parentGuid)
    {
        if (_context.PlayState != EditorPlayState.Edit)
        {
            SetStatus("Stop Play before creating a Folder.");
            return;
        }
        try
        {
            var used = _context.Outliner.Folders
                .Where(folder => folder.ParentFolderGuid == parentGuid)
                .Select(folder => folder.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var name = "New Folder";
            for (var suffix = 2; used.Contains(name); suffix++)
                name = $"New Folder {suffix}";
            var command = new CreateEditorFolderCommand(_context.Outliner, name, parentGuid);
            _context.Execute(command);
            _hierarchy.Refresh();
            _hierarchy.SelectTarget(command.Folder);
            _hierarchy.BeginRename(command.Folder);
            SetStatus("Folder created. Enter a name.");
        }
        catch (Exception ex)
        {
            SetStatus($"Create Folder failed: {ex.Message}");
        }
    }

    private bool CommitOutlinerRename(object target, string value)
    {
        var name = value.Trim();
        if (name.Length == 0)
        {
            SetStatus("Name cannot be empty.");
            return false;
        }
        try
        {
            switch (target)
            {
                case Actor actor when EditorActorPolicy.CanEdit(actor):
                {
                    if (string.Equals(actor.Name, name, StringComparison.Ordinal))
                        return true;
                    var oldName = actor.Name;
                    _context.Execute(new DelegateEditorCommand("Rename Actor",
                        () => actor.Name = name, () => actor.Name = oldName));
                    break;
                }
                case EditorActorFolder folder:
                    if (string.Equals(folder.Name, name, StringComparison.Ordinal))
                        return true;
                    _context.Execute(new RenameEditorFolderCommand(_context.Outliner, folder.FolderGuid, name));
                    break;
                default:
                    return false;
            }
            SetStatus($"Renamed to {name}.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Rename failed: {ex.Message}");
            return false;
        }
    }

    private void MakeCurrentOutlinerFolder(EditorActorFolder folder)
    {
        if (!ReferenceEquals(_context.Outliner.FindFolder(folder.FolderGuid), folder))
            return;
        _context.Outliner.SetCurrentFolder(folder.FolderGuid);
        SetStatus($"Current Folder: {folder.Name}. New Actors will be created here.");
    }

    private void ClearCurrentOutlinerFolder()
    {
        _context.Outliner.SetCurrentFolder(null);
        SetStatus("Current Folder cleared. New Actors will be created at the root.");
    }

    private void SelectOutlinerFolderActors(EditorActorFolder folder)
    {
        var actors = _context.Outliner.GetActorsInFolderSubtree(folder.FolderGuid,
                _context.World.EnumerateActors(includePendingActors: true))
            .Where(EditorActorPolicy.CanSelect).Cast<object>().ToArray();
        _context.Selection.Set(actors, actors.LastOrDefault());
        SetStatus(actors.Length == 0 ? "Folder contains no selectable Actors." : $"Selected {actors.Length} Actor(s).");
    }

    private void DetachOutlinerActor(Actor actor)
    {
        if (actor.RootComponent is not { AttachParent: not null } root)
            return;
        try
        {
            _context.Execute(new DetachComponentCommand(root));
            SetStatus($"Detached '{actor.Name}' while keeping its world transform.");
        }
        catch (Exception ex)
        {
            SetStatus($"Detach failed: {ex.Message}");
        }
    }

    private void MoveActorToCurrentFolder(Actor actor)
    {
        try
        {
            _context.Execute(new MoveActorsToEditorFolderCommand(
                _context.Outliner, new[] { actor }, _context.Outliner.CurrentFolderGuid));
            SetStatus(_context.Outliner.CurrentFolderGuid.HasValue
                ? $"Moved '{actor.Name}' to the current Folder."
                : $"Moved '{actor.Name}' to the root Folder.");
        }
        catch (Exception ex)
        {
            SetStatus($"Move Actor failed: {ex.Message}");
        }
    }

    private void SelectOutlinerActorChildren(Actor actor)
    {
        var actors = _context.World.EnumerateActors(includePendingActors: true).ToArray();
        var descendants = actors.Where(candidate =>
        {
            for (var parent = candidate.RootComponent?.AttachParent?.Owner; parent != null;
                 parent = parent.RootComponent?.AttachParent?.Owner)
            {
                if (ReferenceEquals(parent, actor))
                    return true;
            }
            return false;
        }).Where(EditorActorPolicy.CanSelect).Cast<object>().ToArray();
        _context.Selection.Set(descendants, descendants.LastOrDefault());
        SetStatus(descendants.Length == 0 ? "Actor has no selectable child Actors." : $"Selected {descendants.Length} child Actor(s).");
    }

    private void DeleteOutlinerTarget(object target)
    {
        if (target is Actor actor)
        {
            _context.Selection.Selected = actor;
            DeleteSelection();
            return;
        }
        if (target is not EditorActorFolder folder)
            return;
        try
        {
            _context.Execute(new DeleteEditorFolderCommand(_context.Outliner, folder.FolderGuid,
                _context.World.EnumerateActors(includePendingActors: true)));
            _context.Selection.Selected = null;
            _hierarchy.SelectTarget(null);
            SetStatus("Folder deleted; its contents were moved to the parent Folder.");
        }
        catch (Exception ex)
        {
            SetStatus($"Delete Folder failed: {ex.Message}");
        }
    }

    private void ToggleOutlinerVisibility(object target)
    {
        var world = _hierarchy.DisplayedWorld;
        var outliner = EditorWorldOutlinerData.For(world);
        if (target is Actor actor)
        {
            var hidden = !outliner.IsActorTemporarilyHidden(actor.ActorGuid);
            outliner.SetActorTemporarilyHidden(actor.ActorGuid, hidden);
            SetStatus(hidden ? $"Temporarily hid '{actor.Name}'." : $"Showed '{actor.Name}'.");
        }
        else if (target is EditorActorFolder folder)
        {
            var state = outliner.GetFolderVisibility(folder.FolderGuid,
                world.EnumerateActors(includePendingActors: true));
            var hide = state != EditorVisibilityState.Hidden;
            outliner.SetFolderTemporarilyHidden(folder.FolderGuid,
                world.EnumerateActors(includePendingActors: true), hide);
            SetStatus(hide ? $"Temporarily hid Folder '{folder.Name}'." : $"Showed Folder '{folder.Name}'.");
        }
        _hierarchy.Refresh();
    }

    private void CreateActorFromAssetInFolder(AssetRecord record, EditorActorFolder folder)
    {
        if (_context.PlayState != EditorPlayState.Edit)
            return;
        try
        {
            if (!_context.AssetRegistry.TryResolve(record.AssetGuid, out var resource) || resource is not StaticMesh mesh)
            {
                SetStatus("Only StaticMesh assets can create Actors in an Outliner Folder.");
                return;
            }
            var baseName = Path.GetFileNameWithoutExtension(record.ContentPath ?? record.SourcePath ?? "StaticMesh");
            var actor = new Actor { Name = NextActorName(baseName) };
            actor.AddOwnedComponent(new StaticMeshComponent { Mesh = mesh });
            _context.Execute(new CreateActorsCommand(_context.World, new[] { actor },
                _context.Outliner, folder.FolderGuid));
            _context.Selection.Selected = actor;
            SetStatus($"Created Actor '{actor.Name}' in Folder '{folder.Name}'.");
        }
        catch (Exception ex)
        {
            SetStatus($"Create Actor from asset failed: {ex.Message}");
        }
    }

    private bool RequestResourcePropertyEdit(
        IReadOnlyList<EditorResourcePropertySlot> slots,
        SceneResource? resource)
    {
        if (_context.PlayState != EditorPlayState.Edit)
        {
            SetStatus("Stop Play before editing resource properties.");
            return false;
        }
        if (slots.Count == 0)
            return false;
        if (slots.Any(slot => !EditorActorPolicy.CanEdit(slot.Target)))
        {
            SetStatus("The selection contains a read-only editor object.");
            return false;
        }
        var propertyName = slots[0].Property.Name;
        try
        {
            var changes = slots
                .Where(slot => !SameAsset(slot.Property.GetValue(slot.Target) as SceneResource, resource))
                .Select(slot => (slot.Target, slot.Property, NewValue: (object?)resource))
                .ToArray();
            if (changes.Length == 0)
                return false;
            _context.Execute(new PropertyBatchChangeCommand(propertyName, changes));
            _context.RegisterWorldAssets();
            _inspector.Refresh();
            SetStatus(resource == null
                ? $"Cleared {propertyName} on {changes.Length} object(s)."
                : $"Assigned {EditorContentBrowserModel.GetDisplayName(
                    AssetRegistry.Records.FirstOrDefault(record => record.AssetGuid == resource.AssetGuid)
                    ?? new AssetRecord { AssetGuid = resource.AssetGuid })} to {changes.Length} object(s).");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Resource assignment failed: {ex.GetBaseException().Message}");
            return false;
        }
    }

    private static string NextActorName(string prefix, IReadOnlySet<string> used)
    {
        var candidate = prefix;
        var index = 2;
        while (used.Contains(candidate))
            candidate = $"{prefix} {index++}";
        return candidate;
    }

    private void DeleteSelection()
    {
        if (_hierarchy.ActiveTarget is EditorActorFolder folder)
        {
            DeleteOutlinerTarget(folder);
            return;
        }
        var actors = _context.Selection.Items.OfType<Actor>().Distinct().ToArray();
        if (actors.Length == 0)
        {
            SetStatus("Select one or more Actors to delete.");
            return;
        }
        if (actors.Any(actor => !EditorActorPolicy.CanDelete(actor)))
        {
            SetStatus("The selection contains a protected Actor that cannot be deleted.");
            return;
        }
        _deleteConfirmation.Request(actors);
        SetStatus(actors.Length == 1 ? "Confirm Actor deletion." : $"Confirm deletion of {actors.Length} Actors.");
    }

    private void ConfirmDeleteSelection(IReadOnlyList<Actor> requestedActors)
    {
        var actors = requestedActors
            .Where(_context.World.Actors.Contains)
            .Where(EditorActorPolicy.CanDelete)
            .Distinct()
            .ToArray();
        if (actors.Length == 0)
        {
            SetStatus("Selected Actors are no longer in the scene.");
            return;
        }
        _context.Execute(new DeleteActorsCommand(_context.World, actors));
        _context.Selection.Selected = null;
        SetStatus(actors.Length == 1 ? "Actor queued for deletion." : $"{actors.Length} Actors queued for deletion.");
    }

    /// <summary>创建不进入 World.Actors 的编辑器视口会话。</summary>
    public EditorViewportSession CreateViewportSession(RenderTarget renderTarget)
    {
        var session = new EditorViewportSession(_cameraSnapshotSources, renderTarget);
        _viewportSessions.Add(session);
        return session;
    }

    /// <summary>把渲染视图控件及其会话嵌入中间视口区。</summary>
    public void SetPictureInPicture(UIRenderView control, EditorViewportSession session)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(session);
        if (!_viewportSessions.Contains(session))
            throw new ArgumentException("The viewport session is not owned by this editor UI.", nameof(session));
        if (session.RenderTarget?.Id != control.RenderViewId)
            throw new ArgumentException("The viewport session and render view must use the same render target.", nameof(session));
        _renderViewControl = control;
        control.ClickedWithModifiers += (point, keysDown) => HandleViewportClick(control, point, keysDown);
        control.PointerPressed += point => HandleViewportPointerPressed(control, point);
        control.PointerDragged += point => HandleViewportPointerDragged(control, point);
        control.PointerReleased += point => HandleViewportPointerReleased(control, point);
        control.InputUpdated += (input, deltaTime) => HandleViewportInput(control, input, deltaTime);
        control.OverlayPainter = PaintGizmoOverlay;
        // The render view is the work area, so it must consume the remaining
        // viewport space instead of retaining the old demo thumbnail size.
        control.FixedSize = new UISize(0f, 0f);
        _viewport.SetRenderView(control);
    }

    /// <summary>以渲染视图坐标执行一次 CPU 拾取，并同步层级树和 Inspector 选择。</summary>
    public ViewportHit? PickViewport(
        Vector2 point,
        Vector2 viewportSize,
        CameraComponent camera,
        KeyMask modifiers = default)
    {
        if (_context.PlayState != EditorPlayState.Edit)
            return null;
        var hit = ViewportPicker.Pick(_context.World, camera, point, viewportSize,
            actor => !_context.Outliner.IsActorTemporarilyHidden(actor.ActorGuid));
        bool ctrl = modifiers.IsDown(Key.LeftControl) || modifiers.IsDown(Key.RightControl);
        bool shift = modifiers.IsDown(Key.LeftShift) || modifiers.IsDown(Key.RightShift);
        if (hit?.Component is { } component)
        {
            if (ctrl)
                _context.Selection.Toggle(component);
            else if (shift)
                _context.Selection.Add(component);
            else
                _context.Selection.Selected = component;
        }
        else if (!ctrl && !shift)
        {
            _context.Selection.Selected = null;
        }
        return hit;
    }

    /// <summary>把 StaticMesh 资产实例化到指定视口位置，并作为一次命令加入当前场景。</summary>
    public Actor? PlaceAssetInViewport(
        Guid assetGuid,
        Vector2 point,
        Vector2 viewportSize,
        CameraComponent camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (_context.PlayState != EditorPlayState.Edit)
        {
            SetStatus("Stop Play before placing assets in the scene.");
            return null;
        }
        var isViewportSessionCamera = _viewportSessions.Any(session => ReferenceEquals(session.Camera, camera));
        if (!isViewportSessionCamera && !ReferenceEquals(camera.Owner?.World, _context.World))
        {
            SetStatus("Camera does not belong to this editor viewport or World.");
            return null;
        }

        var record = _context.AssetRegistry.Records.FirstOrDefault(item => item.AssetGuid == assetGuid);
        if (record == null)
        {
            SetStatus($"Asset '{assetGuid}' is not registered.");
            return null;
        }

        try
        {
            if (!_context.AssetRegistry.TryResolve(assetGuid, out var resource) || resource is not StaticMesh mesh)
            {
                SetStatus("Only StaticMesh assets can be placed in the viewport.");
                return null;
            }

            var location = ViewportPicker.FindPlacementPoint(
                _context.World, camera, point, viewportSize);
            location = _gizmo.SnapSettings.SnapTranslationPosition(location);
            var baseName = Path.GetFileNameWithoutExtension(
                record.ContentPath ?? record.SourcePath ?? "StaticMesh");
            var actor = new Actor { Name = NextActorName(baseName) };
            var component = new StaticMeshComponent
            {
                Mesh = mesh,
                RelativeLocation = location,
            };
            actor.AddOwnedComponent(component);
            _context.Execute(new CreateActorsCommand(_context.World, new[] { actor },
                _context.Outliner, _context.Outliner.CurrentFolderGuid));
            _context.Selection.Selected = actor;
            SetStatus($"Placed StaticMesh '{baseName}' in the scene.");
            return actor;
        }
        catch (Exception ex)
        {
            SetStatus($"Asset placement failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>提交一次可撤销的局部变换修改；Gizmo 拖拽应在释放时调用一次。</summary>
    public bool ApplyRelativeTransform(SceneComponent component, Vector3 location, Quaternion rotation, Vector3 scale)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (_context.PlayState != EditorPlayState.Edit ||
            !ReferenceEquals(component.Owner?.World, _context.World) ||
            !EditorActorPolicy.CanEdit(component))
            return false;
        _context.Execute(new TransformChangeCommand(component, location, rotation, scale));
        SetStatus("Transform changed.");
        return true;
    }

    /// <summary>按层级拖放语义挂载当前选择；拖动未选目标时只挂载该目标。</summary>
    public bool AttachSelection(
        object draggedTarget,
        object dropTarget,
        AttachmentTransformRules rules,
        string? socketName = null)
    {
        ArgumentNullException.ThrowIfNull(draggedTarget);
        ArgumentNullException.ThrowIfNull(dropTarget);
        if (_context.PlayState != EditorPlayState.Edit)
            return false;
        var parent = GetSpatialComponent(dropTarget);
        if (parent == null || !IsInEditorWorld(parent) || !EditorActorPolicy.CanEdit(dropTarget))
        {
            SetStatus("Drop target has no SceneComponent.");
            return false;
        }

        var sourceTargets = _context.Selection.Contains(draggedTarget)
            ? _context.Selection.Items
            : new[] { draggedTarget };
        if (sourceTargets.Any(target => !EditorActorPolicy.CanEdit(target)))
        {
            SetStatus("Internal or read-only editor objects cannot be attached.");
            return false;
        }
        var candidates = sourceTargets
            .Select(GetSpatialComponent)
            .Where(component => component != null && IsInEditorWorld(component))
            .Cast<SceneComponent>()
            .Distinct()
            .ToArray();
        var children = candidates
            .Where(candidate => !candidates.Any(other =>
                !ReferenceEquals(other, candidate) && IsAncestor(other, candidate)))
            .ToArray();
        if (children.Length == 0)
        {
            SetStatus("Drag one or more spatial Actors or Components.");
            return false;
        }

        try
        {
            _context.Execute(new AttachComponentsCommand(children, parent, rules, socketName));
            var destination = socketName == null ? parent.GetType().Name : $"{parent.GetType().Name}:{socketName}";
            SetStatus(children.Length == 1
                ? $"Attached component to {destination}."
                : $"Attached {children.Length} components to {destination}.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Attach failed: {ex.Message}");
            return false;
        }
    }

    public bool BeginGizmoDrag(Vector2 point, Vector2 viewportSize, CameraComponent camera,
        GizmoSpace space = GizmoSpace.World)
    {
        var primary = _context.Selection.Selected == null
            ? null
            : GetSpatialComponent(_context.Selection.Selected);
        if (!_transformToolActive || _context.PlayState != EditorPlayState.Edit || primary == null)
            return false;
        var targets = GetTopLevelSelectedSpatialComponents();
        return targets.Count != 0 &&
               _gizmo.BeginDrag(primary, targets, camera, point, viewportSize, _gizmoOperation, space);
    }

    public bool UpdateGizmoDrag(Vector2 point) => _gizmo.UpdateDrag(point);

    public bool EndGizmoDrag()
    {
        var command = _gizmo.EndDrag();
        if (command == null)
            return false;
        _context.Execute(command);
        SetStatus(command.Description + ".");
        return true;
    }

    public void CancelGizmoDrag() => _gizmo.CancelDrag();

    private void HandleViewportClick(UIRenderView control, Vector2 point, KeyMask modifiers)
    {
        if (_suppressViewportClick)
        {
            _suppressViewportClick = false;
            return;
        }
        var targetId = control.RenderViewId;
        var camera = FindViewportCamera(targetId);
        if (camera == null)
            return;

        var localPoint = point - new Vector2(control.Bounds.X, control.Bounds.Y);
        PickViewport(localPoint, new Vector2(control.Bounds.Width, control.Bounds.Height), camera, modifiers);
    }

    private void HandleAssetDropped(AssetRecord record, Vector2 position)
    {
        if (_inspector.TryAcceptAssetDrop(record, position))
            return;
        if (_hierarchy.GetTargetAt(position) is EditorActorFolder folder)
        {
            CreateActorFromAssetInFolder(record, folder);
            return;
        }
        var control = _renderViewControl;
        if (control == null || _assetEditorHost.ActiveDocument != null || !control.Bounds.Contains(position))
            return;
        var camera = FindViewportCamera(control.RenderViewId);
        if (camera == null)
        {
            SetStatus("No camera is bound to the scene viewport.");
            return;
        }

        var localPoint = position - new Vector2(control.Bounds.X, control.Bounds.Y);
        PlaceAssetInViewport(record.AssetGuid, localPoint,
            new Vector2(control.Bounds.Width, control.Bounds.Height), camera);
    }

    private void HandleViewportPointerPressed(UIRenderView control, Vector2 point)
    {
        if (_cameraController.IsNavigating)
            return;
        if (_context.PlayState != EditorPlayState.Edit || _context.Selection.Selected == null ||
            GetSpatialComponent(_context.Selection.Selected) == null)
            return;
        var camera = FindViewportCamera(control.RenderViewId);
        if (camera == null)
            return;
        var localPoint = point - new Vector2(control.Bounds.X, control.Bounds.Y);
        _suppressViewportClick = BeginGizmoDrag(localPoint, new Vector2(control.Bounds.Width, control.Bounds.Height), camera, _gizmoSpace);
    }

    private void HandleViewportPointerDragged(UIRenderView control, Vector2 point)
    {
        if (!_gizmo.IsDragging)
            return;
        var localPoint = point - new Vector2(control.Bounds.X, control.Bounds.Y);
        UpdateGizmoDrag(localPoint);
    }

    private void HandleViewportPointerReleased(UIRenderView control, Vector2 point)
    {
        if (!_gizmo.IsDragging)
            return;
        EndGizmoDrag();
        _suppressViewportClick = true;
    }

    private void HandleViewportInput(UIRenderView control, InputState input, float deltaTime)
    {
        var camera = FindViewportCamera(control.RenderViewId);
        if (camera == null)
            return;
        var alt = input.KeysDown.IsDown(Key.LeftAlt) || input.KeysDown.IsDown(Key.RightAlt);
        if (alt && input.IsButtonPressed(MouseButton.Left))
            _suppressViewportClick = true;
        var pivot = _context.PlayState == EditorPlayState.Edit
            ? GetSelectedSpatialComponents().LastOrDefault()?.WorldTransform.Translation
            : null;
        _cameraController.Update(camera, input, deltaTime, pivot);
    }

    private void FocusSelectionInViewport()
    {
        if (_renderViewControl == null)
            return;
        var camera = FindViewportCamera(_renderViewControl.RenderViewId);
        var targets = GetSelectedSpatialComponents();
        if (camera == null || targets.Count == 0 || !_cameraController.Focus(camera, targets))
        {
            SetStatus("Select a spatial object to focus.");
            return;
        }
        SetStatus(targets.Count == 1 ? "Focused selected object." : $"Focused {targets.Count} selected objects.");
    }

    private IReadOnlyList<SceneComponent> GetSelectedSpatialComponents()
        => _context.Selection.Items
            .Where(EditorActorPolicy.CanEdit)
            .Select(GetSpatialComponent)
            .Where(component => component != null)
            .Cast<SceneComponent>()
            .Distinct()
            .ToArray();

    private IReadOnlyList<SceneComponent> GetTopLevelSelectedSpatialComponents()
    {
        var candidates = GetSelectedSpatialComponents();
        return candidates
            .Where(candidate => !candidates.Any(other =>
                !ReferenceEquals(other, candidate) && IsAncestor(other, candidate)))
            .ToArray();
    }

    private void HandleHierarchyDrop(object draggedTarget, object dropTarget, Vector2 _)
    {
        try
        {
            if (dropTarget is EditorActorFolder destination)
            {
                if (draggedTarget is EditorActorFolder sourceFolder)
                    _context.Execute(new MoveEditorFolderCommand(
                        _context.Outliner, sourceFolder.FolderGuid, destination.FolderGuid));
                else if (draggedTarget is Actor draggedActor)
                {
                    var actors = _context.Selection.Contains(draggedActor)
                        ? _context.Selection.Items.OfType<Actor>()
                        : new[] { draggedActor };
                    _context.Execute(new MoveActorsToEditorFolderCommand(
                        _context.Outliner, actors, destination.FolderGuid));
                }
                else
                    return;
                SetStatus($"Moved selection to Folder '{destination.Name}'.");
                return;
            }
            if (draggedTarget is Actor && dropTarget is Actor)
            {
                AttachSelection(draggedTarget, dropTarget, AttachmentTransformRules.KeepWorldTransform);
                return;
            }
            SetStatus("Drop Actors onto Folders to organize them, or onto Actors to attach them.");
        }
        catch (Exception ex)
        {
            SetStatus($"Outliner move failed: {ex.Message}");
        }
    }

    private void HandleHierarchyBackgroundDrop(object draggedTarget, Vector2 _)
    {
        try
        {
            if (draggedTarget is EditorActorFolder folder)
            {
                _context.Execute(new MoveEditorFolderCommand(_context.Outliner, folder.FolderGuid, null));
                SetStatus($"Moved Folder '{folder.Name}' to the root.");
                return;
            }
            if (draggedTarget is not Actor draggedActor)
                return;
            var actors = (_context.Selection.Contains(draggedActor)
                    ? _context.Selection.Items.OfType<Actor>()
                    : new[] { draggedActor })
                .Distinct().ToArray();
            var commands = new List<IEditorCommand>();
            foreach (var actor in actors)
            {
                if (actor.RootComponent?.AttachParent != null)
                    commands.Add(new DetachComponentCommand(actor.RootComponent));
            }
            commands.Add(new MoveActorsToEditorFolderCommand(_context.Outliner, actors, null));
            _context.Execute(new CompositeEditorCommand("Move Actors to Outliner Root", commands));
            SetStatus(actors.Length == 1 ? "Moved Actor to the Outliner root." : $"Moved {actors.Length} Actors to the Outliner root.");
        }
        catch (Exception ex)
        {
            SetStatus($"Move to Outliner root failed: {ex.Message}");
        }
    }

    private static SceneComponent? GetSpatialComponent(object target)
        => target switch
        {
            SceneComponent component => component,
            Actor actor => actor.RootComponent,
            _ => null,
        };

    private static bool IsAncestor(SceneComponent ancestor, SceneComponent component)
    {
        for (var parent = component.AttachParent; parent != null; parent = parent.AttachParent)
        {
            if (ReferenceEquals(parent, ancestor))
                return true;
        }
        return false;
    }

    private CameraComponent? FindViewportCamera(int targetId)
        => _viewportSessions
            .LastOrDefault(session => !session.IsDisposed && session.IsEnabled && session.RenderTarget?.Id == targetId)
            ?.Camera;

    private void PaintGizmoOverlay(UIManager ui, int targetId, UIRect bounds, int renderViewId)
    {
        var component = _context.Selection.Selected == null
            ? null
            : GetSpatialComponent(_context.Selection.Selected);
        if (!_transformToolActive || _context.PlayState != EditorPlayState.Edit || component == null)
            return;
        var camera = FindViewportCamera(renderViewId);
        if (camera == null || bounds.Width <= 0f || bounds.Height <= 0f)
            return;
        var size = new Vector2(bounds.Width, bounds.Height);
        var segments = _gizmo.GetAxisSegments(component, camera, size, _gizmoSpace);
        var colors = new[]
        {
            new Vector4(0.9f, 0.15f, 0.15f, 1f),
            new Vector4(0.2f, 0.85f, 0.25f, 1f),
            new Vector4(0.2f, 0.45f, 1f, 1f),
        };
        foreach (var segment in segments)
            ui.DrawLine(targetId, new Vector2(bounds.X, bounds.Y) + segment.Start,
                new Vector2(bounds.X, bounds.Y) + segment.End, 3f, colors[(int)segment.Axis]);
        var pivot = new Vector2(bounds.X, bounds.Y) + segments[0].Start;
        ui.DrawRect(targetId, pivot - new Vector2(4f), new Vector2(8f, 8f), Vector4.One);
    }

    private void SetGizmoOperation(GizmoOperation operation)
    {
        _gizmo.CancelDrag();
        _transformToolActive = true;
        _gizmoOperation = operation;
        _toolbar.SetActiveTool(operation);
        SetStatus($"{operation} tool active.");
    }

    private void SetSelectTool()
    {
        _gizmo.CancelDrag();
        _transformToolActive = false;
        _toolbar.SetActiveTool(null);
        SetStatus("Select tool active.");
    }

    /// <summary>每帧调用：层级树按签名重建；状态栏 Actor/组件计数与检查器实时更新。</summary>
    public void Refresh()
    {
        _hierarchy.Refresh();
        _contentBrowser.Refresh();
        RemoveInvalidSelection();
        if (_hierarchy.ActiveTarget is not EditorActorFolder || _context.Selection.Count != 0)
            _hierarchy.SelectTargets(_context.Selection.Items, _context.Selection.Selected);

        int actors = 0, components = 0;
        foreach (var actor in _hierarchy.DisplayedWorld.Actors.Where(EditorActorPolicy.IncludeInLevelStats))
        {
            actors++;
            components += actor.Components.Count();
        }

        _statusBar.SetStatus($"Actors: {actors}  Components: {components}");
        _statusBar.SetSelection(GetSelectionStatus());
        var assetErrorCount = (_context.AssetRegistry as IAssetRegistryDiagnostics)?.Diagnostics.Count ?? 0;
        _statusBar.SetMode(_hierarchy.IsReadOnly
            ? "PLAY · READ ONLY"
            : assetErrorCount == 0 ? "Assets: OK" : $"Asset errors: {assetErrorCount}");

        _inspector.Refresh();
    }

    private void UpdateInspector()
    {
        _selectedTarget = _context.Selection.Selected;
        _inspector.SetTargets(_context.Selection.Items, _selectedTarget);
        _statusBar.SetSelection(GetSelectionStatus());
        UpdateInspectorTitle();
    }

    private void UpdateInspectorTitle()
    {
        var title = _selectedTarget switch
        {
            SceneComponent sceneComponent => sceneComponent.GetType().Name,
            Actor actor => actor.GetType().Name,
            null => "Nothing selected",
            _ => _selectedTarget.GetType().Name,
        };
        if (_hierarchy.IsReadOnly)
            title += " · Read Only";
        _inspector.SetTitle(_context.IsDirty ? $"{title} *" : title);
    }

    private string GetSelectionStatus()
        => _context.Selection.Count switch
        {
            0 => "Nothing selected",
            1 => $"Selected: {_selectedTarget?.GetType().Name}",
            var count => $"Selected: {count} objects (primary: {_selectedTarget?.GetType().Name})",
        };

    private static bool SameAsset(SceneResource? left, SceneResource? right)
        => ReferenceEquals(left, right) || left != null && right != null && left.AssetGuid == right.AssetGuid;

    private void RemoveInvalidSelection()
    {
        var valid = _context.Selection.Items
            .Where(target => IsInWorld(target, _hierarchy.DisplayedWorld))
            .Where(EditorActorPolicy.CanSelect)
            .ToArray();
        var primary = _context.Selection.Selected;
        if (primary != null && !valid.Any(item => ReferenceEquals(item, primary)))
            primary = valid.LastOrDefault();
        _context.Selection.Set(valid, primary);
    }

    private bool IsInEditorWorld(object target)
        => IsInWorld(target, _context.World);

    private static bool IsInWorld(object target, World world)
        => target switch
        {
            Actor actor => world.EnumerateActors(includePendingActors: true)
                .Any(candidate => ReferenceEquals(candidate, actor)),
            SceneComponent component => component.Owner is { } owner &&
                world.EnumerateActors(includePendingActors: true)
                    .Any(candidate => ReferenceEquals(candidate, owner)),
            _ => false,
        };

    private void SwitchOutlinerWorld(bool captureCurrentSelection)
    {
        var previousSelection = _context.Selection.Items.ToArray();
        var previousPrimary = _context.Selection.Selected;
        if (captureCurrentSelection)
            CaptureOutlinerSelection();

        var runtime = _context.RuntimeWorld;
        var showRuntime = _hierarchy.WorldSource == EditorOutlinerWorldSource.ActiveWorld && runtime != null;
        var targetWorld = showRuntime ? runtime! : _context.World;
        var readOnly = _context.PlayState == EditorPlayState.Play;
        if (!ReferenceEquals(_hierarchy.DisplayedWorld, targetWorld) ||
            _hierarchy.IsReadOnly != readOnly || _hierarchy.IsRuntimeView != showRuntime)
            _hierarchy.SetWorld(targetWorld, isReadOnly: readOnly, isRuntimeView: showRuntime);

        var saved = showRuntime ? _runtimeOutlinerSelection : _editorOutlinerSelection;
        var savedPrimary = showRuntime ? _runtimeOutlinerPrimary : _editorOutlinerPrimary;
        var valid = saved.Where(target => IsInWorld(target, targetWorld)).ToArray();
        if (valid.Length == 0 && previousSelection.Length != 0)
        {
            (valid, savedPrimary) = MapSelectionToWorld(previousSelection, previousPrimary, targetWorld);
            if (showRuntime)
            {
                _runtimeOutlinerSelection = valid;
                _runtimeOutlinerPrimary = savedPrimary;
            }
        }
        _context.Selection.Set(valid, savedPrimary);
        _hierarchy.SelectTargets(valid, savedPrimary);
        if (runtime == null)
        {
            _runtimeOutlinerSelection = Array.Empty<object>();
            _runtimeOutlinerPrimary = null;
        }
    }

    private void CaptureOutlinerSelection()
    {
        var items = _context.Selection.Items.ToArray();
        if (ReferenceEquals(_hierarchy.DisplayedWorld, _context.RuntimeWorld))
        {
            _runtimeOutlinerSelection = items;
            _runtimeOutlinerPrimary = _context.Selection.Selected;
        }
        else
        {
            _editorOutlinerSelection = items;
            _editorOutlinerPrimary = _context.Selection.Selected;
        }
    }

    private static (object[] Items, object? Primary) MapSelectionToWorld(
        IReadOnlyList<object> source, object? primary, World destination)
    {
        var actors = destination.EnumerateActors(includePendingActors: true).ToArray();
        var actorsByGuid = actors.ToDictionary(actor => actor.ActorGuid);
        var componentsByGuid = actors.SelectMany(actor => actor.Components)
            .ToDictionary(component => component.ComponentGuid);
        object? Map(object target) => target switch
        {
            Actor actor when actorsByGuid.TryGetValue(actor.ActorGuid, out var match) => match,
            ActorComponent component when componentsByGuid.TryGetValue(component.ComponentGuid, out var match) => match,
            _ => null,
        };
        var mapped = source.Select(Map).Where(item => item != null).Cast<object>().Distinct().ToArray();
        var mappedPrimary = primary == null ? null : Map(primary);
        return (mapped, mappedPrimary != null && mapped.Contains(mappedPrimary) ? mappedPrimary : mapped.LastOrDefault());
    }
}
