using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器菜单面板。菜单只负责把用户操作转发给宿主，不直接修改 World。</summary>
internal sealed class EditorMenuPanel : UIElement
{
    private readonly UIMenuBar _menuBar;

    public EditorMenuPanel(
        Action save,
        Action reload,
        Action undo,
        Action redo,
        Action showAssetErrors,
        Action refreshAssets,
        Action resetLayout,
        Action? backToHub = null)
    {
        _menuBar = new UIMenuBar
        {
            FixedSize = new UISize(0f, 28f),
            BackgroundColor = UITheme.Default.TitleBarBackground,
        };

        _menuBar.AddMenu("File", menu =>
        {
            menu.AddItem(new UIMenuItem("Save Scene", save) { Shortcut = "Ctrl+S" });
            menu.AddItem(new UIMenuItem("Reload", reload) { Shortcut = "Ctrl+R" });
        });
        _menuBar.AddMenu("Edit", menu =>
        {
            menu.AddItem(new UIMenuItem("Undo", undo) { Shortcut = "Ctrl+Z" });
            menu.AddItem(new UIMenuItem("Redo", redo) { Shortcut = "Ctrl+Y" });
        });
        _menuBar.AddMenu("Assets", menu =>
        {
            menu.AddItem(new UIMenuItem("Refresh Content", refreshAssets) { Shortcut = "Ctrl+Shift+R" });
            menu.AddItem(new UIMenuItem("Asset Errors", showAssetErrors));
        });
        _menuBar.AddMenu("Window", menu =>
        {
            menu.AddItem(new UIMenuItem("Reset Layout", resetLayout));
        });

        if (backToHub != null)
        {
            _menuBar.AddChild(new UIButton
            {
                Text = "Back to Hub",
                Padding = UIEdgeInsets.HorizontalVertical(8f, 2f),
                Clicked = backToHub,
            });
        }

        AddChild(_menuBar);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _menuBar.Measure(availableSize);
        return _menuBar.DesiredSize;
    }

    protected override void OnArrange() => _menuBar.Arrange(ContentRect);
}
