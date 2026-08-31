using System.Numerics;
using Spark.Engine.UI;

namespace Demo;

/// <summary>
/// Acceptance: All new editor controls (ScrollBox, ListView, TreeView, TabView, SplitPanel, ComboBox, Toolbar, Dialog, Menu, PropertyGrid).
/// Each scene demonstrates one or two controls with interactive elements.
/// </summary>
public static class EditorControlsVerifyOverlay
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Spacing = 8f,
            Padding = UIEdgeInsets.All(8f),
        };

        root.AddChild(BackBar(switchTo));

        // Description
        root.AddChild(new UILabel
        {
            Text = "New Editor Controls Acceptance (click buttons to test)",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });

        var buttonBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 6f,
            FixedSize = new UISize(0f, 32f),
        };
        buttonBar.AddChild(MakeButton("1 ScrollBox", () => switchTo(ScrollBoxVerify.Build(switchTo))));
        buttonBar.AddChild(MakeButton("2 ListView", () => switchTo(ListViewVerify.Build(switchTo))));
        buttonBar.AddChild(MakeButton("3 TreeView", () => switchTo(TreeViewVerify.Build(switchTo))));
        buttonBar.AddChild(MakeButton("4 TabView", () => switchTo(TabViewVerify.Build(switchTo))));
        buttonBar.AddChild(MakeButton("5 SplitPanel", () => switchTo(SplitPanelVerify.Build(switchTo))));
        buttonBar.AddChild(MakeButton("6 ComboBox", () => switchTo(ComboBoxVerify.Build(switchTo))));
        buttonBar.AddChild(MakeButton("7 Toolbar", () => switchTo(ToolbarVerify.Build(switchTo))));
        buttonBar.AddChild(MakeButton("8 Menu+Dialog", () => switchTo(MenuDialogVerify.Build(switchTo))));
        buttonBar.AddChild(MakeButton("9 PropertyGrid", () => switchTo(PropertyGridVerify.Build(switchTo))));
        root.AddChild(buttonBar);

        // Preview area
        var preview = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(8f),
            BackgroundColor = theme.PanelBackground,
            Spacing = 4f,
        };
        preview.AddChild(new UILabel
        {
            Text = "Controls implemented:",
            TextColor = theme.TextColor,
        });
        preview.AddChild(new UILabel
        {
            Text = "UIScrollBox | UIListView | UITreeView | UITabView | UISplitPanel | UIComboBox | UIToolbar | UIMenuPanel/UIMenuBar | UIDialog | UIPropertyGrid",
            TextColor = theme.TextDimColor,
        });
        preview.AddChild(new UILabel
        {
            Text = "Infrastructure: OnMouseWheel routing (ancestor bubble), ScrollBar drag, keyboard navigation (arrow keys, Tab, Enter, Escape).",
            TextColor = theme.TextDimColor,
        });
        root.AddChild(preview);

        return root;
    }

    private static UIButton MakeButton(string text, Action onClick)
    {
        return new UIButton
        {
            Text = text,
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = onClick,
        };
    }

    /// <summary>
    /// 顶部返回栏。二级页面（EditorControlsVerifyOverlay）不传 <paramref name="backTo"/> → 返回 Hub；
    /// 三级子场景传 <c>() =&gt; EditorControlsVerifyOverlay.Build(switchTo)</c> → 返回二级列表页。
    /// </summary>
    internal static UIElement BackBar(Action<UIElement> switchTo, string? title = null, Func<UIElement>? backTo = null)
    {
        var bar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 8f,
            FixedSize = new UISize(0f, 30f),
        };
        bar.AddChild(new UIButton
        {
            Text = backTo == null ? "<- Back to Hub" : "<- Back",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () => switchTo(backTo?.Invoke() ?? VerifyHub.Build(switchTo)),
        });
        bar.AddChild(new UILabel
        {
            Text = title ?? "Editor Controls Acceptance",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });
        return bar;
    }
}

// ============== Scene 1: ScrollBox ==============

