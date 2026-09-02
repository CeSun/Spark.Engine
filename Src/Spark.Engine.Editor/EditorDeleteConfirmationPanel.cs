using Spark.Engine.Actors;
using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器删除确认面板。仅负责对话框生命周期，实际删除由宿主回调执行。</summary>
internal sealed class EditorDeleteConfirmationPanel : UIElement
{
    private readonly UIDialog _dialog;
    private readonly Action<Actor> _confirmed;
    private Actor? _pendingActor;

    public EditorDeleteConfirmationPanel(Action<Actor> confirmed)
    {
        _confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
        _dialog = new UIDialog
        {
            Title = "Delete Actor",
            MinWidth = 360f,
            MaxWidth = 520f,
        };
        _dialog.Buttons.Add(new UIDialogButton("Cancel", isCancel: true));
        _dialog.Buttons.Add(new UIDialogButton("Delete", isDefault: true));
        _dialog.Closed = OnClosed;
        AddChild(_dialog);
    }

    public void Request(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        _pendingActor = actor;
        var name = string.IsNullOrWhiteSpace(actor.Name) ? actor.GetType().Name : actor.Name;
        _dialog.Message = $"Delete '{name}' from the current scene?";
        _dialog.Show();
    }

    private void OnClosed(int buttonIndex)
    {
        var actor = _pendingActor;
        _pendingActor = null;
        if (buttonIndex == 1 && actor != null)
            _confirmed(actor);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _dialog.Measure(availableSize);
        return _dialog.DesiredSize;
    }

    protected override void OnArrange() => _dialog.Arrange(ContentRect);
}
