using System.Numerics;
using System.Reflection;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Input;
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
    private readonly EditorStatusBarPanel _statusBar;
    private readonly EditorToolbarPanel _toolbar;
    private readonly EditorDeleteConfirmationPanel _deleteConfirmation;
    private readonly EditorAssetErrorsPanel _assetErrors;
    private readonly UIMenuPanel _attachMenu = new() { MinWidth = 220f, MaxWidth = 420f };
    private readonly EditorContext _context;
    private readonly IEditorSceneService? _sceneService;
    private readonly TransformGizmoController _gizmo = new();
    private readonly EditorCameraController _cameraController = new();
    private GizmoOperation _gizmoOperation = GizmoOperation.Move;
    private GizmoSpace _gizmoSpace = GizmoSpace.World;
    private bool _suppressViewportClick;
    private UIRenderView? _renderViewControl;

    private object? _selectedTarget;

    /// <summary>编辑器根元素（挂到主窗口画布 Root）。</summary>
    public UIElement Root { get; }

    public EditorUi(World world, Action? backToHub = null, IEditorSceneService? sceneService = null, WorldContext? worldContext = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        _sceneService = sceneService;
        _context = new EditorContext(world, worldContext);
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
            resetLayout: () => SetStatus("Layout reset requested."),
            backToHub));

        _toolbar = new EditorToolbarPanel(
            select: () => SetStatus("Select tool active."),
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

        // 中部：层级 + 视口（透明）+ 检查器
        var content = new UIStackPanel { Orientation = UIOrientation.Horizontal, FixedSize = new UISize(0f, 0f) };

        _hierarchy = new EditorHierarchyPanel(_context.World);
        _hierarchy.ItemDropped += HandleHierarchyDrop;
        content.AddChild(_hierarchy);

        _viewport = new EditorViewportPanel();
        content.AddChild(_viewport);

        _inspector = new EditorInspectorPanel(RequestPropertyEdit);
        content.AddChild(_inspector);

        root.AddChild(content);

        _deleteConfirmation = new EditorDeleteConfirmationPanel(ConfirmDeleteSelection);
        root.AddChild(_deleteConfirmation);

        _assetErrors = new EditorAssetErrorsPanel(_context.AssetRegistry);
        root.AddChild(_assetErrors);

        // 状态栏
        _statusBar = new EditorStatusBarPanel();
        root.AddChild(_statusBar);

        Root = root;
        _hierarchy.SelectionSetChanged += (targets, primary) => _context.Selection.Set(targets, primary);
        _context.Selection.Changed += _ => UpdateInspector();
        _context.DirtyChanged += _ => UpdateInspectorTitle();
        _context.WorldChanged += (_, next) => _hierarchy.SetWorld(next);
    }

    /// <summary>当前编辑器 Play 状态，供宿主同步窗口标题或工具栏。</summary>
    public EditorPlayState PlayState => _context.PlayState;
    public object? SelectedTarget => _context.Selection.Selected;
    public IReadOnlyList<object> SelectedTargets => _context.Selection.Items;
    /// <summary>当前场景服务提供的最近场景路径；非 Binary 服务返回空列表。</summary>
    public IReadOnlyList<string> RecentScenePaths
        => (_sceneService as BinaryEditorSceneService)?.RecentFiles.Paths ?? Array.Empty<string>();
    public string? CurrentScenePath => (_sceneService as BinaryEditorSceneService)?.Path;
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

    /// <summary>切换编辑器 Play/Stop，并保持运行时 World 与编辑 World 生命周期隔离。</summary>
    public void TogglePlay()
    {
        try
        {
            _cameraController.Cancel();
            if (_context.PlayState == EditorPlayState.Play)
            {
                _context.Stop();
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
            case Key.R when ctrl:
                ReloadScene();
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

    private void SaveScene()
    {
        if (_sceneService == null)
        {
            SetStatus("No scene service configured.");
            return;
        }

        try
        {
            if (_sceneService.Save(_context.World))
            {
                _context.MarkSaved();
                SetStatus("Scene saved.");
            }
            else
            {
                SetStatus("Scene save was cancelled.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Scene save failed: {ex.Message}");
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
        _context.Execute(new DelegateEditorCommand("Add Actor", () => _context.World.AddActor(actor), () => _context.World.RemoveActor(actor)));
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
            _context.Execute(new CreateActorsCommand(_context.World, copies));
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
        if (_selectedTarget is not Actor actor)
        {
            SetStatus("Select an Actor to rename.");
            return;
        }
        var oldName = actor.Name;
        var newName = NextActorName(string.IsNullOrWhiteSpace(oldName) ? actor.GetType().Name : oldName);
        _context.Execute(new DelegateEditorCommand("Rename Actor", () => actor.Name = newName, () => actor.Name = oldName));
        SetStatus($"Renamed Actor to {newName}.");
    }

    private string NextActorName(string prefix)
    {
        var used = _context.World.EnumerateActors(includePendingActors: true)
            .Select(actor => actor.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return NextActorName(prefix, used);
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
        var actors = _context.Selection.Items.OfType<Actor>().Distinct().ToArray();
        if (actors.Length == 0)
        {
            SetStatus("Select one or more Actors to delete.");
            return;
        }
        _deleteConfirmation.Request(actors);
        SetStatus(actors.Length == 1 ? "Confirm Actor deletion." : $"Confirm deletion of {actors.Length} Actors.");
    }

    private void ConfirmDeleteSelection(IReadOnlyList<Actor> requestedActors)
    {
        var actors = requestedActors.Where(_context.World.Actors.Contains).Distinct().ToArray();
        if (actors.Length == 0)
        {
            SetStatus("Selected Actors are no longer in the scene.");
            return;
        }
        _context.Execute(new DeleteActorsCommand(_context.World, actors));
        _context.Selection.Selected = null;
        SetStatus(actors.Length == 1 ? "Actor queued for deletion." : $"{actors.Length} Actors queued for deletion.");
    }

    /// <summary>把渲染视图控件嵌入中间视口区（画中画显示引擎画面）。</summary>
    public void SetPictureInPicture(UIRenderView control)
    {
        ArgumentNullException.ThrowIfNull(control);
        _renderViewControl = control;
        control.ClickedWithModifiers += (point, keysDown) => HandleViewportClick(control, point, keysDown);
        control.PointerPressed += point => HandleViewportPointerPressed(control, point);
        control.PointerDragged += point => HandleViewportPointerDragged(control, point);
        control.PointerReleased += point => HandleViewportPointerReleased(control, point);
        control.InputUpdated += (input, deltaTime) => HandleViewportInput(control, input, deltaTime);
        control.OverlayPainter = PaintGizmoOverlay;
        var resizeRequested = control.RenderViewResizeRequested;
        if (resizeRequested != null)
        {
            control.RenderViewResizeRequested = (oldId, width, height) =>
            {
                var newId = resizeRequested(oldId, width, height);
                if (newId > 0)
                    _context.SyncRuntimeCameraTargets();
                return newId;
            };
        }
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
        var hit = ViewportPicker.Pick(_context.World, camera, point, viewportSize);
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

    /// <summary>提交一次可撤销的局部变换修改；Gizmo 拖拽应在释放时调用一次。</summary>
    public bool ApplyRelativeTransform(SceneComponent component, Vector3 location, Quaternion rotation, Vector3 scale)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (_context.PlayState != EditorPlayState.Edit || !ReferenceEquals(component.Owner?.World, _context.World))
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
        if (parent == null || !IsInEditorWorld(parent))
        {
            SetStatus("Drop target has no SceneComponent.");
            return false;
        }

        var sourceTargets = _context.Selection.Contains(draggedTarget)
            ? _context.Selection.Items
            : new[] { draggedTarget };
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
        if (_context.PlayState != EditorPlayState.Edit || primary == null)
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
        => _context.Selection.Items.Select(GetSpatialComponent)
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

    private void HandleHierarchyDrop(object draggedTarget, object dropTarget, Vector2 position)
    {
        var parent = GetSpatialComponent(dropTarget);
        if (parent == null)
        {
            SetStatus("Drop target has no SceneComponent.");
            return;
        }

        if (parent.Sockets.Count == 0)
        {
            AttachSelection(draggedTarget, dropTarget, AttachmentTransformRules.KeepWorldTransform);
            return;
        }

        _attachMenu.Clear();
        AddAttachmentMenuOptions(draggedTarget, dropTarget, socketName: null, "Component");
        _attachMenu.AddSeparator();
        foreach (var socketName in parent.Sockets.Keys.Order(StringComparer.Ordinal))
            AddAttachmentMenuOptions(draggedTarget, dropTarget, socketName, $"Socket {socketName}");
        _attachMenu.Canvas = Root.FindCanvas();
        _attachMenu.Show(position);
        SetStatus("Choose attachment target and transform rule.");
    }

    private void AddAttachmentMenuOptions(object draggedTarget, object dropTarget, string? socketName, string label)
    {
        _attachMenu.AddItem(new UIMenuItem($"{label} - Keep World",
            () => AttachSelection(draggedTarget, dropTarget, AttachmentTransformRules.KeepWorldTransform, socketName)));
        _attachMenu.AddItem(new UIMenuItem($"{label} - Keep Relative",
            () => AttachSelection(draggedTarget, dropTarget, AttachmentTransformRules.KeepRelativeTransform, socketName)));
        _attachMenu.AddItem(new UIMenuItem($"{label} - Snap",
            () => AttachSelection(draggedTarget, dropTarget, AttachmentTransformRules.SnapToTargetIncludingScale, socketName)));
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
        => _context.ActiveWorld.EnumerateActors(includePendingActors: true)
            .SelectMany(actor => actor.Components)
            .OfType<CameraComponent>()
            .FirstOrDefault(item => item.RenderTarget?.Id == targetId);

    private void PaintGizmoOverlay(UIManager ui, int targetId, UIRect bounds, int renderViewId)
    {
        var component = _context.Selection.Selected == null
            ? null
            : GetSpatialComponent(_context.Selection.Selected);
        if (_context.PlayState != EditorPlayState.Edit || component == null)
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
        _gizmoOperation = operation;
        SetStatus($"{operation} tool active.");
    }

    /// <summary>每帧调用：层级树按签名重建；状态栏 Actor/组件计数与检查器实时更新。</summary>
    public void Refresh()
    {
        // 覆盖没有经过 SetPictureInPicture 的宿主 resize 回调，确保下一帧仍指向最新目标。
        _context.SyncRuntimeCameraTargets();
        _hierarchy.Refresh();
        RemoveInvalidSelection();
        _hierarchy.SelectTargets(_context.Selection.Items, _context.Selection.Selected);

        int actors = 0, components = 0;
        foreach (var actor in _context.World.Actors)
        {
            actors++;
            components += actor.Components.Count();
        }

        _statusBar.SetStatus($"Actors: {actors}  Components: {components}");
        _statusBar.SetSelection(GetSelectionStatus());
        var assetErrorCount = (_context.AssetRegistry as IAssetRegistryDiagnostics)?.Diagnostics.Count ?? 0;
        _statusBar.SetMode(assetErrorCount == 0 ? "Assets: OK" : $"Asset errors: {assetErrorCount}");

        _inspector.Refresh();
    }

    private void UpdateInspector()
    {
        _selectedTarget = _context.Selection.Selected;
        _inspector.Target = _selectedTarget;
        _statusBar.SetSelection(GetSelectionStatus());
        UpdateInspectorTitle();
    }

    private void UpdateInspectorTitle()
    {
        var title = _selectedTarget switch
        {
            SceneComponent sceneComponent => $"{sceneComponent.GetType().Name}  (Loc {sceneComponent.RelativeLocation.X:F1}, {sceneComponent.RelativeLocation.Y:F1}, {sceneComponent.RelativeLocation.Z:F1})",
            Actor actor => $"{actor.GetType().Name}  ({actor.Components.Count()} comps)",
            null => "Inspector",
            _ => _selectedTarget.GetType().Name,
        };
        _inspector.SetTitle(_context.IsDirty ? $"{title} *" : title);
    }

    private string GetSelectionStatus()
        => _context.Selection.Count switch
        {
            0 => "Nothing selected",
            1 => $"Selected: {_selectedTarget?.GetType().Name}",
            var count => $"Selected: {count} objects (primary: {_selectedTarget?.GetType().Name})",
        };

    private void RemoveInvalidSelection()
    {
        var valid = _context.Selection.Items.Where(IsInEditorWorld).ToArray();
        var primary = _context.Selection.Selected;
        if (primary != null && !valid.Any(item => ReferenceEquals(item, primary)))
            primary = valid.LastOrDefault();
        _context.Selection.Set(valid, primary);
    }

    private bool IsInEditorWorld(object target)
        => target switch
        {
            Actor actor => _context.World.EnumerateActors(includePendingActors: true)
                .Any(candidate => ReferenceEquals(candidate, actor)),
            SceneComponent component => component.Owner is { } owner &&
                _context.World.EnumerateActors(includePendingActors: true)
                    .Any(candidate => ReferenceEquals(candidate, owner)),
            _ => false,
        };
}
