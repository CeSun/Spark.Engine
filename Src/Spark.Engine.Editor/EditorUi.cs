using System.Numerics;
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

    private object? _selectedTarget;

    /// <summary>编辑器根元素（挂到主窗口画布 Root）。</summary>
    public UIElement Root { get; }

    public EditorUi(World world, Action? backToHub = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        var theme = UITheme.Default;

        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground, // 铺满窗口，遮挡底层 3D 场景
        };

        // 菜单栏（占位项 + 可选返回按钮）
        var menuBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            FixedSize = new UISize(0f, 24f),
            Spacing = 12f,
            Padding = UIEdgeInsets.HorizontalVertical(8f, 3f),
            BackgroundColor = theme.TitleBarBackground,
        };
        menuBar.AddChild(new UILabel { Text = "File", TextColor = theme.TextDimColor });
        menuBar.AddChild(new UILabel { Text = "Edit", TextColor = theme.TextDimColor });
        menuBar.AddChild(new UILabel { Text = "View", TextColor = theme.TextDimColor });
        if (backToHub != null)
        {
            menuBar.AddChild(new UIButton
            {
                Text = "<- Hub",
                Padding = UIEdgeInsets.HorizontalVertical(8f, 2f),
                Clicked = () => backToHub(),
            });
        }
        root.AddChild(menuBar);

        // 中部：层级 + 视口（透明）+ 检查器
        var content = new UIStackPanel { Orientation = UIOrientation.Horizontal };

        _hierarchy = new HierarchyPanel(_world);
        var hierarchyPanel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(220f, 0f),
            BackgroundColor = theme.PanelBackground,
        };
        hierarchyPanel.AddChild(_hierarchy.Element);
        content.AddChild(hierarchyPanel);

        // 视口区（深色背景，UIRenderView 画中画嵌入此处显示 3D 画面）
        _viewportArea = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = new Vector4(0.05f, 0.05f, 0.08f, 1f),
        };
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
        inspector.AddChild(_inspectorTitle);

        _propertyGrid = new UIPropertyGrid
        {
            FixedSize = new UISize(0f, 0f), // 拉伸填满剩余
            BackgroundColor = new Vector4(0f, 0f, 0f, 0f), // 透明：用面板背景
            PropertyChanged = (_, _) => UpdateInspectorTitle(), // 编辑写回后刷新标题计数
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
        statusBar.AddChild(_status);
        root.AddChild(statusBar);

        Root = root;
        _hierarchy.SelectionChanged += _ => UpdateInspector();
    }

    /// <summary>把渲染视图控件嵌入中间视口区（画中画显示引擎画面）。</summary>
    public void SetPictureInPicture(UIRenderView control)
    {
        control.FixedSize = new UISize(320f, 240f); // 固定尺寸，避免测量不确定
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
        _selectedTarget = _hierarchy.SelectedTarget;
        _propertyGrid.Target = _selectedTarget;
        UpdateInspectorTitle();
    }

    private void UpdateInspectorTitle()
    {
        _inspectorTitle.Text = _selectedTarget switch
        {
            SceneComponent sceneComponent => $"{sceneComponent.GetType().Name}  (Loc {sceneComponent.RelativeLocation.X:F1}, {sceneComponent.RelativeLocation.Y:F1}, {sceneComponent.RelativeLocation.Z:F1})",
            Actor actor => $"{actor.GetType().Name}  ({actor.Components.Count()} comps)",
            null => "Inspector",
            _ => _selectedTarget.GetType().Name,
        };
    }
}
