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
    private readonly EditorContext _context;
    private readonly IEditorSceneService? _sceneService;

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
            resetLayout: () => SetStatus("Layout reset requested."),
            backToHub));

        _toolbar = new EditorToolbarPanel(
            select: () => SetStatus("Select tool active."),
            move: () => SetStatus("Move tool active."),
            rotate: () => SetStatus("Rotate tool active."),
            scale: () => SetStatus("Scale tool active."),
            addActor: AddActor,
            duplicate: DuplicateSelection,
            rename: RenameSelection,
            delete: DeleteSelection,
            play: TogglePlay,
            openControlTests: () => _openControlTests?.Invoke());
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

    /// <summary>注册 RuntimeWorld 创建后的宿主行为恢复逻辑。</summary>
    public void SetRuntimeWorldInitializer(Action<World> initializer)
        => _context.RuntimeWorldInitializer = initializer ?? throw new ArgumentNullException(nameof(initializer));

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
    private void Undo() => SetStatus(_context.Undo() ? "Undo completed." : "Nothing to undo.");
    private void Redo() => SetStatus(_context.Redo() ? "Redo completed." : "Nothing to redo.");

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
        _context.Execute(new PropertyChangeCommand(target, property, oldValue, newValue));
        SetStatus($"Changed {propertyName}.");
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
