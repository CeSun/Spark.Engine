using System.Numerics;
using System.Reflection;
using Spark.Engine.Resources;
using Spark.Engine.UI;

namespace Spark.Engine.Editor;

internal sealed record EditorResourcePropertySlot(object Target, PropertyInfo Property);

/// <summary>Inspector 中的统一资源引用行：选择、拖入、清空、定位和打开均从这里转发。</summary>
internal sealed class EditorResourcePropertyField : UIElement
{
    private const float ActionWidth = 22f;
    private readonly IReadOnlyList<EditorResourcePropertySlot> _slots;
    private readonly IAssetRegistry _registry;
    private readonly Action<IReadOnlyList<EditorResourcePropertySlot>, SceneResource?> _assign;
    private readonly Action<Guid> _locate;
    private readonly Action<Guid> _open;
    private readonly UIMenuPanel _picker = new() { MinWidth = 220f, MaxWidth = 420f };
    private Vector2 _lastPointerPosition;
    private string _valueText = "None";
    private string? _errorText;
    private Guid? _assetGuid;
    private bool _mixed;

    public EditorResourcePropertyField(
        string propertyName,
        Type resourceType,
        IReadOnlyList<EditorResourcePropertySlot> slots,
        IAssetRegistry registry,
        Action<IReadOnlyList<EditorResourcePropertySlot>, SceneResource?> assign,
        Action<Guid> locate,
        Action<Guid> open)
    {
        PropertyName = propertyName;
        ResourceType = resourceType;
        _slots = slots;
        _registry = registry;
        _assign = assign;
        _locate = locate;
        _open = open;
        Focusable = true;
        FixedSize = new UISize(0f, 30f);
        Refresh();
    }

    public string PropertyName { get; }
    public Type ResourceType { get; }
    public IReadOnlyList<EditorResourcePropertySlot> Slots => _slots;
    public string ValueText => _valueText;
    public string? ErrorText => _errorText;
    public bool IsMixed => _mixed;

    public void Refresh()
    {
        _errorText = null;
        _mixed = false;
        var values = new List<SceneResource?>();
        foreach (var slot in _slots)
        {
            try
            {
                values.Add(slot.Property.GetValue(slot.Target) as SceneResource);
            }
            catch (Exception ex)
            {
                _errorText = $"Read failed: {ex.GetBaseException().Message}";
                _valueText = "Error";
                _assetGuid = null;
                return;
            }
        }

        var first = values.FirstOrDefault();
        _mixed = values.Skip(1).Any(value => !SameAsset(first, value));
        if (_mixed)
        {
            _valueText = "<Multiple Values>";
            _assetGuid = null;
            return;
        }
        if (first == null)
        {
            _valueText = "None";
            _assetGuid = null;
            return;
        }

        _assetGuid = first.AssetGuid;
        var record = _registry.Records.FirstOrDefault(candidate => candidate.AssetGuid == first.AssetGuid);
        if (record == null)
        {
            _valueText = first.AssetGuid.ToString("N");
            _errorText = "Asset is not registered.";
            return;
        }
        _valueText = EditorContentBrowserModel.GetDisplayName(record);
        if (record.ImportStatus == AssetImportStatus.Failed)
            _errorText = record.LastError ?? "Asset failed to load.";
    }

    public bool TryAcceptDrop(AssetRecord record, Vector2 position)
    {
        if (!Bounds.Contains(position))
            return false;
        TryAssignRecord(record);
        return true;
    }

    public void ClosePicker() => _picker.Close();

    protected override UISize OnMeasure(UISize availableSize)
        => new(FixedSize is { } size && size.Width > 0f ? size.Width : 0f, 30f);

    protected override void OnPaint(UIManager ui, int targetId)
    {
        var theme = UITheme.Default;
        ui.DrawRect(targetId, new Vector2(Bounds.X, Bounds.Y), new Vector2(Bounds.Width, Bounds.Height),
            new Vector4(0.10f, 0.12f, 0.15f, 1f));
        var renderer = GetTextRenderer();
        if (renderer == null)
            return;

        var labelWidth = MathF.Min(92f, Bounds.Width * 0.38f);
        var y = Bounds.Y + (Bounds.Height - renderer.LineHeight) * 0.5f;
        renderer.DrawText(ui, targetId, renderer.Truncate(PropertyName, labelWidth - 6f),
            new Vector2(Bounds.X + 4f, y), theme.TextDimColor);

        var actionArea = ActionWidth * 3f;
        var valueWidth = MathF.Max(0f, Bounds.Width - labelWidth - actionArea - 8f);
        var valueColor = _errorText != null ? new Vector4(1f, 0.38f, 0.32f, 1f) : theme.TextColor;
        var display = _errorText == null ? _valueText : $"{_valueText}: {_errorText}";
        renderer.DrawText(ui, targetId, renderer.Truncate(display, valueWidth),
            new Vector2(Bounds.X + labelWidth + 4f, y), valueColor);

        DrawAction(renderer, ui, targetId, 2, "L", _assetGuid.HasValue && !_mixed, theme);
        DrawAction(renderer, ui, targetId, 1, "O", _assetGuid.HasValue && !_mixed, theme);
        DrawAction(renderer, ui, targetId, 0, "X", _assetGuid.HasValue || _mixed, theme);
    }

