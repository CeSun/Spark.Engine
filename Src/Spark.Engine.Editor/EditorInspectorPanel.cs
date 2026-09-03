using System.Reflection;
using Spark.Engine.Components;
using Spark.Engine.Resources;
using Spark.Engine.UI;

namespace Spark.Engine.Editor;

public sealed record EditorInspectorResourceProperty(
    string Name,
    Type ResourceType,
    string Value,
    bool IsMixed,
    string? Error);

/// <summary>编辑器 Inspector 面板，封装属性网格和标题显示。</summary>
internal sealed class EditorInspectorPanel : UIElement
{
    private readonly UIStackPanel _panel;
    private readonly UILabel _title;
    private readonly UIPropertyGrid _propertyGrid;
    private readonly UIStackPanel _resourceRowsPanel;
    private readonly IAssetRegistry _registry;
    private readonly Action<IReadOnlyList<EditorResourcePropertySlot>, SceneResource?> _resourceEditRequested;
    private readonly Action<Guid> _locateAsset;
    private readonly Action<Guid> _openAsset;
    private readonly List<EditorResourcePropertyField> _resourceFields = [];
    private IReadOnlyList<object> _targets = Array.Empty<object>();

    public EditorInspectorPanel(
        Action<object, string, object?, object?> propertyEditRequested,
        IAssetRegistry registry,
        Action<IReadOnlyList<EditorResourcePropertySlot>, SceneResource?> resourceEditRequested,
        Action<Guid> locateAsset,
        Action<Guid> openAsset)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _resourceEditRequested = resourceEditRequested ?? throw new ArgumentNullException(nameof(resourceEditRequested));
        _locateAsset = locateAsset ?? throw new ArgumentNullException(nameof(locateAsset));
        _openAsset = openAsset ?? throw new ArgumentNullException(nameof(openAsset));
        var theme = UITheme.Default;
        _panel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(260f, 0f),
            Padding = UIEdgeInsets.All(8f),
            Spacing = 4f,
            BackgroundColor = theme.PanelBackground,
        };

        _panel.AddChild(new UILabel { Text = "INSPECTOR", TextColor = theme.TextDimColor });
        _title = new UILabel { Text = "Inspector", TextColor = theme.TextColor };
        _panel.AddChild(_title);

        _resourceRowsPanel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Spacing = 2f,
        };
        _panel.AddChild(_resourceRowsPanel);

        _propertyGrid = new UIPropertyGrid
        {
            FixedSize = new UISize(0f, 0f),
            BackgroundColor = new(0f, 0f, 0f, 0f),
            PropertyEditRequested = propertyEditRequested,
        };
        _panel.AddChild(_propertyGrid);
        AddChild(_panel);
    }

    public object? Target
    {
        get => _propertyGrid.Target;
        set => SetTargets(value == null ? Array.Empty<object>() : new[] { value }, value);
    }

    public IReadOnlyList<object> Targets => _targets;
    public IReadOnlyList<EditorInspectorResourceProperty> ResourceProperties
        => _resourceFields.Select(resourceField => new EditorInspectorResourceProperty(
            resourceField.PropertyName, resourceField.ResourceType, resourceField.ValueText,
            resourceField.IsMixed, resourceField.ErrorText)).ToArray();

    public void SetTargets(IReadOnlyList<object> targets, object? primary)
    {
        ArgumentNullException.ThrowIfNull(targets);
        foreach (var field in _resourceFields)
            field.ClosePicker();
        _targets = targets.Where(target => target != null).Distinct().ToArray();
        _propertyGrid.Target = primary;
        RebuildResourceFields(primary);
    }

    public bool TryAcceptAssetDrop(AssetRecord record, System.Numerics.Vector2 position)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!_panel.Bounds.Contains(position))
            return false;
        return _resourceFields.Any(field => field.TryAcceptDrop(record, position));
    }

    public void Refresh()
    {
        _propertyGrid.Refresh();
        foreach (var field in _resourceFields)
            field.Refresh();
    }

    public void SetTitle(string title) => _title.Text = title;

    protected override UISize OnMeasure(UISize availableSize)
    {
        _panel.Measure(availableSize);
        return _panel.DesiredSize;
    }

    protected override void OnArrange() => _panel.Arrange(ContentRect);

    private void RebuildResourceFields(object? primary)
    {
        _resourceFields.Clear();
        _resourceRowsPanel.ClearChildren();
        if (primary == null || _targets.Count == 0)
            return;

        var primaryProperties = GetResourceProperties(primary.GetType());
        foreach (var primaryProperty in primaryProperties)
        {
            var slots = new List<EditorResourcePropertySlot>(_targets.Count);
            foreach (var target in _targets)
            {
                var property = target.GetType().GetProperty(
                    primaryProperty.Name, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanRead || !property.CanWrite ||
                    !primaryProperty.PropertyType.IsAssignableFrom(property.PropertyType) ||
                    property.GetCustomAttribute<ScenePropertyAttribute>() == null)
                {
                    slots.Clear();
                    break;
                }
                slots.Add(new EditorResourcePropertySlot(target, property));
            }
            if (slots.Count != _targets.Count)
                continue;

            var field = new EditorResourcePropertyField(
                primaryProperty.Name,
                primaryProperty.PropertyType,
                slots,
                _registry,
                _resourceEditRequested,
                _locateAsset,
                _openAsset);
            _resourceFields.Add(field);
            _resourceRowsPanel.AddChild(field);
        }
    }

    private static IReadOnlyList<PropertyInfo> GetResourceProperties(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.CanWrite &&
                property.GetIndexParameters().Length == 0 &&
                typeof(SceneResource).IsAssignableFrom(property.PropertyType) &&
                property.GetCustomAttribute<ScenePropertyAttribute>() != null)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
}