file static class ScrollBoxVerify
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "ScrollBox", () => EditorControlsVerifyOverlay.Build(switchTo)));

        root.AddChild(new UILabel
        {
            Text = "ScrollBox acceptance: scroll many items with mouse wheel or drag scrollbar. Verify: content scrolls, scrollbar moves, clipping works.",
            TextColor = theme.TextDimColor,
        });

        var scrollBox = new UIScrollBox
        {
            ScrollDirection = UIScrollDirection.Vertical,
            BackgroundColor = new Vector4(0.10f, 0.12f, 0.15f, 1f),
            FixedSize = new UISize(0f, 300f),
        };

        var content = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Spacing = 2f,
            Padding = UIEdgeInsets.All(4f),
        };

        for (int i = 1; i <= 50; i++)
        {
            content.AddChild(new UILabel
            {
                Text = $"Item {i}: This is a scrollable content line. Scroll me!",
                TextColor = i % 2 == 0
                    ? new Vector4(0.85f, 0.88f, 0.92f, 1f)
                    : new Vector4(0.70f, 0.74f, 0.80f, 1f),
                FixedSize = new UISize(0f, 24f),
            });
        }

        scrollBox.Content = content;
        root.AddChild(scrollBox);

        // ScrollIntoView test
        var testBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 8f,
            FixedSize = new UISize(0f, 32f),
        };
        testBar.AddChild(new UIButton
        {
            Text = "Scroll to top",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () => scrollBox.ScrollOffset = new Vector2(0f, 0f),
        });
        testBar.AddChild(new UIButton
        {
            Text = "Scroll to bottom",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () =>
            {
                var viewport = new UIRect(0f, 0f, scrollBox.Bounds.Width, scrollBox.Bounds.Height - scrollBox.Padding.Top - scrollBox.Padding.Bottom);
                float maxScroll = System.Math.Max(0f, 50 * 26f + 8f - viewport.Height);
                scrollBox.ScrollOffset = new Vector2(0f, maxScroll);
            },
        });
        testBar.AddChild(new UILabel
        {
            Text = "(use mouse wheel to scroll)",
            TextColor = theme.TextDimColor,
        });
        root.AddChild(testBar);

        return root;
    }
}

// ============== Scene 2: ListView ==============

file static class ListViewVerify
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "ListView", () => EditorControlsVerifyOverlay.Build(switchTo)));

        var statusLabel = new UILabel
        {
            Text = "Selected: (none)",
            TextColor = theme.TextDimColor,
        };
        root.AddChild(statusLabel);

        var listView = new UIListView
        {
            FixedSize = new UISize(0f, 250f),
        };
        listView.SelectionChanged = (item) =>
        {
            statusLabel.Text = item != null
                ? $"Selected: {item.Text}"
                : "Selected: (none)";
        };

        for (int i = 1; i <= 30; i++)
            listView.AddItem($"List Item {i:D2}");

        root.AddChild(listView);

        root.AddChild(new UILabel
        {
            Text = "Verify: click items to select, use Up/Down/Home/End keys. Scroll wheel works. Selection persists on scroll.",
            TextColor = theme.TextDimColor,
        });

        return root;
    }
}

// ============== Scene 3: TreeView ==============

file static class TreeViewVerify
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "TreeView", () => EditorControlsVerifyOverlay.Build(switchTo)));

        var statusLabel = new UILabel
        {
            Text = "Selected: (none)",
            TextColor = theme.TextDimColor,
        };
        root.AddChild(statusLabel);

        var treeView = new UITreeView
        {
            FixedSize = new UISize(0f, 300f),
        };
        treeView.SelectionChanged = (item) =>
        {
            statusLabel.Text = item != null
                ? $"Selected: {item.Text}"
                : "Selected: (none)";
        };

        // Build a sample hierarchy
        var world = new UITreeViewItem("World");
        var actor1 = new UITreeViewItem("Actor_Camera");
        var actor2 = new UITreeViewItem("Actor_Light");
        var actor3 = new UITreeViewItem("Actor_Props");
        actor3.AddSubItem(new UITreeViewItem("StaticMesh_Wall"));
        actor3.AddSubItem(new UITreeViewItem("StaticMesh_Floor"));
        var actor4 = new UITreeViewItem("Actor_Characters");
        actor4.AddSubItem(new UITreeViewItem("SkeletalMesh_Player"));
        actor4.AddSubItem(new UITreeViewItem("SkeletalMesh_NPC_A"));
        actor4.AddSubItem(new UITreeViewItem("SkeletalMesh_NPC_B"));

        world.AddSubItem(actor1);
        world.AddSubItem(actor2);
        world.AddSubItem(actor3);
        world.AddSubItem(actor4);

        treeView.AddRoot(world);
        // Expand by default
        world.IsExpanded = true;
        actor3.IsExpanded = true;
        actor4.IsExpanded = true;
        treeView.RebuildFlatList();

        root.AddChild(treeView);

        root.AddChild(new UILabel
        {
            Text = "Verify: click arrows to expand/collapse, click items to select. Keyboard: Up/Down/Left/Right/Enter/Home/End. Indentation correct.",
            TextColor = theme.TextDimColor,
        });

        // Expand/Collapse all buttons
        var btnBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 8f,
            FixedSize = new UISize(0f, 32f),
        };
        btnBar.AddChild(new UIButton
        {
            Text = "Expand All",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () => treeView.ExpandAll(),
        });
        btnBar.AddChild(new UIButton
        {
            Text = "Collapse All",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () => treeView.CollapseAll(),
        });
        root.AddChild(btnBar);

        return root;
    }
}

