using Spark.Engine.UI;

namespace Spark.Engine.Editor;

/// <summary>把 Asset Registry 的持久诊断显示为编辑器模态面板。</summary>
internal sealed class EditorAssetErrorsPanel : UIElement
{
    private readonly IAssetRegistryDiagnostics? _diagnostics;
    private readonly UIDialog _dialog;

    public EditorAssetErrorsPanel(IAssetRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _diagnostics = registry as IAssetRegistryDiagnostics;
        Visible = false;
        _dialog = new UIDialog
        {
            Title = "Asset Errors",
            MinWidth = 480f,
            MaxWidth = 720f,
        };
        _dialog.Buttons.Add(new UIDialogButton("Clear", Clear));
        _dialog.Buttons.Add(new UIDialogButton("Close", isDefault: true, isCancel: true));
        AddChild(_dialog);
    }

    public void Show()
    {
        var diagnostics = _diagnostics?.Diagnostics ?? Array.Empty<AssetDiagnostic>();
        if (diagnostics.Count == 0)
        {
            _dialog.Message = "No asset errors.";
        }
        else
        {
            var latest = diagnostics.Last();
            _dialog.Message = $"{diagnostics.Count} error(s). Latest: {Path.GetFileName(latest.Path)} [{latest.Stage}] {latest.Message}";
        }
        _dialog.Show();
    }

    private void Clear() => _diagnostics?.ClearDiagnostics();

    protected override UISize OnMeasure(UISize availableSize)
    {
        _dialog.Measure(availableSize);
        return _dialog.DesiredSize;
    }

    protected override void OnArrange() => _dialog.Arrange(ContentRect);
}
