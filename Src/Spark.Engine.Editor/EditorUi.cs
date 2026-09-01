using System.Numerics;
using System.Reflection;
using Spark.Engine.Actors;
using Spark.Engine.Components;
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
    private readonly HierarchyPanel _hierarchy;
    private readonly UILabel _inspectorTitle;
    private readonly UIPropertyGrid _propertyGrid;
    private readonly UILabel _status;
    private readonly UIStackPanel _viewportArea;
    private readonly UILabel _selectionStatus;
    private readonly UILabel _modeStatus;
    private readonly EditorContext _context;

    private object? _selectedTarget;

    /// <summary>编辑器根元素（挂到主窗口画布 Root）。</summary>
    public UIElement Root { get; }

    public EditorUi(World world, Action? backToHub = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _context = new EditorContext(_world);
        var theme = UITheme.Default;

        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground, // 铺满窗口，遮挡底层 3D 场景
        };

        var menuBar = new UIMenuBar { FixedSize = new UISize(0f, 28f), BackgroundColor = theme.TitleBarBackground };
        menuBar.AddMenu("File", menu =>
        {
            menu.AddItem(new UIMenuItem("Save Scene", () => SetStatus("Save is available through the scene service.")) { Shortcut = "Ctrl+S" });
            menu.AddItem(new UIMenuItem("Reload", () => SetStatus("Reload requested.")) { Shortcut = "Ctrl+R" });
        });
        menuBar.AddMenu("Edit", menu =>
        {
            menu.AddItem(new UIMenuItem("Undo", Undo) { Shortcut = "Ctrl+Z" });
            menu.AddItem(new UIMenuItem("Redo", Redo) { Shortcut = "Ctrl+Y" });
        });
        menuBar.AddMenu("Window", menu =>
        {
            menu.AddItem(new UIMenuItem("Reset Layout", () => SetStatus("Layout reset requested.")));
        });
        if (backToHub != null)
        {
            menuBar.AddChild(new UIButton { Text = "Back to Hub", Padding = UIEdgeInsets.HorizontalVertical(8f, 2f), Clicked = backToHub });
        }
        root.AddChild(menuBar);

        var toolbar = new UIToolbar { FixedSize = new UISize(0f, 34f), BackgroundColor = theme.PanelBackground };
        toolbar.AddButton("Select", () => SetStatus("Select tool active."));
        toolbar.AddButton("Move", () => SetStatus("Move tool active."));
        toolbar.AddButton("Rotate", () => SetStatus("Rotate tool active."));
        toolbar.AddButton("Scale", () => SetStatus("Scale tool active."));
        toolbar.AddSeparator();
        toolbar.AddButton("Add Actor", AddActor);
        toolbar.AddButton("Delete", DeleteSelection);
        toolbar.AddSeparator();
        toolbar.AddButton("Play", () => SetStatus("Play requested."));
        root.AddChild(toolbar);

        // 中部：层级 + 视口（透明）+ 检查器
        var content = new UIStackPanel { Orientation = UIOrientation.Horizontal, FixedSize = new UISize(0f, 0f) };

        _hierarchy = new HierarchyPanel(_world);
        var hierarchyPanel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(220f, 0f),
            BackgroundColor = theme.PanelBackground,
        };
        hierarchyPanel.AddChild(new UILabel { Text = "SCENE HIERARCHY", TextColor = theme.TextDimColor, Padding = UIEdgeInsets.HorizontalVertical(8f, 6f) });
        hierarchyPanel.AddChild(_hierarchy.Element);
        content.AddChild(hierarchyPanel);

        // 视口区（深色背景，UIRenderView 画中画嵌入此处显示 3D 画面）
        _viewportArea = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 0f),
            BackgroundColor = new Vector4(0.05f, 0.05f, 0.08f, 1f),
        };
        _viewportArea.AddChild(new UILabel { Text = "VIEWPORT", TextColor = theme.TextDimColor, Padding = UIEdgeInsets.HorizontalVertical(8f, 6f) });
        content.AddChild(_viewportArea);

        var inspector = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(260f, 0f),
            Padding = UIEdgeInsets.All(8f),
            Spacing = 4f,
            BackgroundColor = theme.PanelBackground,
        };
        _inspectorTitle = new UILabel { Text = "Inspector", TextColor = theme.TextColor };
        inspector.AddChild(new UILabel { Text = "INSPECTOR", TextColor = theme.TextDimColor });
        inspector.AddChild(_inspectorTitle);

        _propertyGrid = new UIPropertyGrid
        {
            FixedSize = new UISize(0f, 0f), // 拉伸填满剩余
            BackgroundColor = new Vector4(0f, 0f, 0f, 0f), // 透明：用面板背景
            PropertyEditRequested = RequestPropertyEdit,
        };
        inspector.AddChild(_propertyGrid);
        content.AddChild(inspector);

        root.AddChild(content);

        // 状态栏
        var statusBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 20f),
            Padding = UIEdgeInsets.HorizontalVertical(8f, 2f),
            BackgroundColor = theme.StatusBarBackground,
        };
        _status = new UILabel { Text = "Ready", TextColor = theme.TextDimColor };
        _selectionStatus = new UILabel { Text = "Nothing selected", TextColor = theme.TextDimColor };
        _modeStatus = new UILabel { Text = "Editor", TextColor = theme.TextDimColor };
        statusBar.AddChild(_status);
        statusBar.AddChild(_selectionStatus);
        statusBar.AddChild(_modeStatus);
        root.AddChild(statusBar);

        Root = root;
        _hierarchy.SelectionChanged += target => _context.Selection.Selected = target;
        _context.Selection.Changed += _ => UpdateInspector();
    }

    private void SetStatus(string message) => _status.Text = message;
    private void Undo() => SetStatus(_context.Undo() ? "Undo completed." : "Nothing to undo.");
    private void Redo() => SetStatus(_context.Redo() ? "Redo completed." : "Nothing to redo.");

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
        var actor = new Actor();
        _context.Execute(new DelegateEditorCommand("Add Actor", () => _world.AddActor(actor), () => _world.RemoveActor(actor)));
        SetStatus("Actor queued for creation.");
    }

    private void DeleteSelection()
    {
        if (_selectedTarget is not Actor actor)
        {
            SetStatus("Select an Actor to delete.");
            return;
        }
        _context.Execute(new DelegateEditorCommand("Delete Actor", () => _world.RemoveActor(actor), () => _world.AddActor(actor)));
        _selectedTarget = null;
        _propertyGrid.Target = null;
        SetStatus("Actor queued for deletion.");
    }

    /// <summary>把渲染视图控件嵌入中间视口区（画中画显示引擎画面）。</summary>
    public void SetPictureInPicture(UIRenderView control)
    {
        ArgumentNullException.ThrowIfNull(control);
        // The render view is the work area, so it must consume the remaining
        // viewport space instead of retaining the old demo thumbnail size.
        control.FixedSize = new UISize(0f, 0f);
        _viewportArea.AddChild(control);
    }

    /// <summary>每帧调用：层级树按签名重建；状态栏 Actor/组件计数与检查器实时更新。</summary>
    public void Refresh()
    {
        _hierarchy.Refresh();

        int actors = 0, components = 0;
        foreach (var actor in _world.Actors)
        {
            actors++;
            components += actor.Components.Count();
        }

        _status.Text = $"Actors: {actors}  Components: {components}";
        _selectionStatus.Text = _selectedTarget == null ? "Nothing selected" : $"Selected: {_selectedTarget.GetType().Name}";

        // 选中对象被移除时清空检查器
        if (_selectedTarget is Actor removedActor && !_world.Actors.Contains(removedActor))
        {
            _selectedTarget = null;
            _propertyGrid.Target = null;
            _inspectorTitle.Text = "Inspector";
        }

        _propertyGrid.Refresh();
    }

    private void UpdateInspector()
    {
        _selectedTarget = _context.Selection.Selected;
        _propertyGrid.Target = _selectedTarget;
        _selectionStatus.Text = _selectedTarget == null ? "Nothing selected" : $"Selected: {_selectedTarget.GetType().Name}";
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
        _inspectorTitle.Text = _context.IsDirty ? $"{title} *" : title;
    }
}