// ============== Scene 4: TabView ==============

file static class TabViewVerify
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "TabView", () => EditorControlsVerifyOverlay.Build(switchTo)));

        var tabView = new UITabView
        {
            FixedSize = new UISize(0f, 250f),
        };

        var tab1Content = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(12f),
            Spacing = 4f,
        };
        tab1Content.AddChild(new UILabel
        {
            Text = "Tab 1: Scene",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });
        tab1Content.AddChild(new UILabel
        {
            Text = "This is the scene hierarchy panel.",
            TextColor = theme.TextColor,
        });

        var tab2Content = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(12f),
            Spacing = 4f,
        };
        tab2Content.AddChild(new UILabel
        {
            Text = "Tab 2: Inspector",
            TextColor = new Vector4(0.5f, 1f, 0.5f, 1f),
        });
        tab2Content.AddChild(new UILabel
        {
            Text = "This is the property inspector panel.",
            TextColor = theme.TextColor,
        });

        var tab3Content = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(12f),
            Spacing = 4f,
        };
        tab3Content.AddChild(new UILabel
        {
            Text = "Tab 3: Console (closable)",
            TextColor = new Vector4(1f, 0.8f, 0.3f, 1f),
        });
        tab3Content.AddChild(new UILabel
        {
            Text = "This tab can be closed using the X button.",
            TextColor = theme.TextColor,
        });

        tabView.AddTab(new UITabItem("Scene", tab1Content));
        tabView.AddTab(new UITabItem("Inspector", tab2Content));
        tabView.AddTab(new UITabItem("Console", tab3Content, canClose: true));

        root.AddChild(tabView);

        root.AddChild(new UILabel
        {
            Text = "Verify: click tabs to switch content. Console tab has X close button. Click to close.",
            TextColor = theme.TextDimColor,
        });

        return root;
    }
}

// ============== Scene 5: SplitPanel ==============

file static class SplitPanelVerify
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "SplitPanel", () => EditorControlsVerifyOverlay.Build(switchTo)));

        var splitPanel = new UISplitPanel
        {
            Direction = UISplitDirection.Horizontal,
            SplitRatio = 0.35f,
            FixedSize = new UISize(0f, 300f),
        };

        var leftPanel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(8f),
            BackgroundColor = new Vector4(0.10f, 0.12f, 0.15f, 1f),
            Spacing = 4f,
        };
        leftPanel.AddChild(new UILabel
        {
            Text = "Left Panel",
            TextColor = new Vector4(0.5f, 0.8f, 1f, 1f),
        });
        leftPanel.AddChild(new UILabel
        {
            Text = "This is a tree view or file browser area.",
            TextColor = theme.TextDimColor,
        });

        var rightPanel = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(8f),
            BackgroundColor = new Vector4(0.12f, 0.14f, 0.18f, 1f),
            Spacing = 4f,
        };
        rightPanel.AddChild(new UILabel
        {
            Text = "Right Panel",
            TextColor = new Vector4(0.5f, 1f, 0.5f, 1f),
        });
        rightPanel.AddChild(new UILabel
        {
            Text = "This is the main content area.",
            TextColor = theme.TextColor,
        });

        splitPanel.SetPanels(leftPanel, rightPanel);
        root.AddChild(splitPanel);

        root.AddChild(new UILabel
        {
            Text = "Verify: drag the splitter bar to resize panels. Hover to see color change. Min sizes enforced.",
            TextColor = theme.TextDimColor,
        });

        return root;
    }
}

// ============== Scene 6: ComboBox ==============