    protected override void OnMouseMove(Vector2 position) => _lastPointerPosition = position;

    protected override void OnMouseClick()
    {
        var action = GetActionIndex(_lastPointerPosition);
        if (action == 0)
        {
            if (_assetGuid.HasValue || _mixed)
                _assign(_slots, null);
            return;
        }
        if (action == 1)
        {
            if (_assetGuid is { } guid && !_mixed)
                _open(guid);
            return;
        }
        if (action == 2)
        {
            if (_assetGuid is { } guid && !_mixed)
                _locate(guid);
            return;
        }
        ShowPicker();
    }

    private void ShowPicker()
    {
        _picker.Clear();
        _picker.AddItem(new UIMenuItem("None (Clear)", () => _assign(_slots, null)));
        _picker.AddSeparator();
        var candidates = _registry.Records
            .Where(record => record.IsPersistent && IsCompatible(record))
            .OrderBy(EditorContentBrowserModel.GetDisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(record => record.AssetGuid)
            .ToArray();
        if (candidates.Length == 0)
            _picker.AddItem(new UIMenuItem($"No {ResourceType.Name} assets") { IsEnabled = false });
        else
            foreach (var record in candidates)
                _picker.AddItem(new UIMenuItem(
                    EditorContentBrowserModel.GetDisplayName(record),
                    () => TryAssignRecord(record)));
        _picker.Canvas = FindCanvas();
        _picker.Show(new Vector2(Bounds.X, Bounds.Bottom));
    }

    private void TryAssignRecord(AssetRecord record)
    {
        try
        {
            if (!_registry.TryResolve(record.AssetGuid, out var resource) || resource == null)
                throw new InvalidDataException($"Asset '{record.AssetGuid}' could not be loaded.");
            if (!ResourceType.IsInstanceOfType(resource))
                throw new InvalidDataException(
                    $"Asset is {resource.GetType().Name}; {ResourceType.Name} is required.");
            _assign(_slots, resource);
            _errorText = null;
        }
        catch (Exception ex)
        {
            // 拒绝的拖放不改变真实属性，因此保留当前值和定位/打开能力，仅把原因贴在字段旁。
            Refresh();
            _errorText = $"{EditorContentBrowserModel.GetDisplayName(record)}: {ex.GetBaseException().Message}";
        }
    }

    private bool IsCompatible(AssetRecord record)
    {
        if (record.Resource != null)
            return ResourceType.IsInstanceOfType(record.Resource);
        var registeredType = Type.GetType(record.AssetType, throwOnError: false);
        return registeredType != null
            ? ResourceType.IsAssignableFrom(registeredType)
            : string.Equals(EditorContentBrowserModel.GetTypeName(record), ResourceType.Name,
                StringComparison.OrdinalIgnoreCase);
    }

    private int GetActionIndex(Vector2 position)
    {
        if (!Bounds.Contains(position))
            return -1;
        var distanceFromRight = Bounds.Right - position.X;
        if (distanceFromRight < 0f || distanceFromRight >= ActionWidth * 3f)
            return -1;
        return (int)(distanceFromRight / ActionWidth);
    }

    private void DrawAction(TextRenderer renderer, UIManager ui, int targetId, int index, string text,
        bool enabled, UITheme theme)
    {
        var x = Bounds.Right - ActionWidth * (index + 1);
        var color = enabled ? theme.AccentColor : theme.TextDimColor * new Vector4(1f, 1f, 1f, 0.45f);
        renderer.DrawText(ui, targetId, text,
            new Vector2(x + 6f, Bounds.Y + (Bounds.Height - renderer.LineHeight) * 0.5f), color);
    }

    private static bool SameAsset(SceneResource? left, SceneResource? right)
        => ReferenceEquals(left, right) || left != null && right != null && left.AssetGuid == right.AssetGuid;
}
