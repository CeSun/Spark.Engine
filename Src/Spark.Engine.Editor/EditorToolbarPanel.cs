using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器工具栏面板。按钮行为由宿主注入，便于接入命令和快捷键。</summary>
internal sealed class EditorToolbarPanel : UIElement
{
    private readonly UIToolbar _toolbar;
    private readonly UIToolbarButton _selectButton;
    private readonly UIToolbarButton _moveButton;
    private readonly UIToolbarButton _rotateButton;
    private readonly UIToolbarButton _scaleButton;
    private readonly UIToolbarButton _snapButton;

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
        Action toggleSnap)
    {
        _toolbar = new UIToolbar
        {
            FixedSize = new UISize(0f, 34f),
            BackgroundColor = UITheme.Default.PanelBackground,
        };

        _selectButton = _toolbar.AddButton("Select [Q]", select);
        _moveButton = _toolbar.AddButton("Move [W]", move);
        _rotateButton = _toolbar.AddButton("Rotate [E]", rotate);
        _scaleButton = _toolbar.AddButton("Scale [R]", scale);
        _snapButton = _toolbar.AddButton("Snap: On", toggleSnap);
        _snapButton.Tooltip = "Toggle transform grid snapping";
        _toolbar.AddSeparator();
        _toolbar.AddButton("Add Actor", addActor);
        _toolbar.AddButton("Duplicate", duplicate);
        _toolbar.AddButton("Rename", rename);
        _toolbar.AddButton("Delete", delete);
        _toolbar.AddSeparator();
        _toolbar.AddButton("Play", play);

        AddChild(_toolbar);
        SetActiveTool(GizmoOperation.Move);
        SetSnapEnabled(true);
    }

    public void SetActiveTool(GizmoOperation? operation)
    {
        _selectButton.IsChecked = operation == null;
        _moveButton.IsChecked = operation == GizmoOperation.Move;
        _rotateButton.IsChecked = operation == GizmoOperation.Rotate;
        _scaleButton.IsChecked = operation == GizmoOperation.Scale;
    }

    public void SetSnapEnabled(bool enabled)
    {
        _snapButton.Text = enabled ? "Snap: On" : "Snap: Off";
        _snapButton.IsChecked = enabled;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _toolbar.Measure(availableSize);
        return _toolbar.DesiredSize;
    }

    protected override void OnArrange() => _toolbar.Arrange(ContentRect);
}