file static class ComboBoxVerify
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "ComboBox", () => EditorControlsVerifyOverlay.Build(switchTo)));

        var statusLabel = new UILabel
        {
            Text = "Selected: (none)",
            TextColor = theme.TextDimColor,
        };
        root.AddChild(statusLabel);

        // 说明文字放在 ComboBox 之前，避免覆盖下拉列表（下拉绘制在控件下方）
        root.AddChild(new UILabel
        {
            Text = "Verify: click to open dropdown, click item to select. Arrow keys navigate. Enter/Space/Escape work.",
            TextColor = theme.TextDimColor,
        });

        var comboBox = new UIComboBox
        {
            FixedSize = new UISize(200f, 26f),
        };
        comboBox.AddItem("Option Alpha");
        comboBox.AddItem("Option Beta");
        comboBox.AddItem("Option Gamma");
        comboBox.AddItem("Option Delta");
        comboBox.AddItem("Option Epsilon");
        comboBox.AddItem("Option Zeta");
        comboBox.SelectedItemChanged = (text) =>
        {
            statusLabel.Text = text != null
                ? $"Selected: {text}"
                : "Selected: (none)";
        };
        comboBox.SelectedIndex = 0;

        root.AddChild(comboBox);
        // 注意：下拉列表直接绘制在控件下方（非 Overlay），下方留白以免被后续控件覆盖

        return root;
    }
}

// ============== Scene 7: Toolbar ==============

file static class ToolbarVerify
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "Toolbar", () => EditorControlsVerifyOverlay.Build(switchTo)));

        var statusLabel = new UILabel
        {
            Text = "Click a toolbar button...",
            TextColor = theme.TextDimColor,
        };
        root.AddChild(statusLabel);

        var toolbar = new UIToolbar
        {
            FixedSize = new UISize(0f, 36f),
        };
        toolbar.AddButton("New", () => statusLabel.Text = "Clicked: New");
        toolbar.AddButton("Open", () => statusLabel.Text = "Clicked: Open");
        toolbar.AddButton("Save", () => statusLabel.Text = "Clicked: Save");
        toolbar.AddSeparator();
        toolbar.AddButton("Cut", () => statusLabel.Text = "Clicked: Cut");
        toolbar.AddButton("Copy", () => statusLabel.Text = "Clicked: Copy");
        toolbar.AddButton("Paste", () => statusLabel.Text = "Clicked: Paste");
        toolbar.AddSeparator();
        toolbar.AddButton("?", () => statusLabel.Text = "Clicked: Help");

        root.AddChild(toolbar);

        root.AddChild(new UILabel
        {
            Text = "Verify: click buttons to see status update. Hover to see highlight. Separator lines visible.",
            TextColor = theme.TextDimColor,
        });

        return root;
    }
}

// ============== Scene 8: Menu + Dialog ==============

