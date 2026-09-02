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
    private readonly World _world;
    private readonly EditorHierarchyPanel _hierarchy;
    private readonly EditorInspectorPanel _inspector;
    private readonly EditorViewportPanel _viewport;
    private readonly EditorStatusBarPanel _statusBar;
    private readonly EditorToolbarPanel _toolbar;
    private readonly EditorDeleteConfirmationPanel _deleteConfirmation;
    private readonly EditorAssetErrorsPanel _assetErrors;
    private readonly EditorContext _context;
    private readonly IEditorSceneService? _sceneService;
    private readonly TransformGizmoController _gizmo = new();
    private GizmoOperation _gizmoOperation = GizmoOperation.Move;
    private GizmoSpace _gizmoSpace = GizmoSpace.World;
    private bool _suppressViewportClick;

    private object? _selectedTarget;

    /// <summary>编辑器根元素（挂到主窗口画布 Root）。</summary>
    public UIElement Root { get; }

    public EditorUi(World world, Action? backToHub = null, IEditorSceneService? sceneService = null, WorldContext? worldContext = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _sceneService = sceneService;
        _context = new EditorContext(_world, worldContext);
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

        _hierarchy = new EditorHierarchyPanel(_world);
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
        _hierarchy.SelectionChanged += target => _context.Selection.Selected = target;
        _context.Selection.Changed += _ => UpdateInspector();
        _context.DirtyChanged += _ => UpdateInspectorTitle();
    }

    /// <summary>当前编辑器 Play 状态，供宿主同步窗口标题或工具栏。</summary>
    public EditorPlayState PlayState => _context.PlayState;
    public GizmoOperation ActiveGizmoOperation => _gizmoOperation;
    public GizmoSpace ActiveGizmoSpace => _gizmoSpace;
    public bool IsGizmoDragging => _gizmo.IsDragging;
    public bool GridSnapEnabled => _gizmo.SnapSettings.Enabled;
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
        }
    }

    private void SetStatus(string message) => _statusBar.SetStatus(message);
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
            if (_sceneService.Save(_world))
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
            if (_sceneService.Reload(_world))
            {
                _context.MarkReloaded();
                _context.Selection.Selected = null;
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
        _context.Execute(new DelegateEditorCommand("Add Actor", () => _world.AddActor(actor), () => _world.RemoveActor(actor)));
        _context.Selection.Selected = actor;
        SetStatus("Actor queued for creation.");
    }

    private void DuplicateSelection()
    {
        if (_selectedTarget is not Actor source)
        {
            SetStatus("Select an Actor to duplicate.");
            return;
        }
        var prefix = string.IsNullOrWhiteSpace(source.Name) ? source.GetType().Name : source.Name + " Copy";
        var copy = new Actor { Name = NextActorName(prefix) };
        _context.Execute(new DelegateEditorCommand("Duplicate Actor", () => _world.AddActor(copy), () => _world.RemoveActor(copy)));
        _context.Selection.Selected = copy;
        SetStatus("Actor duplicated.");
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
        var used = _world.Actors.Select(actor => actor.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidate = prefix;
        var index = 2;
        while (used.Contains(candidate))
            candidate = $"{prefix} {index++}";
        return candidate;
    }

    private void DeleteSelection()
    {
        if (_selectedTarget is not Actor actor)
        {
            SetStatus("Select an Actor to delete.");
            return;
        }
        _deleteConfirmation.Request(actor);
        SetStatus("Confirm Actor deletion.");
    }

    private void ConfirmDeleteSelection(Actor actor)
    {
        if (!_world.Actors.Contains(actor))
        {
            SetStatus("Actor is no longer in the scene.");
            return;
        }
        _context.Execute(new DelegateEditorCommand("Delete Actor", () => _world.RemoveActor(actor), () => _world.AddActor(actor)));
        _selectedTarget = null;
        _inspector.Target = null;
        SetStatus("Actor queued for deletion.");
    }

    /// <summary>把渲染视图控件嵌入中间视口区（画中画显示引擎画面）。</summary>
    public void SetPictureInPicture(UIRenderView control)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.Clicked += point => HandleViewportClick(control, point);
        control.PointerPressed += point => HandleViewportPointerPressed(control, point);
        control.PointerDragged += point => HandleViewportPointerDragged(control, point);
        control.PointerReleased += point => HandleViewportPointerReleased(control, point);
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
    public ViewportHit? PickViewport(Vector2 point, Vector2 viewportSize, CameraComponent camera)
    {
        if (_context.PlayState != EditorPlayState.Edit)
            return null;
        var hit = ViewportPicker.Pick(_world, camera, point, viewportSize);
        _context.Selection.Selected = hit?.Component;
        return hit;
    }

    /// <summary>提交一次可撤销的局部变换修改；Gizmo 拖拽应在释放时调用一次。</summary>
    public bool ApplyRelativeTransform(SceneComponent component, Vector3 location, Quaternion rotation, Vector3 scale)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (_context.PlayState != EditorPlayState.Edit || !ReferenceEquals(component.Owner?.World, _world))
            return false;
        _context.Execute(new TransformChangeCommand(component, location, rotation, scale));
        SetStatus("Transform changed.");
        return true;
    }

    public bool BeginGizmoDrag(Vector2 point, Vector2 viewportSize, CameraComponent camera,
        GizmoSpace space = GizmoSpace.World)
    {
        if (_context.PlayState != EditorPlayState.Edit || _context.Selection.Selected is not SceneComponent component)
            return false;
        return _gizmo.BeginDrag(component, camera, point, viewportSize, _gizmoOperation, space);
    }

    public bool UpdateGizmoDrag(Vector2 point) => _gizmo.UpdateDrag(point);

    public bool EndGizmoDrag()
    {
        var command = _gizmo.EndDrag();
        if (command == null)
            return false;
        _context.Execute(command);
        SetStatus("Gizmo transform changed.");
        return true;
    }

    public void CancelGizmoDrag() => _gizmo.CancelDrag();

    private void HandleViewportClick(UIRenderView control, Vector2 point)
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
        PickViewport(localPoint, new Vector2(control.Bounds.Width, control.Bounds.Height), camera);
    }

    private void HandleViewportPointerPressed(UIRenderView control, Vector2 point)
    {
        if (_context.PlayState != EditorPlayState.Edit || _context.Selection.Selected is not SceneComponent component)
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

    private CameraComponent? FindViewportCamera(int targetId)
        => _world.EnumerateActors(includePendingActors: true)
            .SelectMany(actor => actor.Components)
            .OfType<CameraComponent>()
            .FirstOrDefault(item => item.RenderTarget?.Id == targetId);

    private void PaintGizmoOverlay(UIManager ui, int targetId, UIRect bounds, int renderViewId)
    {
        if (_context.PlayState != EditorPlayState.Edit || _context.Selection.Selected is not SceneComponent component)
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
        _hierarchy.SelectTarget(_context.Selection.Selected);

        int actors = 0, components = 0;
        foreach (var actor in _world.Actors)
        {
            actors++;
            components += actor.Components.Count();
        }

        _statusBar.SetStatus($"Actors: {actors}  Components: {components}");
        _statusBar.SetSelection(_selectedTarget == null ? "Nothing selected" : $"Selected: {_selectedTarget.GetType().Name}");
        var assetErrorCount = (_context.AssetRegistry as IAssetRegistryDiagnostics)?.Diagnostics.Count ?? 0;
        _statusBar.SetMode(assetErrorCount == 0 ? "Assets: OK" : $"Asset errors: {assetErrorCount}");

        // 选中对象被移除时清空检查器
        if (_selectedTarget is Actor removedActor && !_world.Actors.Contains(removedActor))
        {
            _selectedTarget = null;
            _inspector.Target = null;
            _inspector.SetTitle("Inspector");
        }

        _inspector.Refresh();
    }

    private void UpdateInspector()
    {
        _selectedTarget = _context.Selection.Selected;
        _inspector.Target = _selectedTarget;
        _statusBar.SetSelection(_selectedTarget == null ? "Nothing selected" : $"Selected: {_selectedTarget.GetType().Name}");
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
}
