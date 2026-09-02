using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器工具栏面板。按钮行为由宿主注入，便于接入命令和快捷键。</summary>
internal sealed class EditorToolbarPanel : UIElement
{
    private readonly UIToolbar _toolbar;

    public EditorToolbarPanel(
        Action select,
        Action move,
        Action rotate,
        Action scale,
        Action addActor,
        Action duplicate,
        Action rename,
        Action delete,
        Action play,
        Action openControlTests)
    {
        _toolbar = new UIToolbar
        {
            FixedSize = new UISize(0f, 34f),
            BackgroundColor = UITheme.Default.PanelBackground,
        };

        _toolbar.AddButton("Select", select);
        _toolbar.AddButton("Move", move);
        _toolbar.AddButton("Rotate", rotate);
        _toolbar.AddButton("Scale", scale);
        _toolbar.AddSeparator();
        _toolbar.AddButton("Add Actor", addActor);
        _toolbar.AddButton("Duplicate", duplicate);
        _toolbar.AddButton("Rename", rename);
        _toolbar.AddButton("Delete", delete);
        _toolbar.AddSeparator();
        _toolbar.AddButton("Play", play);
        _toolbar.AddButton("UI Tests", openControlTests);

        AddChild(_toolbar);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _toolbar.Measure(availableSize);
        return _toolbar.DesiredSize;
    }

    protected override void OnArrange() => _toolbar.Arrange(ContentRect);
}
