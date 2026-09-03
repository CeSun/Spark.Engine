using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>Content 删除确认；实际引用检查和可恢复删除仍由 EditorAssetOperationService 执行。</summary>
internal sealed class EditorAssetDeleteConfirmationPanel : UIElement
{
    private readonly UIDialog _dialog;
    private Action? _confirmed;

    public EditorAssetDeleteConfirmationPanel()
    {
        Visible = false;
        _dialog = new UIDialog
        {
            Title = "Delete Content",
            MinWidth = 420f,
            MaxWidth = 620f,
        };
        _dialog.Buttons.Add(new UIDialogButton("Cancel", isCancel: true));
        _dialog.Buttons.Add(new UIDialogButton("Move to Trash", isDefault: true));
        _dialog.Closed = OnClosed;
        AddChild(_dialog);
    }

    public void Request(string displayName, Action confirmed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        _confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
        _dialog.Message = $"Delete '{displayName}'? Referenced content will be blocked; successful deletion is recoverable from Saved/Trash.";
        _dialog.Show();
    }

    private void OnClosed(int buttonIndex)
    {
        var confirmed = _confirmed;
        _confirmed = null;
        if (buttonIndex == 1)
            confirmed?.Invoke();
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _dialog.Measure(availableSize);
        return _dialog.DesiredSize;
    }

    protected override void OnArrange() => _dialog.Arrange(ContentRect);
}