file static class MenuDialogVerify
{
    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "Menu + Dialog", () => EditorControlsVerifyOverlay.Build(switchTo)));

        var statusLabel = new UILabel
        {
            Text = "Status: idle",
            TextColor = theme.TextDimColor,
        };
        root.AddChild(statusLabel);

        // MenuBar
        var menuBar = new UIMenuBar
        {
            FixedSize = new UISize(0f, 30f),
        };
        menuBar.AddMenu("File", panel =>
        {
            panel.AddItem(new UIMenuItem("New Scene", () => statusLabel.Text = "Menu: File > New Scene"));
            panel.AddItem(new UIMenuItem("Open Scene...", () => statusLabel.Text = "Menu: File > Open Scene"));
            panel.AddSeparator();
            panel.AddItem(new UIMenuItem("Save", () => statusLabel.Text = "Menu: File > Save"));
            panel.AddItem(new UIMenuItem("Save As...", () => statusLabel.Text = "Menu: File > Save As"));
            panel.AddSeparator();
            panel.AddItem(new UIMenuItem("Exit", () => statusLabel.Text = "Menu: File > Exit"));
        });
        menuBar.AddMenu("Edit", panel =>
        {
            panel.AddItem(new UIMenuItem("Undo", () => statusLabel.Text = "Menu: Edit > Undo"));
            panel.AddItem(new UIMenuItem("Redo", () => statusLabel.Text = "Menu: Edit > Redo"));
            panel.AddSeparator();
            panel.AddItem(new UIMenuItem("Cut", () => statusLabel.Text = "Menu: Edit > Cut"));
            panel.AddItem(new UIMenuItem("Copy", () => statusLabel.Text = "Menu: Edit > Copy"));
            panel.AddItem(new UIMenuItem("Paste", () => statusLabel.Text = "Menu: Edit > Paste"));
        });
        menuBar.AddMenu("Help", panel =>
        {
            panel.AddItem(new UIMenuItem("About", () => statusLabel.Text = "Menu: Help > About"));
        });

        root.AddChild(menuBar);

        // Dialog test buttons
        root.AddChild(new UILabel
        {
            Text = "Dialog tests:",
            TextColor = theme.TextColor,
        });

        var dialog = new UIDialog
        {
            Title = "Confirm Action",
            Message = "Are you sure you want to delete the selected item? This action cannot be undone.",
        };
        dialog.Buttons.Add(new UIDialogButton("Cancel", isCancel: true));
        dialog.Buttons.Add(new UIDialogButton("Delete", () => statusLabel.Text = "Dialog: Delete confirmed", isDefault: true));
        dialog.Closed = (idx) =>
        {
            statusLabel.Text = idx switch
            {
                0 => "Dialog: Cancel clicked",
                1 => "Dialog: Delete clicked",
                _ => "Dialog: dismissed",
            };
        };
        // 对话框作为 Overlay 注册（不加入控件树，Show() 时自动注册到画布 Overlays）

        var dialogBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 8f,
            FixedSize = new UISize(0f, 32f),
        };
        UIButton? showButton = null;
        showButton = new UIButton
        {
            Text = "Show Dialog",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () =>
            {
                // 对话框不在控件树中，需要手动设置 Canvas 才能注册 Overlay
                dialog.Canvas = showButton?.FindCanvas();
                dialog.Show();
            },
        };
        dialogBar.AddChild(showButton);
        dialogBar.AddChild(new UIButton
        {
            Text = "Hide Dialog",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () => dialog.Close(-1),
        });
        root.AddChild(dialogBar);

        root.AddChild(new UILabel
        {
            Text = "Verify: menu bar shows dropdown on click. Dialog: overlay + centered panel + buttons. Escape = Cancel, Enter = Delete.",
            TextColor = theme.TextDimColor,
        });

        return root;
    }
}

// ============== Scene 9: PropertyGrid ==============

file static class PropertyGridVerify
{
    // Sample object for property grid
    private sealed class SampleObject
    {
        public string Name { get; set; } = "MyActor";
        public int Health { get; set; } = 100;
        public float Speed { get; set; } = 5.5f;
        public bool IsActive { get; set; } = true;
    }

    public static UIElement Build(Action<UIElement> switchTo)
    {
        var theme = UITheme.Default;
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            BackgroundColor = theme.WindowBackground,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };

        root.AddChild(EditorControlsVerifyOverlay.BackBar(switchTo, "PropertyGrid", () => EditorControlsVerifyOverlay.Build(switchTo)));

        var sample = new SampleObject();

        var statusLabel = new UILabel
        {
            Text = $"Object: {sample.Name}",
            TextColor = theme.TextDimColor,
        };
        root.AddChild(statusLabel);

        var propertyGrid = new UIPropertyGrid
        {
            Target = sample,
            FixedSize = new UISize(0f, 200f),
        };
        propertyGrid.PropertyChanged = (name, value) =>
        {
            statusLabel.Text = $"Changed: {name} = {value}";
        };

        root.AddChild(propertyGrid);

        root.AddChild(new UILabel
        {
            Text = "Verify: property grid shows Name, Health, Speed, IsActive. Click a value to edit. Enter to commit, Escape to cancel.",
            TextColor = theme.TextDimColor,
        });

        // Refresh button
        var refreshBar = new UIStackPanel
        {
            Orientation = UIOrientation.Horizontal,
            Spacing = 8f,
            FixedSize = new UISize(0f, 32f),
        };
        refreshBar.AddChild(new UIButton
        {
            Text = "Refresh",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () => propertyGrid.Refresh(),
        });
        refreshBar.AddChild(new UIButton
        {
            Text = "Change Name",
            Padding = UIEdgeInsets.HorizontalVertical(8f, 4f),
            Clicked = () =>
            {
                sample.Name = "Renamed_" + System.Random.Shared.Next(100);
                propertyGrid.Refresh();
                statusLabel.Text = $"Object: {sample.Name}";
            },
        });
        root.AddChild(refreshBar);

        return root;
    }
}