using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>脏场景关闭确认面板；保存成功后才执行宿主提供的关闭回调。</summary>
internal sealed class EditorCloseConfirmationPanel : UIElement
{
    private readonly UIDialog _dialog;
    private readonly Func<bool> _save;
    private Action? _close;

    public EditorCloseConfirmationPanel(Func<bool> save)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Visible = false;
        _dialog = new UIDialog
        {
            Title = "Unsaved Changes",
            Message = "The current scene has unsaved changes.",
            MinWidth = 420f,
            MaxWidth = 620f,
        };
        _dialog.Buttons.Add(new UIDialogButton("Cancel", isCancel: true));
        _dialog.Buttons.Add(new UIDialogButton("Save", isDefault: true));
        _dialog.Buttons.Add(new UIDialogButton("Don't Save"));
        _dialog.Closed = OnClosed;
        AddChild(_dialog);
    }

    public void Request(Action close)
    {
        _close = close ?? throw new ArgumentNullException(nameof(close));
        _dialog.Show();
    }

    private void OnClosed(int buttonIndex)
    {
        var close = _close;
        _close = null;
        if (buttonIndex == 1)
        {
            if (_save())
                close?.Invoke();
        }
        else if (buttonIndex == 2)
        {
            close?.Invoke();
        }
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _dialog.Measure(availableSize);
        return _dialog.DesiredSize;
    }

    protected override void OnArrange() => _dialog.Arrange(ContentRect);
}
