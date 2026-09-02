using Spark.Engine.UI;

namespace Demo;

/// <summary>集合控件验收面板：ListView、TreeView、ComboBox 和 TabView。</summary>
internal sealed class ControlCollectionsPanel : UIElement
{
    private readonly UIStackPanel _panel;

    public ControlCollectionsPanel()
    {
        var theme = UITheme.Default;
        _panel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 350f),
            Padding = UIEdgeInsets.All(10f),
            Spacing = 7f,
            BackgroundColor = theme.PanelBackground,
        };
        _panel.AddChild(new UILabel { Text = "COLLECTION CONTROLS", TextColor = theme.TextColor });

        var columns = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 10f,
            FixedSize = new UISize(0f, 190f),
        };
        columns.AddChild(BuildList());
        columns.AddChild(BuildTree());
        _panel.AddChild(columns);

        var combo = new UIComboBox { FixedSize = new UISize(0f, 28f) };
        combo.AddItem("Unlit");
        combo.AddItem("Lit");
        combo.AddItem("Transparent");
        combo.SelectedIndex = 1;
        _panel.AddChild(combo);

        var tabs = new UITabView { FixedSize = new UISize(0f, 80f) };
        tabs.AddTab(new UITabItem("Overview", new UILabel { Text = "Tab content: Overview", Padding = UIEdgeInsets.All(8f) }));
        tabs.AddTab(new UITabItem("Details", new UILabel { Text = "Tab content: Details", Padding = UIEdgeInsets.All(8f) }));
        _panel.AddChild(tabs);
        AddChild(_panel);
    }

    private static UIListView BuildList()
    {
        var list = new UIListView
        {
            FixedSize = new UISize(0f, 170f),
            BackgroundColor = new(0.08f, 0.10f, 0.13f, 1f),
        };
        list.AddItem("Camera");
        list.AddItem("Directional Light");
        list.AddItem("Static Mesh");
        list.SelectedIndex = 0;
        return list;
    }

    private static UITreeView BuildTree()
    {
        var tree = new UITreeView
        {
            FixedSize = new UISize(0f, 170f),
            BackgroundColor = new(0.08f, 0.10f, 0.13f, 1f),
        };
        var scene = new UITreeViewItem("Scene") { IsExpanded = true };
        scene.AddSubItem(new UITreeViewItem("Environment"));
        var actors = new UITreeViewItem("Actors") { IsExpanded = true };
        actors.AddSubItem(new UITreeViewItem("Camera"));
        actors.AddSubItem(new UITreeViewItem("Light"));
        scene.AddSubItem(actors);
        tree.AddRoot(scene);
        return tree;
    }

    protected override UISize OnMeasure(UISize availableSize)
    {
        _panel.Measure(availableSize);
        return _panel.DesiredSize;
    }

    protected override void OnArrange() => _panel.Arrange(ContentRect);
}
