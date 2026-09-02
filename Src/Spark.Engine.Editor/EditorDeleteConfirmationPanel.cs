using Spark.Engine.Actors;
using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>编辑器删除确认面板。仅负责对话框生命周期，实际删除由宿主回调执行。</summary>
internal sealed class EditorDeleteConfirmationPanel : UIElement
{
    private readonly UIDialog _dialog;
    private readonly Action<IReadOnlyList<Actor>> _confirmed;
    private IReadOnlyList<Actor>? _pendingActors;

    public EditorDeleteConfirmationPanel(Action<IReadOnlyList<Actor>> confirmed)
    {
        _confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
        // The dialog is registered on UICanvas.Overlays when shown. Keep this
        // carrier hidden so it never participates in the editor's layout or
        // paints once as a normal child and again as an overlay.
        Visible = false;
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

    public void Request(IReadOnlyList<Actor> actors)
    {
        ArgumentNullException.ThrowIfNull(actors);
        if (actors.Count == 0)
            throw new ArgumentException("At least one Actor is required.", nameof(actors));
        _pendingActors = actors.ToArray();
        if (actors.Count == 1)
        {
            var actor = actors[0];
            var name = string.IsNullOrWhiteSpace(actor.Name) ? actor.GetType().Name : actor.Name;
            _dialog.Message = $"Delete '{name}' from the current scene?";
        }
        else
        {
            _dialog.Message = $"Delete {actors.Count} Actors from the current scene?";
        }
        _dialog.Show();
    }

    private void OnClosed(int buttonIndex)
    {
        var actors = _pendingActors;
        _pendingActors = null;
        if (buttonIndex == 1 && actors != null)
            _confirmed(actors);
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _dialog.Measure(availableSize);
        return _dialog.DesiredSize;
    }

    protected override void OnArrange() => _dialog.Arrange(ContentRect);
}
