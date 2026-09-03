using Spark.Engine.UI;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>承载最多四个独立 ViewState 的 Outliner 标签；底层 World 与选择服务仍由 EditorUi 共享。</summary>
internal sealed class EditorOutlinerHost : UIElement
{
    public const int MaximumInstances = 4;

    private readonly UITabView _tabs = new()
    {
        FixedSize = new UISize(0f, 0f),
        TabBarHeight = 24f,
        TabMinWidth = 64f,
        TabMaxWidth = 110f,
    };
    private readonly List<EditorHierarchyPanel> _panels = [];
    private readonly Dictionary<EditorHierarchyPanel, int> _slots = [];
    private readonly World _initialWorld;
    private readonly EditorWorldOutlinerData _initialOutliner;
    private readonly string? _projectDirectory;
    private readonly EditorOutlinerExtensionRegistry _extensions;
    private readonly Action<EditorHierarchyPanel> _configure;
    private EditorHierarchyPanel? _previousActivePanel;

    public EditorOutlinerHost(World world, EditorWorldOutlinerData outliner, string? projectDirectory,
        EditorOutlinerExtensionRegistry extensions, Action<EditorHierarchyPanel> configure)
    {
        _initialWorld = world;
        _initialOutliner = outliner;
        _projectDirectory = projectDirectory;
        _extensions = extensions;
        _configure = configure;
        _tabs.SelectedTabChanged = _ =>
        {
            var next = ActivePanel;
            ActivePanelChanged?.Invoke(_previousActivePanel, next);
            _previousActivePanel = next;
        };
        _tabs.TabClosed = tab =>
        {
            if (tab.Content is EditorHierarchyPanel panel)
            {
                _panels.Remove(panel);
                _slots.Remove(panel);
            }
        };
        AddChild(_tabs);
        CreateInstance();
    }

    public IReadOnlyList<EditorHierarchyPanel> Panels => _panels;
    public EditorHierarchyPanel ActivePanel
        => (EditorHierarchyPanel)(_tabs.SelectedTab?.Content
            ?? throw new InvalidOperationException("Outliner host has no active instance."));
    public int ActiveIndex => _panels.IndexOf(ActivePanel);
    public event Action<EditorHierarchyPanel?, EditorHierarchyPanel>? ActivePanelChanged;

    public bool CreateInstance()
    {
        if (_panels.Count >= MaximumInstances)
            return false;
        var slotIndex = Enumerable.Range(0, MaximumInstances)
            .First(index => !_slots.ContainsValue(index));
        var slot = slotIndex == 0 ? "primary" : $"secondary-{slotIndex}";
        var panel = new EditorHierarchyPanel(_initialWorld, _initialOutliner,
            viewStateStore: _projectDirectory == null
                ? null
                : EditorOutlinerViewStateStore.ForProject(_projectDirectory, slot),
            extensions: _extensions);
        _configure(panel);
        panel.CreateOutlinerRequested = () => CreateInstance();
        if (slotIndex != 0)
            panel.CloseOutlinerRequested = () => CloseInstance(panel);
        _panels.Add(panel);
        _slots.Add(panel, slotIndex);
        _tabs.AddTab(new UITabItem($"Outliner {slotIndex + 1}", panel, canClose: slotIndex != 0)
        {
            Closing = () => slotIndex != 0,
        });
        _tabs.SelectedIndex = _tabs.Tabs.Count - 1;
        return true;
    }

    public bool CloseActiveInstance()
        => ActiveIndex > 0 && CloseInstance(ActivePanel);

    private bool CloseInstance(EditorHierarchyPanel panel)
    {
        var index = _panels.IndexOf(panel);
        if (index <= 0)
            return false;
        _panels.RemoveAt(index);
        _slots.Remove(panel);
        _tabs.RemoveTab(index);
        return true;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _tabs.Measure(availableSize);
        return _tabs.DesiredSize;
    }

    protected override void OnArrange() => _tabs.Arrange(ContentRect);
}
