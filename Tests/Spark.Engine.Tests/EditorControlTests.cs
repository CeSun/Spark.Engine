using System.Numerics;
using System.Threading;
using Spark.Engine.Input;
using Spark.Engine.UI;
using Xunit;

namespace Spark.Engine.Tests;

/// <summary>
/// 新编辑器控件（UIScrollBox / UIListView / UITreeView / UITabView / UISplitPanel / UIComboBox）的布局与逻辑测试。
/// 不依赖 GPU：只测 Measure/Arrange/HitTest/选择/滚动等纯逻辑。
/// </summary>
public class EditorControlTests
{
    [Fact]
    public void Button_DefaultTextAlignmentIsCenteredWithinContent()
    {
        var button = new UIButton
        {
            Text = "OK",
            FixedSize = new UISize(200f, 60f),
            Padding = new UIEdgeInsets(10f, 5f, 20f, 15f),
        };
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 60f), Root = button };
        var ui = new UIManager();
        canvas.Update(default, ui.Text);
        canvas.Paint(ui);

        var text = Assert.Single(ui.Primitives.Span.ToArray(), primitive => primitive.TextureId > 0);
        var contentCenter = new Vector2(
            (10f + 180f) * 0.5f,
            (5f + 45f) * 0.5f);
        var textCenter = new Vector2(
            text.Rect.X + text.Rect.Z * 0.5f,
            text.Rect.Y + text.Rect.W * 0.5f);

        Assert.InRange(textCenter.X, contentCenter.X - 3f, contentCenter.X + 3f);
        Assert.InRange(textCenter.Y, contentCenter.Y - 3f, contentCenter.Y + 3f);
    }

    // ———————————— UIProgressBar ————————————

    [Fact]
    public void ProgressBar_Value_IsClampedAndRaisesOnlyOnChange()
    {
        var progress = new UIProgressBar();
        var changes = new List<float>();
        progress.ValueChanged = changes.Add;

        progress.Value = -1f;
        progress.Value = 0.5f;
        progress.Value = 0.5f;
        progress.Value = 2f;

        Assert.Equal(1f, progress.Value);
        Assert.Equal(new[] { 0.5f, 1f }, changes);
    }

    [Fact]
    public void ProgressBar_Paint_DrawsTrackAndPartialFill()
    {
        var progress = new UIProgressBar { Value = 0.25f };
        var canvas = new UICanvas(0)
        {
            Size = new Vector2(200f, 40f),
            Root = progress,
        };
        canvas.Update(default, CreateTextRenderer());

        var ui = new UIManager();
        canvas.Paint(ui);
        var primitives = ui.Primitives.Span.ToArray();

        Assert.Equal(2, primitives.Length);
        Assert.Equal(200f, primitives[0].Rect.Z, precision: 3);
        Assert.Equal(50f, primitives[1].Rect.Z, precision: 3);
        Assert.Equal(primitives[0].Rect.W, primitives[1].Rect.W, precision: 3);
    }

    [Fact]
    public void UIManager_DrawLine_EmitsLinePrimitiveWithThickness()
    {
        var ui = new UIManager();
        ui.DrawLine(7, new Vector2(10f, 20f), new Vector2(80f, 60f), 3f, Vector4.One);

        var primitive = Assert.Single(ui.Primitives.Span.ToArray());
        Assert.True(primitive.IsLine);
        Assert.Equal(new Vector2(10f, 20f), primitive.LineStart);
        Assert.Equal(new Vector2(80f, 60f), primitive.LineEnd);
        Assert.Equal(3f, primitive.LineThickness);
        Assert.Equal(7, primitive.TargetId);
    }

    [Fact]
    public void RenderView_ReceivesContinuousInputWhileRightMouseIsCaptured()
    {
        var view = new UIRenderView();
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 100f), Root = view };
        var renderer = CreateTextRenderer();
        var updates = 0;
        var lastDeltaTime = 0f;
        var clicks = 0;
        view.InputUpdated += (_, deltaTime) => { updates++; lastDeltaTime = deltaTime; };
        view.Clicked += _ => clicks++;
        canvas.Update(default, renderer);
        updates = 0;
        var right = default(MouseButtonMask);
        right.Set(MouseButton.Right, true);

        canvas.Update(new InputState(new Vector2(50f), Vector2.Zero, 0f,
            right, right, default, default, default, default, string.Empty), renderer, 0.02f);
        canvas.Update(new InputState(new Vector2(250f, 50f), new Vector2(200f, 0f), 0f,
            right, default, default, default, default, default, string.Empty), renderer, 0.03f);
        canvas.Update(new InputState(new Vector2(250f, 50f), Vector2.Zero, 0f,
            default, default, right, default, default, default, string.Empty), renderer, 0.04f);

        Assert.Equal(3, updates);
        Assert.Equal(0.04f, lastDeltaTime);
        Assert.Equal(0, clicks);
    }

    [Fact]
    public void Image_PaintUploadsFullResolutionWithUniqueTextureId()
    {
        const uint width = 128;
        const uint height = 96;
        var rgba = new byte[checked((int)(width * height * 4))];
        for (var index = 0; index < rgba.Length; index += 4)
        {
            rgba[index] = (byte)(index / 4 % 251);
            rgba[index + 1] = 127;
            rgba[index + 2] = 255;
            rgba[index + 3] = 255;
        }
        using var image = new UIImage(width, height, rgba)
        {
            FixedSize = new UISize(0f, 0f),
        };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(new UILabel { Text = "Texture preview" });
        root.AddChild(image);
        var canvas = new UICanvas(0) { Size = new Vector2(320f, 240f), Root = root };
        canvas.Update(default, CreateTextRenderer());
        var ui = new UIManager();

        canvas.Paint(ui);

        var uploads = new List<UITextureUpload>();
        while (ui.TryDequeueTexture(out var upload))
            uploads.Add(upload);
        var imageUpload = Assert.Single(uploads, upload => upload.Width == width && upload.Height == height);
        Assert.Equal(rgba, imageUpload.Rgba);
        Assert.Equal(uploads.Count, uploads.Select(upload => upload.Id).Distinct().Count());
        var imagePrimitive = Assert.Single(ui.Primitives.Span.ToArray(),
            primitive => primitive.TextureId == imageUpload.Id);
        Assert.True(imagePrimitive.Rect.Z > 0f);
        Assert.True(imagePrimitive.Rect.W > 0f);

        image.Dispose();
        Assert.Equal(1, ui.PendingTextureReleaseCount);
    }

    [Fact]
    public void TextBox_ExplicitZeroWidth_FillsParentInsteadOfGrowingWithText()
    {
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        var textBox = new UITextBox
        {
            FixedSize = new UISize(0f, 30f),
            Text = "short",
        };
        root.AddChild(textBox);
        var canvas = new UICanvas(0) { Size = new Vector2(320f, 80f), Root = root };

        canvas.Update(default, CreateTextRenderer());
        var initialWidth = textBox.Bounds.Width;
        textBox.Text = "a much longer value that must remain inside the parent width";
        canvas.Update(default, CreateTextRenderer());

        Assert.Equal(320f, initialWidth, precision: 3);
        Assert.Equal(initialWidth, textBox.Bounds.Width, precision: 3);
    }

    // ———————————— UIScrollBox ————————————

    [Fact]
    public void ScrollBox_ContentLargerThanViewport_ClampsOffset()
    {
        var scroll = new UIScrollBox { ScrollDirection = UIScrollDirection.Vertical };
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        for (int i = 0; i < 100; i++)
        {
            content.AddChild(new UIPanel { FixedSize = new UISize(100f, 20f) });
        }

        scroll.Content = content;
        scroll.Measure(new UISize(200f, 200f));
        scroll.Arrange(new UIRect(0f, 0f, 200f, 200f));

        // 内容高 100*20=2000 > 视口 200 → 最大偏移 1800
        scroll.ScrollOffset = new Vector2(0f, 99999f);
        scroll.Arrange(new UIRect(0f, 0f, 200f, 200f));
        Assert.Equal(1800f, scroll.ScrollOffset.Y);

        // 负偏移被钳到 0
        scroll.ScrollOffset = new Vector2(0f, -100f);
        scroll.Arrange(new UIRect(0f, 0f, 200f, 200f));
        Assert.Equal(0f, scroll.ScrollOffset.Y);
    }

    [Fact]
    public void ScrollBox_ContentFitsViewport_NoScroll()
    {
        var scroll = new UIScrollBox { ScrollDirection = UIScrollDirection.Vertical };
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        content.AddChild(new UIPanel { FixedSize = new UISize(100f, 50f) });

        scroll.Content = content;
        scroll.Measure(new UISize(200f, 200f));
        scroll.Arrange(new UIRect(0f, 0f, 200f, 200f));

        scroll.ScrollOffset = new Vector2(0f, 500f);
        scroll.Arrange(new UIRect(0f, 0f, 200f, 200f));
        Assert.Equal(0f, scroll.ScrollOffset.Y); // 内容不超出视口 → 无滚动
    }

    [Fact]
    public void ScrollBox_SilkWheelTickScrollsByConfiguredSpeed()
    {
        var scroll = CreateScrollableBox();
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 200f), Root = scroll };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        canvas.Update(new InputState(
            new Vector2(50f, 50f), Vector2.Zero, -1f,
            default, default, default,
            default, default, default,
            string.Empty), renderer);

        Assert.Equal(scroll.ScrollSpeed, scroll.ScrollOffset.Y, precision: 3);

        // 兼容仍按 Win32 WHEEL_DELTA（±120）上报的平台后端。
        scroll.ScrollOffset = Vector2.Zero;
        canvas.Update(new InputState(
            new Vector2(50f, 50f), Vector2.Zero, -120f,
            default, default, default,
            default, default, default,
            string.Empty), renderer);
        Assert.Equal(scroll.ScrollSpeed, scroll.ScrollOffset.Y, precision: 3);
    }

    [Fact]
    public void ScrollBox_VerticalThumbHasOverlayHitAndCanBeDragged()
    {
        var scroll = CreateScrollableBox();
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 200f), Root = scroll };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        var thumbStart = new Vector2(195f, 10f);
        var thumbEnd = new Vector2(195f, 100f);
        canvas.Update(new InputState(
            thumbStart, Vector2.Zero, 0f,
            left, left, default,
            default, default, default,
            string.Empty), renderer);
        canvas.Update(new InputState(
            thumbEnd, thumbEnd - thumbStart, 0f,
            left, default, default,
            default, default, default,
            string.Empty), renderer);

        Assert.InRange(scroll.ScrollOffset.Y, 899f, 901f);
    }

    private static UIScrollBox CreateScrollableBox()
    {
        var scroll = new UIScrollBox { ScrollDirection = UIScrollDirection.Vertical };
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        for (int i = 0; i < 100; i++)
            content.AddChild(new UIPanel { FixedSize = new UISize(100f, 20f) });
        scroll.Content = content;
        return scroll;
    }

    // ———————————— UIListView ————————————

    [Fact]
    public void ListView_AddItems_SelectByIndex()
    {
        var list = new UIListView();
        for (int i = 0; i < 5; i++)
            list.AddItem($"Item {i}");

        Assert.Equal(5, list.Items.Count);

        list.SelectedIndex = 2;
        Assert.Equal("Item 2", list.SelectedItem?.Text);
        Assert.True(list.SelectedItem!.IsSelected);

        list.SelectedIndex = -1;
        Assert.Null(list.SelectedItem);
    }

    [Fact]
    public void ListView_RightScrollbarCanBeDraggedAboveItems()
    {
        var list = new UIListView { FixedSize = new UISize(200f, 200f) };
        for (int i = 0; i < 100; i++)
            list.AddItem($"Item {i}");
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 200f), Root = list };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        float initialY = list.Items[0].Bounds.Y;

        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        var thumbStart = new Vector2(195f, 8f);
        var thumbEnd = new Vector2(195f, 100f);
        canvas.Update(new InputState(
            thumbStart, Vector2.Zero, 0f,
            left, left, default,
            default, default, default,
            string.Empty), renderer);
        canvas.Update(new InputState(
            thumbEnd, thumbEnd - thumbStart, 0f,
            left, default, default,
            default, default, default,
            string.Empty), renderer);
        canvas.Update(new InputState(
            thumbEnd, Vector2.Zero, 0f,
            default, default, left,
            default, default, default,
            string.Empty), renderer);

        Assert.True(list.Items[0].Bounds.Y < initialY - 500f,
            "Dragging the right scrollbar should scroll the list instead of dragging an item.");
    }

    [Fact]
    public void ListView_RemoveSelectedItem_ClearsSelection()
    {
        var list = new UIListView();
        var item = list.AddItem("To Remove");
        list.AddItem("Keep");
        list.SelectedIndex = 0;

        Assert.True(list.RemoveItem(item));
        Assert.Null(list.SelectedItem);
        Assert.Single(list.Items);
    }

    [Fact]
    public void ListView_MouseClickSelectsAndDoubleClickActivatesItem()
    {
        var list = new UIListView { FixedSize = new UISize(200f, 80f) };
        var item = list.AddItem("Open me");
        var activations = 0;
        list.ItemActivated = activated =>
        {
            Assert.Same(item, activated);
            activations++;
        };
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 80f), Root = list };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        ClickListItem(canvas, renderer, new Vector2(60f, 12f));

        Assert.Same(item, list.SelectedItem);
        Assert.Equal(0, activations);

        ClickListItem(canvas, renderer, new Vector2(60f, 12f));

        Assert.Equal(1, activations);
    }

    private static void ClickListItem(UICanvas canvas, TextRenderer renderer, Vector2 point)
    {
        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            left, left, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            default, default, left, default, default, default, string.Empty), renderer);
    }

    // ———————————— UITreeView ————————————

    [Fact]
    public void TreeView_Flatten_RespectsExpansion()
    {
        var tree = new UITreeView();
        var root = new UITreeViewItem("Root");
        var childA = new UITreeViewItem("A");
        var childB = new UITreeViewItem("B");
        var grandchild = new UITreeViewItem("A1");
        childA.AddSubItem(grandchild);
        root.AddSubItem(childA);
        root.AddSubItem(childB);

        tree.AddRoot(root);
        Assert.Single(tree.Roots);

        // 通过 SubItems 验证逻辑层级
        Assert.Equal(2, root.SubItems.Count);
        Assert.Equal("A", root.SubItems[0].Text);
        Assert.Equal("B", root.SubItems[1].Text);
        Assert.Equal("A1", childA.SubItems[0].Text);
    }

    [Fact]
    public void TreeView_SelectItem_RaisesCallback()
    {
        var tree = new UITreeView();
        var root = new UITreeViewItem("Root");
        tree.AddRoot(root);

        UITreeViewItem? selected = null;
        tree.SelectionChanged = (item) => selected = item;

        tree.SelectItem(root);
        Assert.Same(root, selected);
        Assert.True(root.IsSelected);

        tree.SelectItem(null);
        Assert.Null(selected);
        Assert.False(root.IsSelected);
    }

    [Fact]
    public void TreeView_NonSelectableItemsAreSkippedBySelection()
    {
        var tree = new UITreeView { FixedSize = new UISize(200f, 80f) };
        var locked = new UITreeViewItem("Locked")
        {
            IsSelectable = false,
            IsDraggable = false,
            IsDropTarget = false,
        };
        var editable = new UITreeViewItem("Editable");
        tree.AddRoot(locked);
        tree.AddRoot(editable);

        tree.SelectItem(locked);
        Assert.Null(tree.SelectedItem);
        Assert.Empty(tree.SelectedItems);

        tree.SelectItems(new[] { locked, editable }, locked);
        Assert.Same(editable, tree.SelectedItem);
        Assert.Equal(new[] { editable }, tree.SelectedItems);
    }

    [Fact]
    public void TreeView_MultipleSelection_SupportsControlToggleAndShiftRange()
    {
        var tree = new UITreeView
        {
            AllowMultipleSelection = true,
            FixedSize = new UISize(200f, 120f),
        };
        var items = Enumerable.Range(0, 4).Select(i => new UITreeViewItem($"Item {i}")).ToArray();
        foreach (var item in items)
            tree.AddRoot(item);
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 120f), Root = tree };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        ClickRow(canvas, renderer, row: 0);
        ClickRow(canvas, renderer, row: 2, Key.LeftControl);

        Assert.Equal(new[] { items[0], items[2] }, tree.SelectedItems);
        Assert.Same(items[2], tree.SelectedItem);

        ClickRow(canvas, renderer, row: 3, Key.LeftShift);

        Assert.Equal(new[] { items[2], items[3] }, tree.SelectedItems);
        Assert.Same(items[3], tree.SelectedItem);
        Assert.False(items[0].IsSelected);
        Assert.True(items[2].IsSelected);
        Assert.True(items[3].IsSelected);
    }

    [Fact]
    public void TreeView_ClickArrow_TogglesExpandedState()
    {
        var tree = new UITreeView { FixedSize = new UISize(200f, 120f) };
        var rootItem = new UITreeViewItem("Root") { IsExpanded = true };
        rootItem.AddSubItem(new UITreeViewItem("Child"));
        tree.AddRoot(rootItem);

        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(tree);
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 120f), Root = root };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        var buttonsDown = default(MouseButtonMask);
        buttonsDown.Set(MouseButton.Left, true);
        var arrow = new Vector2(8f, 12f);
        canvas.Update(new InputState(arrow, Vector2.Zero, 0f,
            buttonsDown, buttonsDown, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(arrow, Vector2.Zero, 0f,
            default, default, buttonsDown, default, default, default, string.Empty), renderer);

        Assert.False(rootItem.IsExpanded);
    }

    [Fact]
    public void TreeView_DraggingRowOntoAnother_RaisesDropEvent()
    {
        var tree = new UITreeView { FixedSize = new UISize(200f, 80f) };
        var source = new UITreeViewItem("Source");
        var target = new UITreeViewItem("Target");
        tree.AddRoot(source);
        tree.AddRoot(target);
        UITreeViewItem? droppedSource = null;
        UITreeViewItem? droppedTarget = null;
        tree.ItemDropped = (from, to, _) =>
        {
            droppedSource = from;
            droppedTarget = to;
        };
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 80f), Root = tree };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        var buttons = default(MouseButtonMask);
        buttons.Set(MouseButton.Left, true);
        var sourcePoint = new Vector2(60f, 12f);
        var targetPoint = new Vector2(60f, 36f);

        canvas.Update(new InputState(sourcePoint, Vector2.Zero, 0f,
            buttons, buttons, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(targetPoint, targetPoint - sourcePoint, 0f,
            buttons, default, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(targetPoint, Vector2.Zero, 0f,
            default, default, buttons, default, default, default, string.Empty), renderer);

        Assert.Same(source, droppedSource);
        Assert.Same(target, droppedTarget);
    }

    [Fact]
    public void ListView_DraggingItemOutsideItsRow_RaisesDropCompletedWithCanvasPosition()
    {
        var list = new UIListView { FixedSize = new UISize(200f, 80f) };
        var item = list.AddItem("StaticMesh");
        UIListItem? droppedItem = null;
        var droppedPosition = Vector2.Zero;
        list.ItemDropCompleted = (source, position, _) =>
        {
            droppedItem = source;
            droppedPosition = position;
        };
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 80f), Root = list };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        var buttons = default(MouseButtonMask);
        buttons.Set(MouseButton.Left, true);
        var sourcePoint = new Vector2(60f, 12f);
        var dropPoint = new Vector2(150f, 60f);

        canvas.Update(new InputState(sourcePoint, Vector2.Zero, 0f,
            buttons, buttons, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(dropPoint, dropPoint - sourcePoint, 0f,
            buttons, default, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(dropPoint, Vector2.Zero, 0f,
            default, default, buttons, default, default, default, string.Empty), renderer);

        Assert.Same(item, droppedItem);
        Assert.Equal(dropPoint, droppedPosition);
    }

    private static void ClickRow(UICanvas canvas, TextRenderer renderer, int row, Key modifier = Key.Unknown)
    {
        var point = new Vector2(60f, row * 24f + 12f);
        var buttons = default(MouseButtonMask);
        buttons.Set(MouseButton.Left, true);
        var keys = default(KeyMask);
        if (modifier != Key.Unknown)
            keys.Set(modifier, true);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            buttons, buttons, default, keys, default, default, string.Empty), renderer);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            default, default, buttons, keys, default, default, string.Empty), renderer);
    }

    // ———————————— UITabView ————————————

    [Fact]
    public void TabView_AddTabs_SelectedIndexWorks()
    {
        var tab = new UITabView();
        tab.AddTab(new UITabItem("One", new UIPanel()));
        tab.AddTab(new UITabItem("Two", new UIPanel()));

        Assert.Equal(0, tab.SelectedIndex); // 默认选中第一个
        Assert.Equal("One", tab.SelectedTab?.Title);

        tab.SelectedIndex = 1;
        Assert.Equal("Two", tab.SelectedTab?.Title);
    }

    [Fact]
    public void TabView_CloseClosableTab_RemovesIt()
    {
        var tab = new UITabView();
        tab.AddTab(new UITabItem("One", new UIPanel(), canClose: true));
        tab.AddTab(new UITabItem("Two", new UIPanel()));

        tab.CloseTab(0);
        Assert.Single(tab.Tabs);
        Assert.Equal("Two", tab.Tabs[0].Title);
    }

    [Fact]
    public void TabView_CloseAfterLayout_CanPaintInSameFrame()
    {
        var tabs = new UITabView();
        tabs.AddTab(new UITabItem("Scene", new UIPanel()));
        tabs.AddTab(new UITabItem("Asset A", new UIPanel(), canClose: true));
        tabs.AddTab(new UITabItem("Asset B", new UIPanel(), canClose: true));
        tabs.Measure(new UISize(600f, 400f));
        tabs.Arrange(new UIRect(0f, 0f, 600f, 400f));
        tabs.SelectedIndex = 1;

        tabs.CloseTab(1);

        var ui = new UIManager();
        tabs.Paint(ui, 0);
        Assert.Equal(2, tabs.Tabs.Count);
        Assert.Equal("Asset B", tabs.SelectedTab?.Title);
        Assert.NotEmpty(ui.Primitives.Span.ToArray());
    }

    [Fact]
    public void TabView_MeasuresSelectedContentBeforeArrange()
    {
        var content = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            FixedSize = new UISize(0f, 0f),
        };
        var title = new UIPanel { FixedSize = new UISize(0f, 24f) };
        var viewport = new UIPanel { FixedSize = new UISize(0f, 0f) };
        content.AddChild(title);
        content.AddChild(viewport);
        var tabs = new UITabView { FixedSize = new UISize(0f, 0f) };
        tabs.AddTab(new UITabItem("Scene", content));

        tabs.Measure(new UISize(600f, 400f));
        tabs.Arrange(new UIRect(0f, 0f, 600f, 400f));

        Assert.Equal(24f, title.Bounds.Height);
        Assert.Equal(346f, viewport.Bounds.Height);
    }

    // ———————————— UISplitPanel ————————————

    [Fact]
    public void SplitPanel_Arrange_SplitsCorrectly()
    {
        var split = new UISplitPanel
        {
            Direction = UISplitDirection.Horizontal,
            SplitRatio = 0.25f,
            SplitterWidth = 4f,
        };
        var left = new UIPanel();
        var right = new UIPanel();
        split.SetPanels(left, right);

        split.Measure(new UISize(200f, 100f));
        split.Arrange(new UIRect(0f, 0f, 200f, 100f));
        // 可用 196，左 25% = 49，但 MinFirstSize 默认 50 → 提升到 50
        Assert.Equal(50f, left.Bounds.Width);
        Assert.Equal(196f - 50f, right.Bounds.Width, 1);
        Assert.Equal(100f, right.Bounds.Height);
    }

    [Fact]
    public void SplitPanel_SinglePanel_FillsAvailableSpace()
    {
        var split = new UISplitPanel
        {
            Direction = UISplitDirection.Horizontal,
            SplitRatio = 0.68f,
            SplitterWidth = 4f,
        };
        var viewport = new UIPanel();
        split.SetPanels(viewport, null);

        split.Measure(new UISize(320f, 180f));
        split.Arrange(new UIRect(10f, 20f, 320f, 180f));

        Assert.Equal(new UIRect(10f, 20f, 320f, 180f), viewport.Bounds);
    }

    [Fact]
    public void SplitPanel_MeasuresSubPanels_DesiredSizeFromContent()
    {
        // 回归：UISplitPanel 曾无 OnMeasure，整棵子树从不测量 → DesiredSize 恒 0，内容全被当 fill。
        var split = new UISplitPanel
        {
            Direction = UISplitDirection.Horizontal,
            SplitRatio = 0.5f,
            SplitterWidth = 4f,
        };
        // 左面板固定宽 120 → 右面板固定宽 80，都有内容期望
        var left = new UIPanel { FixedSize = new UISize(120f, 50f) };
        var right = new UIPanel { FixedSize = new UISize(80f, 50f) };
        split.SetPanels(left, right);

        // 经 UICanvas 驱动完整布局
        var canvas = new UICanvas(0) { Size = new Vector2(400f, 100f), Root = split };
        canvas.Update(default, CreateTextRenderer());

        // 左面板按 SplitRatio(0.5) 分配到 (400-4)/2 = 198，右面板 198
        Assert.Equal(198f, left.Bounds.Width, 1);
        Assert.Equal(198f, right.Bounds.Width, 1);
    }

    [Fact]
    public void SplitPanel_TinyBounds_NoThrow()
    {
        // 回归：总尺寸小于分割条+最小面板时，Math.Clamp(min>max) 曾抛 ArgumentException
        var split = new UISplitPanel
        {
            Direction = UISplitDirection.Horizontal,
            SplitRatio = 0.5f,
            SplitterWidth = 10f,
        };
        var left = new UIPanel();
        var right = new UIPanel();
        split.SetPanels(left, right);

        // 各种退化尺寸都不应抛异常
        foreach (float w in new[] { 0f, 1f, 5f, 9f, 10f, 50f, 90f })
        {
            split.Measure(new UISize(w, 100f));
            split.Arrange(new UIRect(0f, 0f, w, 100f));
        }

        // 两面板尺寸不应为负
        Assert.True(left.Bounds.Width >= 0f);
        Assert.True(right.Bounds.Width >= 0f);
    }

    [Fact]
    public void SplitPanel_Vertical_TinyHeight_NoThrow()
    {
        var split = new UISplitPanel
        {
            Direction = UISplitDirection.Vertical,
            SplitRatio = 0.5f,
            SplitterWidth = 6f,
        };
        var top = new UIPanel();
        var bottom = new UIPanel();
        split.SetPanels(top, bottom);

        foreach (float h in new[] { 0f, 2f, 6f, 30f })
        {
            split.Measure(new UISize(100f, h));
            split.Arrange(new UIRect(0f, 0f, 100f, h));
        }

        Assert.True(top.Bounds.Height >= 0f);
        Assert.True(bottom.Bounds.Height >= 0f);
    }

    [Fact]
    public void Toolbar_LayoutsInternalPanel_ButtonsNotStacked()
    {
        // 回归：UIToolbar 未布局内部 _itemsPanel 时，所有按钮叠在左上角 (0,0)（文字重叠）
        var toolbar = new UIToolbar { FixedSize = new UISize(300f, 36f) };
        toolbar.AddButton("New");
        toolbar.AddButton("Open");
        toolbar.AddButton("Save");

        // 经 UICanvas 驱动完整布局（注入 TextRenderer 供按钮测量文本宽度）
        var canvas = new UICanvas(0) { Size = new Vector2(300f, 200f), Root = toolbar };
        canvas.Update(default, CreateTextRenderer());

        // 按钮应横向排开（各自 X 不同，且位于工具栏内而非 (0,0) 重叠）
        var buttons = toolbar.Buttons;
        Assert.Equal(3, buttons.Count);
        Assert.True(buttons[0].Bounds.Width > 0f);
        Assert.True(buttons[1].Bounds.X > buttons[0].Bounds.X, "Second button must be to the right of the first");
        Assert.True(buttons[2].Bounds.X > buttons[1].Bounds.X, "Third button must be to the right of the second");
        // 按钮须在工具栏 Bounds 内（fill 高度拉伸到内容区，不超过工具栏）
        Assert.True(buttons[0].Bounds.Y >= toolbar.Bounds.Y && buttons[0].Bounds.Bottom <= toolbar.Bounds.Bottom + 0.01f,
            "Buttons must stay within toolbar height");
        // 按钮高度 = 工具栏内容高（交叉轴 fill 拉伸，不再顶部对齐留空）
        float toolbarContentH = toolbar.Bounds.Height - toolbar.Padding.Top - toolbar.Padding.Bottom;
        Assert.Equal(toolbarContentH, buttons[0].Bounds.Height, 2);

        // 回归：按钮宽度必须 ≥ 文本宽度 + 内边距（文字不再溢出相邻按钮）
        var textRenderer = CreateTextRenderer();
        var textW = textRenderer.Measure("Open").X;
        Assert.True(buttons[1].Bounds.Width >= textW, $"Button width ({buttons[1].Bounds.Width}) must fit text width ({textW})");

        // 回归：同字号按钮必须等高（不同文本的墨水高不同，曾导致按钮高度不一致）
        float h0 = buttons[0].Bounds.Height;
        float h1 = buttons[1].Bounds.Height;
        float h2 = buttons[2].Bounds.Height;
        Assert.Equal(h0, h1, 2);
        Assert.Equal(h0, h2, 2);
    }


    [Fact]
    public void MenuBar_LayoutsInternalPanel_ItemsNotStacked()
    {
        // 回归：UIMenuBar 未布局内部 _itemsPanel 时，菜单项叠在左上角
        var menuBar = new UIMenuBar { FixedSize = new UISize(300f, 30f) };
        menuBar.AddMenu("File", _ => { });
        menuBar.AddMenu("Edit", _ => { });

        // 经 UICanvas 驱动完整布局（注入 TextRenderer 供菜单项测量文本宽度）
        var canvas = new UICanvas(0) { Size = new Vector2(300f, 200f), Root = menuBar };
        var input = default(Spark.Engine.Input.InputState);
        canvas.Update(input, CreateTextRenderer());

        var items = menuBar.Items;
        Assert.Equal(2, items.Count);
        Assert.True(items[0].Bounds.Width > 0f, "Menu item should have measured width from text");
        Assert.True(items[1].Bounds.X > items[0].Bounds.X, "Second menu item must be to the right of the first");
    }

    [Fact]
    public void MenuPanel_ClosesWhenClickingOutsidePopup()
    {
        var menuBar = new UIMenuBar { FixedSize = new UISize(300f, 30f) };
        menuBar.AddMenu("File", panel => panel.AddItem(new UIMenuItem("Open")));
        var canvas = new UICanvas(0)
        {
            Size = new Vector2(300f, 200f),
            Root = menuBar,
        };
        canvas.Update(default, CreateTextRenderer());

        var menuBarItemCenter = new Vector2(
            menuBar.Items[0].Bounds.X + menuBar.Items[0].Bounds.Width * 0.5f,
            menuBar.Items[0].Bounds.Y + menuBar.Items[0].Bounds.Height * 0.5f);
        Click(canvas, menuBarItemCenter, CreateTextRenderer());
        var menu = Assert.Single(canvas.Overlays.OfType<UIMenuPanel>());
        Assert.NotEmpty(menu.Items);

        // 点击弹层外的 Root 空白区域应立即关闭菜单。
        Click(canvas, new Vector2(280f, 180f), CreateTextRenderer());
        Assert.DoesNotContain(menu, canvas.Overlays);
        Assert.False(menu.Visible);
    }

    [Fact]
    public void DragHandle_TriggersOnlyAfterLeavingClickThreshold()
    {
        var handle = new UIDragHandle { Text = "Details" };
        var started = 0;
        var dragPosition = Vector2.Zero;
        handle.DragStarted = position => { started++; dragPosition = position; };
        var canvas = new UICanvas(0)
        {
            Size = new Vector2(240f, 40f),
            Root = handle,
        };
        canvas.Update(default, CreateTextRenderer());

        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        var point = new Vector2(20f, 12f);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            left, left, default, default, default, default, string.Empty), CreateTextRenderer());
        canvas.Update(new InputState(new Vector2(24f, 12f), new Vector2(4f, 0f), 0f,
            left, default, default, default, default, default, string.Empty), CreateTextRenderer());
        Assert.Equal(0, started);

        canvas.Update(new InputState(new Vector2(40f, 12f), new Vector2(16f, 0f), 0f,
            left, default, default, default, default, default, string.Empty), CreateTextRenderer());
        Assert.Equal(1, started);
        Assert.Equal(new Vector2(40f, 12f), dragPosition);

        canvas.Update(new InputState(new Vector2(40f, 12f), Vector2.Zero, 0f,
            default, default, left, default, default, default, string.Empty), CreateTextRenderer());
        Assert.Equal(1, started);
    }

    private static TextRenderer CreateTextRenderer()
    {
        var family = SixLabors.Fonts.SystemFonts.TryGet("Arial", out var f) ? f : SixLabors.Fonts.SystemFonts.Families.First();
        return new TextRenderer(family.CreateFont(16f, SixLabors.Fonts.FontStyle.Regular));
    }

    private static void Click(UICanvas canvas, Vector2 point, TextRenderer renderer)
    {
        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            left, left, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(point, Vector2.Zero, 0f,
            default, default, left, default, default, default, string.Empty), renderer);
    }

    // ———————————— UIComboBox ————————————

    [Fact]
    public void ComboBox_AddItems_SelectChanges()
    {
        var combo = new UIComboBox();
        combo.AddItem("Alpha");
        combo.AddItem("Beta");
        combo.AddItem("Gamma");

        string? changed = null;
        combo.SelectedItemChanged = (text) => changed = text;

        combo.SelectedIndex = 1;
        Assert.Equal("Beta", combo.SelectedText);
        Assert.Equal("Beta", changed);

        combo.SelectedIndex = 5; // 越界 → 不改变
        Assert.Equal(1, combo.SelectedIndex);
    }

    // ———————————— UIPropertyGrid ————————————

    private sealed class TestObject
    {
        public string Name { get; set; } = "Test";
        public int Count { get; set; } = 42;
        public bool Active { get; set; } = true;
    }

    [Fact]
    public void PropertyGrid_SetsTarget_RendersRows()
    {
        var grid = new UIPropertyGrid { Target = new TestObject() };
        // 无 GPU 环境下仅验证不抛异常；行数由内部私有列表维护，通过 Arrange 无异常验证
        grid.Measure(new UISize(300f, 300f));
        grid.Arrange(new UIRect(0f, 0f, 300f, 300f));
    }

    [Fact]
    public void PropertyGrid_Refresh_NoThrow()
    {
        var grid = new UIPropertyGrid { Target = new TestObject() };
        grid.Refresh();
    }

    [Fact]
    public void PropertyGrid_EditorCaretBlinksDuringIdleFrames()
    {
        var grid = new UIPropertyGrid
        {
            FixedSize = new UISize(300f, 72f),
            Target = new TestObject(),
        };
        var canvas = new UICanvas(0) { Size = new Vector2(300f, 72f), Root = grid };
        var renderer = CreateTextRenderer();
        var ui = new UIManager();
        _ = ui.Text;
        canvas.Update(default, renderer);
        Click(new Vector2(160f, 36f)); // Count 行

        Assert.IsType<UITextBox>(canvas.FocusedElement);
        canvas.Paint(ui);
        Assert.Contains(ui.Primitives.Span.ToArray(), IsCaret);

        bool enteredHiddenPhase = false;
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(2))
        {
            Thread.Sleep(20);
            canvas.Update(default, renderer);
            ui.Clear();
            canvas.Paint(ui);
            if (!ui.Primitives.Span.ToArray().Any(IsCaret))
            {
                enteredHiddenPhase = true;
                break;
            }
        }

        Assert.True(enteredHiddenPhase, "Inspector editor caret did not enter its hidden blink phase.");

        void Click(Vector2 point)
        {
            var left = default(MouseButtonMask);
            left.Set(MouseButton.Left, true);
            canvas.Update(new InputState(point, Vector2.Zero, 0f,
                left, left, default, default, default, default, string.Empty), renderer);
            canvas.Update(new InputState(point, Vector2.Zero, 0f,
                default, default, left, default, default, default, string.Empty), renderer);
        }

        static bool IsCaret(UIPrimitive primitive)
            => primitive.TextureId == 0 && System.Math.Abs(primitive.Rect.Z - 1.5f) < 0.001f;
    }

    [Fact]
    public void PropertyGrid_ClickingOutsideCommitsEditAndHidesCaret()
    {
        var target = new TestObject();
        var grid = new UIPropertyGrid
        {
            FixedSize = new UISize(300f, 72f),
            Target = target,
        };
        var blank = new UIPanel { FixedSize = new UISize(300f, 30f) };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(grid);
        root.AddChild(blank);
        var canvas = new UICanvas(0) { Size = new Vector2(300f, 102f), Root = root };
        var renderer = CreateTextRenderer();
        var ui = new UIManager();
        _ = ui.Text;
        canvas.Update(default, renderer);

        Click(new Vector2(160f, 36f)); // Count = 42
        SendKey(Key.Backspace);
        canvas.Update(new InputState(
            Vector2.Zero, Vector2.Zero, 0f,
            default, default, default,
            default, default, default,
            "7"), renderer);
        Click(new Vector2(160f, 87f));

        Assert.Null(canvas.FocusedElement);
        Assert.Equal(47, target.Count);
        canvas.Paint(ui);
        Assert.DoesNotContain(ui.Primitives.Span.ToArray(), static primitive
            => primitive.TextureId == 0 && System.Math.Abs(primitive.Rect.Z - 1.5f) < 0.001f);

        Click(new Vector2(160f, 36f));
        var editor = Assert.IsType<UITextBox>(canvas.FocusedElement);
        editor.SelectAll();
        SendText("99");
        SendKey(Key.Escape);
        Assert.Null(canvas.FocusedElement);
        Assert.Equal(47, target.Count);

        Click(new Vector2(160f, 36f));
        editor = Assert.IsType<UITextBox>(canvas.FocusedElement);
        editor.SelectAll();
        SendText("88");
        SendKey(Key.Enter);
        Assert.Null(canvas.FocusedElement);
        Assert.Equal(88, target.Count);

        void Click(Vector2 point)
        {
            var left = default(MouseButtonMask);
            left.Set(MouseButton.Left, true);
            canvas.Update(new InputState(point, Vector2.Zero, 0f,
                left, left, default, default, default, default, string.Empty), renderer);
            canvas.Update(new InputState(point, Vector2.Zero, 0f,
                default, default, left, default, default, default, string.Empty), renderer);
        }

        void SendKey(Key key)
        {
            var keys = default(KeyMask);
            keys.Set(key, true);
            canvas.Update(new InputState(
                Vector2.Zero, Vector2.Zero, 0f,
                default, default, default,
                keys, keys, default,
                string.Empty), renderer);
        }

        void SendText(string text)
        {
            canvas.Update(new InputState(
                Vector2.Zero, Vector2.Zero, 0f,
                default, default, default,
                default, default, default,
                text), renderer);
        }
    }

    // ———————————— UIGridPanel ————————————

    // ———————————— UICanvas ————————————

    [Fact]
    public void Canvas_RootReplacedDuringRouteInput_IsLaidOutSameFrame()
    {
        // 回归：切换页面时，RouteInput（按钮点击）替换 Root 后若不同帧补布局，
        // 当帧 Paint 的 Root Bounds 全 0 → UI 空白闪烁（露出底层 3D）。
        var canvas = new UICanvas(0) { Size = new Vector2(400f, 300f) };

        // 旧页面：一个按钮，点击后切换到新页面
        UIButton? switchButton = null;
        var oldRoot = new UIStackPanel { Orientation = UIOrientation.Vertical };
        switchButton = new UIButton
        {
            Text = "Go",
            FixedSize = new UISize(100f, 30f),
            Clicked = () =>
            {
                canvas.ClearFocus();
                canvas.Root = new UIStackPanel
                {
                    Orientation = UIOrientation.Vertical,
                    BackgroundColor = new Vector4(0.1f, 0.2f, 0.3f, 1f),
                };
            },
        };
        oldRoot.AddChild(switchButton);
        canvas.Root = oldRoot;

        // 第一帧：布局旧页面
        canvas.Update(default, CreateTextRenderer());

        // 第二帧：鼠标在按钮上按下+抬起 → RouteInput 内替换 Root
        var buttonsDown = default(MouseButtonMask);
        buttonsDown.Set(MouseButton.Left, true);
        var pressed = buttonsDown;
        var released = default(MouseButtonMask);

        // 按下帧：RouteInput 记录 _pressed = button
        var inputDown = new InputState(new Vector2(50f, 15f), Vector2.Zero, 0f,
            buttonsDown, pressed, released, default, default, default, string.Empty);
        canvas.Update(inputDown, CreateTextRenderer());

        // 抬起帧：OnMouseClick 触发 → switchTo 替换 Root；Update 末尾应补布局
        var inputUp = new InputState(new Vector2(50f, 15f), Vector2.Zero, 0f,
            default, default, released, default, default, default, string.Empty);
        canvas.Update(inputUp, CreateTextRenderer());

        // 断言：新 Root 同帧已布局（Bounds 非 0），Paint 不会空白
        Assert.NotNull(canvas.Root);
        Assert.Equal(400f, canvas.Root!.Bounds.Width);
        Assert.Equal(300f, canvas.Root!.Bounds.Height);
    }

    [Fact]
    public void Canvas_OverlayAddedDuringRouteInput_IsLaidOutSameFrame()
    {
        var canvas = new UICanvas(0) { Size = new Vector2(400f, 300f) };
        var dialog = new UIDialog { Title = "Confirm", Message = "Proceed?" };
        dialog.Buttons.Add(new UIDialogButton("Cancel", isCancel: true));
        var button = new UIButton
        {
            Text = "Open",
            FixedSize = new UISize(100f, 30f),
            Clicked = dialog.Show,
        };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(button);
        root.AddChild(dialog);
        canvas.Root = root;
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        var down = default(MouseButtonMask);
        down.Set(MouseButton.Left, true);
        canvas.Update(new InputState(new Vector2(10f, 10f), Vector2.Zero, 0f,
            down, down, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(new Vector2(10f, 10f), Vector2.Zero, 0f,
            default, default, down, default, default, default, string.Empty), renderer);

        Assert.True(dialog.IsOpen);
        Assert.Contains(dialog, canvas.Overlays);
        Assert.True(dialog.Bounds.Width > 0f);
        Assert.True(dialog.Bounds.Height > 0f);
    }

    [Fact]
    public void Dialog_ShowAndClose_ManagesCanvasFocus()
    {
        var canvas = new UICanvas(0) { Size = new Vector2(320f, 200f) };
        var dialog = new UIDialog { Title = "Confirm" };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(dialog);
        canvas.Root = root;
        canvas.Update(default, CreateTextRenderer());

        dialog.Show();
        Assert.Same(dialog, canvas.FocusedElement);
        dialog.Close();
        Assert.Null(canvas.FocusedElement);
    }

    [Fact]
    public void Canvas_GlobalKeyDownReceivesPressedKeyAndFocusedElement()
    {
        var canvas = new UICanvas(0)
        {
            Size = new Vector2(200f, 100f),
            Root = new UIStackPanel { Orientation = UIOrientation.Vertical },
        };
        var key = KeyMask.None;
        key.Set(Key.Z, true);
        Key? received = null;
        UIElement? receivedFocus = null;
        canvas.GlobalKeyDown = (pressed, _, focused) =>
        {
            received = pressed;
            receivedFocus = focused;
        };

        canvas.Update(new InputState(Vector2.Zero, Vector2.Zero, 0f,
            default, default, default, key, key, default, string.Empty), CreateTextRenderer());

        Assert.Equal(Key.Z, received);
        Assert.Null(receivedFocus);
    }

    [Fact]
    public void TextBox_SelectReplaceUndoRedo_WorksThroughCanvasInput()
    {
        var textBox = new UITextBox { FixedSize = new UISize(200f, 30f), Text = "hello" };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(textBox);
        var canvas = new UICanvas(0) { Size = new Vector2(240f, 60f), Root = root };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        canvas.Focus(textBox);

        var ctrl = KeyMask.None;
        ctrl.Set(Key.LeftControl, true);
        var selectAll = ctrl;
        selectAll.Set(Key.A, true);
        canvas.Update(new InputState(Vector2.Zero, Vector2.Zero, 0f,
            default, default, default, selectAll, selectAll, default, string.Empty), renderer);

        var input = new InputState(Vector2.Zero, Vector2.Zero, 0f,
            default, default, default, ctrl, default, default, "world");
        canvas.Update(input, renderer);
        Assert.Equal("world", textBox.Text);
        Assert.Equal(0, textBox.SelectionLength);
        Assert.True(textBox.CanUndo);

        var undo = new KeyMask();
        undo.Set(Key.Z, true);
        canvas.Update(new InputState(Vector2.Zero, Vector2.Zero, 0f,
            default, default, default, ctrl, undo, default, string.Empty), renderer);
        Assert.Equal("hello", textBox.Text);

        var redo = new KeyMask();
        redo.Set(Key.Y, true);
        canvas.Update(new InputState(Vector2.Zero, Vector2.Zero, 0f,
            default, default, default, ctrl, redo, default, string.Empty), renderer);
        Assert.Equal("world", textBox.Text);
    }

    [Fact]
    public void TextBox_ClickingNonFocusablePanelClearsFocusAndStopsPaintingCaret()
    {
        var textBox = new UITextBox { FixedSize = new UISize(200f, 30f), Text = "value" };
        var background = new UIPanel { FixedSize = new UISize(200f, 30f) };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(textBox);
        root.AddChild(background);
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 60f), Root = root };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        var left = default(MouseButtonMask);
        left.Set(MouseButton.Left, true);
        var ui = new UIManager();
        _ = ui.Text; // 先完成默认字体初始化，避免初始化耗时跨过首个光标闪烁周期。

        Click(new Vector2(20f, 15f));
        Assert.Same(textBox, canvas.FocusedElement);
        canvas.Paint(ui);
        Assert.Contains(ui.Primitives.Span.ToArray(), IsCaret);

        Click(new Vector2(20f, 45f));
        Assert.Null(canvas.FocusedElement);
        ui.Clear();
        canvas.Paint(ui);
        Assert.DoesNotContain(ui.Primitives.Span.ToArray(), IsCaret);

        void Click(Vector2 point)
        {
            canvas.Update(new InputState(point, Vector2.Zero, 0f,
                left, left, default, default, default, default, string.Empty), renderer);
            canvas.Update(new InputState(point, Vector2.Zero, 0f,
                default, default, left, default, default, default, string.Empty), renderer);
        }

        static bool IsCaret(UIPrimitive primitive)
            => primitive.TextureId == 0 && System.Math.Abs(primitive.Rect.Z - 1.5f) < 0.001f;
    }

    [Fact]
    public void TextBox_IdleInputFramesDoNotRestartCaretBlink()
    {
        var textBox = new UITextBox { FixedSize = new UISize(200f, 30f), Text = "value" };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(textBox);
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 30f), Root = root };
        var renderer = CreateTextRenderer();
        var ui = new UIManager();
        _ = ui.Text;
        canvas.Update(default, renderer);
        canvas.Focus(textBox);

        canvas.Paint(ui);
        Assert.Contains(ui.Primitives.Span.ToArray(), IsCaret);

        bool enteredHiddenPhase = false;
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(2))
        {
            Thread.Sleep(20);
            canvas.Update(default, renderer);
            ui.Clear();
            canvas.Paint(ui);
            if (!ui.Primitives.Span.ToArray().Any(IsCaret))
            {
                enteredHiddenPhase = true;
                break;
            }
        }

        Assert.True(enteredHiddenPhase, "Idle input frames kept restarting the caret blink timer.");

        static bool IsCaret(UIPrimitive primitive)
            => primitive.TextureId == 0 && System.Math.Abs(primitive.Rect.Z - 1.5f) < 0.001f;
    }

    [Fact]
    public void TextBox_WindowFocusLossClearsFocusAndStopsPaintingCaret()
    {
        var textBox = new UITextBox { FixedSize = new UISize(200f, 30f), Text = "value" };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(textBox);
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 30f), Root = root };
        var renderer = CreateTextRenderer();
        var ui = new UIManager();
        _ = ui.Text;
        canvas.Update(default, renderer);
        canvas.Focus(textBox);

        canvas.Update(new InputState(
            Vector2.Zero, Vector2.Zero, 0f,
            default, default, default,
            default, default, default,
            string.Empty, windowFocusLost: true), renderer);

        Assert.Null(canvas.FocusedElement);
        canvas.Paint(ui);
        Assert.DoesNotContain(ui.Primitives.Span.ToArray(), static primitive
            => primitive.TextureId == 0 && System.Math.Abs(primitive.Rect.Z - 1.5f) < 0.001f);
    }

    [Fact]
    public void TextBox_ImeCompositionPreviewsWithoutMutatingUntilCommit()
    {
        var textBox = new UITextBox { FixedSize = new UISize(200f, 30f), Text = "编辑" };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(textBox);
        var canvas = new UICanvas(0) { Size = new Vector2(240f, 60f), Root = root };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        canvas.Focus(textBox);

        canvas.Update(new InputState(
            Vector2.Zero, Vector2.Zero, 0f,
            default, default, default,
            default, default, default,
            string.Empty, "qi", isComposing: true), renderer);

        Assert.Equal("编辑", textBox.Text);
        Assert.NotNull(canvas.ImeCandidatePosition);
        var backspace = new KeyMask();
        backspace.Set(Key.Backspace, true);
        canvas.Update(new InputState(
            Vector2.Zero, Vector2.Zero, 0f,
            default, default, default,
            backspace, backspace, default,
            string.Empty, "qi", isComposing: true), renderer);
        Assert.Equal("编辑", textBox.Text);

        canvas.Update(new InputState(
            Vector2.Zero, Vector2.Zero, 0f,
            default, default, default,
            default, default, default,
            "器", string.Empty, isComposing: false), renderer);
        Assert.Equal("编辑器", textBox.Text);
        Assert.True(textBox.CanUndo);
        textBox.Undo();
        Assert.Equal("编辑", textBox.Text);
    }

    [Fact]
    public void TextBox_ClipboardCopyPaste_UsesInjectedClipboard()
    {
        var clipboard = new MemoryClipboard();
        var textBox = new UITextBox { Text = "copy me", Clipboard = clipboard };
        textBox.SelectAll();

        // 通过公开编辑 API 验证剪贴板依赖可以替换为平台实现；输入路由负责快捷键转发。
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(textBox);
        var canvas = new UICanvas(0) { Size = new Vector2(240f, 60f), Root = root };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);
        canvas.Focus(textBox);

        var ctrl = KeyMask.None;
        ctrl.Set(Key.LeftControl, true);
        var copy = new KeyMask();
        copy.Set(Key.C, true);
        canvas.Update(new InputState(Vector2.Zero, Vector2.Zero, 0f,
            default, default, default, ctrl, copy, default, string.Empty), renderer);
        Assert.Equal("copy me", clipboard.GetText());

        textBox.Text = "";
        var paste = new KeyMask();
        paste.Set(Key.V, true);
        canvas.Update(new InputState(Vector2.Zero, Vector2.Zero, 0f,
            default, default, default, ctrl, paste, default, string.Empty), renderer);
        Assert.Equal("copy me", textBox.Text);
    }

    [Fact]
    public void ComboBox_MouseMoveThenClick_SelectsDropDownItem()
    {
        var combo = new UIComboBox { FixedSize = new UISize(160f, 26f) };
        combo.AddItem("First");
        combo.AddItem("Second");
        combo.AddItem("Third");

        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(combo);
        var canvas = new UICanvas(0)
        {
            Size = new Vector2(200f, 200f),
            Root = root,
        };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        // 打开下拉框，并执行一次 Paint 生成下拉项矩形。
        var down = default(MouseButtonMask);
        down.Set(MouseButton.Left, true);
        var pressed = new InputState(new Vector2(10f, 10f), Vector2.Zero, 0f,
            down, down, default, default, default, default, string.Empty);
        canvas.Update(pressed, renderer);
        var ui = new UIManager();
        canvas.Paint(ui);

        var released = new InputState(new Vector2(10f, 10f), Vector2.Zero, 0f,
            default, default, down, default, default, default, string.Empty);
        canvas.Update(released, renderer);
        canvas.Paint(ui);

        // 普通鼠标移动（没有按键）到第二项，再按下/抬起。
        var hoverPoint = new Vector2(10f, 26f + 26f + 4f);
        canvas.Update(new InputState(hoverPoint, Vector2.Zero, 0f,
            default, default, default, default, default, default, string.Empty), renderer);
        canvas.Paint(ui);

        canvas.Update(new InputState(hoverPoint, Vector2.Zero, 0f,
            down, down, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(hoverPoint, Vector2.Zero, 0f,
            default, default, down, default, default, default, string.Empty), renderer);

        Assert.Equal(1, combo.SelectedIndex);
        Assert.Equal("Second", combo.SelectedText);
    }

    [Fact]
    public void ComboBox_DropDownOverlay_IsPaintedAfterLaterSibling()
    {
        var combo = new UIComboBox { FixedSize = new UISize(160f, 26f) };
        combo.AddItem("First");
        combo.AddItem("Second");
        var sibling = new UIPanel
        {
            FixedSize = new UISize(160f, 80f),
            Color = new Vector4(1f, 0f, 0f, 1f),
        };
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        root.AddChild(combo);
        root.AddChild(sibling);
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 160f), Root = root };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        var down = default(MouseButtonMask);
        down.Set(MouseButton.Left, true);
        canvas.Update(new InputState(new Vector2(10f, 10f), Vector2.Zero, 0f,
            down, down, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(new Vector2(10f, 10f), Vector2.Zero, 0f,
            default, default, down, default, default, default, string.Empty), renderer);
        canvas.Update(default, renderer);

        var ui = new UIManager();
        canvas.Paint(ui);
        var primitives = ui.Primitives.Span.ToArray();
        int siblingIndex = Array.FindIndex(primitives, p => p.Color.X > 0.9f && p.Color.Y < 0.1f && p.Rect.Z > 50f);
        int dropDownIndex = Array.FindIndex(primitives, p => p.Rect.Y > 25f && p.Rect.Z >= 150f && p.Color.Z > 0.1f);

        Assert.True(siblingIndex >= 0);
        Assert.True(dropDownIndex > siblingIndex, "Drop-down primitives must be painted after later siblings.");
    }

    [Fact]
    public void ComboBox_DropDownEscapesParentClip_AndWinsHitTest()
    {
        var combo = new UIComboBox { FixedSize = new UISize(160f, 26f) };
        combo.AddItem("First");
        combo.AddItem("Second");
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        content.AddChild(combo);
        content.AddChild(new UIPanel { FixedSize = new UISize(160f, 80f), Color = Vector4.One });
        var scroll = new UIScrollBox
        {
            Content = content,
            FixedSize = new UISize(160f, 40f),
            ClipToBounds = true,
        };
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 120f), Root = scroll };
        var renderer = CreateTextRenderer();
        canvas.Update(default, renderer);

        var down = default(MouseButtonMask);
        down.Set(MouseButton.Left, true);
        canvas.Update(new InputState(new Vector2(10f, 10f), Vector2.Zero, 0f,
            down, down, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(new Vector2(10f, 10f), Vector2.Zero, 0f,
            default, default, down, default, default, default, string.Empty), renderer);
        canvas.Update(default, renderer);

        var ui = new UIManager();
        canvas.Paint(ui);
        var primitives = ui.Primitives.Span.ToArray();
        var dropdown = primitives.Last(p => p.Rect.Y > 25f && p.Rect.Z >= 150f && p.Color.Z > 0.1f);
        Assert.Equal(0f, dropdown.ScissorRect.Z, precision: 3);

        // The second row sits underneath the popup. Clicking its second item
        // must still be routed to the open ComboBox.
        var itemPoint = new Vector2(10f, 26f + 26f + 4f);
        canvas.Update(new InputState(itemPoint, Vector2.Zero, 0f,
            default, default, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(itemPoint, Vector2.Zero, 0f,
            down, down, default, default, default, default, string.Empty), renderer);
        canvas.Update(new InputState(itemPoint, Vector2.Zero, 0f,
            default, default, down, default, default, default, string.Empty), renderer);
        Assert.Equal("Second", combo.SelectedText);
    }

    [Fact]
    public void GridPanel_AutoRow_WithFixedSize_MeasuresContent()
    {
        // 回归：FixedSize 高度 > 0 时 OnMeasure 曾提前返回，跳过 Auto 轨尺寸收集，
        // 导致 Arrange 时 Auto 行高为 0（文本溢出到行缝隙）。
        var grid = new UIGridPanel
        {
            CellSpacing = 4f,
            FixedSize = new UISize(0f, 300f), // 宽 0（fill）、高 300
        };
        grid.RowDefinitions.Add(UIGridDefinition.Auto());
        grid.RowDefinitions.Add(UIGridDefinition.Star(1f));
        grid.ColumnDefinitions.Add(UIGridDefinition.Auto());
        grid.ColumnDefinitions.Add(UIGridDefinition.Star(1f));

        var cell = new UIStackPanel { Orientation = UIOrientation.Vertical };
        cell.AddChild(new UILabel { Text = "two\nlines" });
        grid.AddChild(cell);
        grid.SetRow(cell, 0);
        grid.SetColumn(cell, 0);

        // 经 UICanvas 驱动完整布局（注入 TextRenderer 供 UILabel 测量行高）
        var canvas = new UICanvas(0) { Size = new Vector2(400f, 300f), Root = grid };
        canvas.Update(default, CreateTextRenderer());

        // Auto 行应容纳两行文本（> 单行高），文本不再溢出到行 1 缝隙
        Assert.True(cell.Bounds.Height > 20f, $"Auto row should fit two lines of text, got {cell.Bounds.Height}");
        Assert.True(cell.Bounds.Height >= CreateTextRenderer().LineHeight * 2f - 1f,
            $"Auto row height ({cell.Bounds.Height}) should be >= 2 line heights");
    }

    [Fact]
    public void GridPanel_MultilineLabel_HeightMatchesDraw()
    {
        // 回归：UILabel 多行文本高度曾只算单行，绘制却画两行 → 溢出
        var textRenderer = CreateTextRenderer();
        var single = textRenderer.MeasureBlock("one");
        var multi = textRenderer.MeasureBlock("one\ntwo");

        // 两行高度 ≈ 单行 × 2
        Assert.True(multi.Y > single.Y * 1.9f, $"Multiline height ({multi.Y}) should be ~2x single ({single.Y})");
        // 宽度取最宽行
        Assert.Equal(textRenderer.Measure("one\ntwo").X, multi.X, 2);
    }

    [Fact]
    public void StackPanel_Padding_ChildFillsContentNotOverflow()
    {
        // 回归：StackPanel 传给子元素的 Measure 约束曾不减自身 Padding，
        // 导致 fill 子元素（含 Star 列的 Grid）按未减 padding 宽度测量，
        // Arrange 时溢出内容区、右缘贴到父容器边缘（Grid 页面右侧贴窗口）。
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(8f),
        };

        var grid = new UIGridPanel
        {
            CellSpacing = 4f,
            FixedSize = new UISize(0f, 100f),
        };
        grid.ColumnDefinitions.Add(UIGridDefinition.Fixed(50f));
        grid.ColumnDefinitions.Add(UIGridDefinition.Star(1f));
        var cell = new UIStackPanel { Orientation = UIOrientation.Vertical };
        cell.AddChild(new UILabel { Text = "cell" });
        grid.AddChild(cell);
        grid.SetRow(cell, 0);
        grid.SetColumn(cell, 0);

        root.AddChild(grid);

        var canvas = new UICanvas(0) { Size = new Vector2(400f, 300f), Root = root };
        canvas.Update(default, CreateTextRenderer());

        // Grid 应填满 root 内容区（400 - 8*2 = 384），右缘不超出窗口（400）
        Assert.Equal(384f, grid.Bounds.Width, 2);
        Assert.True(grid.Bounds.Right <= 400.01f, $"Grid right edge ({grid.Bounds.Right}) must not exceed window (400)");
        Assert.Equal(8f, grid.Bounds.X, 2);
    }

    [Fact]
    public void StackPanel_CrossAxis_CappedToContainerWidth()
    {
        // 回归：StackPanel 交叉轴曾直接用子元素 DesiredSize（不封顶），
        // 长文本子元素被安排到比容器还宽的矩形 → 文字溢出容器边框、到边距上。
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(4f),
        };
        // 宽 200 的容器里放一条文本宽远超 200 的 Label
        var label = new UILabel { Text = "This is a very long label text that exceeds the container width significantly" };
        root.AddChild(label);

        var canvas = new UICanvas(0) { Size = new Vector2(200f, 300f), Root = root };
        canvas.Update(default, CreateTextRenderer());

        // Label 宽度应封顶到容器内容宽（200 - 8 = 192），不溢出
        Assert.True(label.Bounds.Width <= 192.01f,
            $"Label width ({label.Bounds.Width}) must be capped to container content width (192)");
        Assert.True(label.Bounds.Right <= 200.01f,
            $"Label right edge ({label.Bounds.Right}) must not exceed container (200)");
    }

    [Fact]
    public void TextRenderer_Truncate_RespectsMaxWidth()
    {
        var textRenderer = CreateTextRenderer();
        const string longText = "A very long string that will not fit in a narrow cell";

        // 宽裕时不截断
        Assert.Equal(longText, textRenderer.Truncate(longText, 10000f));

        // 窄时返回「前缀 + …」，且宽度不超 maxWidth
        var truncated = textRenderer.Truncate(longText, 80f);
        Assert.NotEqual(longText, truncated);
        Assert.EndsWith("…", truncated);
        Assert.True(textRenderer.Measure(truncated).X <= 81f,
            $"Truncated width ({textRenderer.Measure(truncated).X}) must be <= 80 + epsilon");

        // 空/超窄回退
        Assert.Equal(string.Empty, textRenderer.Truncate(string.Empty, 10f));
        // 极窄（省略号本身都放不下）：返回省略号（不小于任意内容）
        var tiny = textRenderer.Truncate(longText, 5f);
        Assert.Equal("…", tiny);
    }

    [Fact]
    public void TextRenderer_MeasureBlock_MultiLineWidthIsMaxLine()
    {
        var textRenderer = CreateTextRenderer();
        // 短行 + 超长行：MeasureBlock 宽度应取最宽行（曾直接把含 \n 整串当单行测量）
        var wide = textRenderer.Measure("This is the longest line here").X;
        var block = textRenderer.MeasureBlock("short\nThis is the longest line here");

        Assert.Equal(wide, block.X, 1);
        // 高度 = max(2×行高, 整串墨水盒高)——至少 2×行高
        Assert.True(block.Y >= textRenderer.LineHeight * 2f - 1f,
            $"Block height ({block.Y:F2}) must be >= 2 line heights ({textRenderer.LineHeight * 2f:F2})");
    }

    [Fact]
    public void Multiline_MeasureBlock_HeightCoversRenderHeight()
    {
        // 回归：LineHeight 曾用单行墨水盒近似，多行渲染高度（含 line gap）超过行数×LineHeight，
        // 布局分配不足 → 文字底部被裁剪（Grid 第二行现象）。
        var tr = CreateTextRenderer();
        double renderH = tr.Measure("Ag\nAg\nAg").Y;        // 实际渲染高度（含抗锯齿余量）
        double layoutH = tr.MeasureBlock("Ag\nAg\nAg").Y;   // 布局分配高度

        Assert.True(layoutH >= renderH - 1.0,
            $"Layout height ({layoutH:F2}) must cover render height ({renderH:F2})");

        // 多行布局高度 ≥ 行数 × 行高
        Assert.True(tr.MeasureBlock("Ag\nAg\nAg").Y >= tr.LineHeight * 3f - 1f);
    }

    [Fact]
    public void Label_Height_StableAcrossTexts_SameFont()
    {
        // 回归：UILabel 高度曾用 max(行数×LineHeight, 墨水盒高)，墨水盒随文本变化
        // （含 descender 更高）→ 同字号不同文本高度波动 → 状态文字变化时下方控件位移。
        var tr = CreateTextRenderer();

        float h1 = tr.MeasureBlock("Click a toolbar button to test").Y;
        float h2 = tr.MeasureBlock("Clicked: New").Y;
        float h3 = tr.MeasureBlock("agyp").Y;  // 含 descender/ascender

        // 单行：高度恒定 = LineHeight，不随文本波动
        Assert.Equal(h1, h2, 2);
        Assert.Equal(h1, h3, 2);
        Assert.Equal(tr.LineHeight, h1, 2);

        // 多行：行数 × LineHeight，且不裁（≥ 纯墨水盒）
        double twoLineInk = tr.Measure("A\nB").Y - 2.0; // 减 +2 余量得纯墨水盒
        double twoLineLayout = tr.MeasureBlock("A\nB").Y;
        Assert.True(twoLineLayout >= twoLineInk,
            $"2-line layout ({twoLineLayout:F1}) must cover ink ({twoLineInk:F1})");
    }

    [Fact]
    public void Toolbar_StatusTextChange_DoesNotShiftLayout()
    {
        // 端到端回归：点击 Toolbar 按钮改状态文字（含 descender 差异），
        // Toolbar 位置必须稳定（UILabel 高度曾随墨水盒波动 → 下方控件位移）。
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(8f),
            Spacing = 6f,
        };
        var statusLabel = new UILabel { Text = "Click a toolbar button to test" };
        root.AddChild(statusLabel);

        var toolbar = new UIToolbar { FixedSize = new UISize(300f, 36f) };
        toolbar.AddButton("New", () => statusLabel.Text = "Clicked: New");
        toolbar.AddButton("agyp", () => statusLabel.Text = "agyp");  // 含 descender/ascender 的短文本
        root.AddChild(toolbar);

        var canvas = new UICanvas(0) { Size = new Vector2(400f, 300f), Root = root };
        canvas.Update(default, CreateTextRenderer());

        float toolbarY0 = toolbar.Bounds.Y;
        float statusH0 = statusLabel.Bounds.Height;

        // 模拟点击按钮改变状态文字 → 重新布局
        statusLabel.Text = "agyp";
        canvas.Update(default, CreateTextRenderer());

        Assert.Equal(statusH0, statusLabel.Bounds.Height, 2);
        Assert.Equal(toolbarY0, toolbar.Bounds.Y, 2);
    }

    [Fact]
    public void Probe_LineHeight_CoversSingleLineInk()
    {
        // 关键验证：LineHeight 必须 ≥ 单行文本的实际墨水高度（含 descender 的 "agyp"），
        // 否则 UILabel 的 ClipToBounds 会裁掉文字底部。
        var tr = CreateTextRenderer();

        // Measure("agyp").Y 是纹理高（墨水盒 + 2px 余量）。纹理从 position + (top-1) 开始绘制，
        // 墨水实际在纹理内偏移。布局高度 = LineHeight。需要 LineHeight ≥ 墨水盒高（含顶部偏移）。
        float inkSingle = tr.Measure("agyp").Y;   // 含 descender 'p'，最坏情况
        float inkAg = tr.Measure("Ag").Y;         // 含 ascender

        Assert.True(tr.LineHeight + 2f >= inkSingle,
            $"LineHeight ({tr.LineHeight:F1}) + 2 must cover single-line ink texture ({inkSingle:F1})");
        Assert.True(tr.LineHeight + 2f >= inkAg,
            $"LineHeight ({tr.LineHeight:F1}) + 2 must cover 'Ag' ink texture ({inkAg:F1})");
    }

    [Fact]
    public void Label_Paint_TextInkStaysWithinLabelBounds()
    {
        // 精确端到端验证：单个 UILabel（单行含 descender / 两行），Paint 后
        // 文本基元底部 ≤ Label 底部（文字不被 ClipToBounds 裁掉）。
        var tr = CreateTextRenderer();
        var cases = new[] { "agyp", "line1\nline2", "Click toolbar" };

        foreach (var text in cases)
        {
            var label = new UILabel { Text = text };
            var canvas = new UICanvas(0) { Size = new Vector2(400f, 300f), Root = label };
            canvas.Update(default, tr);

            var ui = new UIManager();
            canvas.Paint(ui);
            var textPrims = ui.Primitives.Span.ToArray().Where(p => p.TextureId > 0).ToList();
            Assert.NotEmpty(textPrims);

            float layoutBottom = label.Bounds.Bottom;
            foreach (var prim in textPrims)
            {
                float primBottom = prim.Rect.Y + prim.Rect.W;
                // 纹理含 +2 余量，允许 2px 超出（裁剪只影响无墨水的余量区）
                Assert.True(primBottom <= layoutBottom + 2.01f,
                    $"'{text}': text bottom ({primBottom:F1}) must be within label bottom ({layoutBottom:F1}) + 2px");
            }
        }
    }

    [Fact]
    public void LineHeight_Formula_CoversMultilineInk_AllSizes()
    {
        // 验证 MeasureLineHeight 公式（三行墨水盒-单行墨水盒)/2 对不同字号都给出 ≥ 墨水盒的行高
        foreach (var size in new[] { 12f, 16f, 24f, 32f })
        {
            var family = SixLabors.Fonts.SystemFonts.TryGet("Arial", out var f) ? f : SixLabors.Fonts.SystemFonts.Families.First();
            var tr = new TextRenderer(family.CreateFont(size, SixLabors.Fonts.FontStyle.Regular));

            // 单行含 descender 的最坏情况
            float singleInk = tr.Measure("agyp").Y - 2f;   // 纯墨水盒
            // 两行墨水盒（含 1 个行距）
            float twoInk = tr.Measure("agyp\nagyp").Y - 2f;
            // 布局两行高度 = 2 × LineHeight
            float twoLayout = tr.MeasureBlock("agyp\nagyp").Y;

            Assert.True(twoLayout >= twoInk,
                $"size={size}: 2-line layout ({twoLayout:F1}) must cover ink ({twoInk:F1})");
            Assert.True(tr.LineHeight >= singleInk,
                $"size={size}: LineHeight ({tr.LineHeight:F1}) must cover single-line ink ({singleInk:F1})");
        }
    }

    // ———————————— 滚动视口裁剪 ————————————

    [Fact]
    public void ScrollBox_ScrollToTop_ContentDoesNotExceedViewportTop()
    {
        var scroll = new UIScrollBox { ScrollDirection = UIScrollDirection.Vertical };
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        for (int i = 0; i < 50; i++)
            content.AddChild(new UIPanel { FixedSize = new UISize(100f, 20f) });

        scroll.Content = content;
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 200f), Root = scroll };
        canvas.Update(default, CreateTextRenderer());

        // 滚动到顶部：第一个子元素顶部应 = 视口顶部（ScrollBox 无 Padding，Bounds 即视口）
        scroll.ScrollOffset = new Vector2(0f, 0f);
        canvas.Update(default, CreateTextRenderer());

        var viewport = scroll.Bounds;
        var firstChild = content.Children[0];
        Assert.True(firstChild.Bounds.Y >= viewport.Y - 0.01f,
            $"At top, first item top ({firstChild.Bounds.Y:F2}) must not exceed viewport top ({viewport.Y:F2})");
    }

    [Fact]
    public void ScrollBox_ScrollToBottom_ContentDoesNotExceedViewportBottom()
    {
        var scroll = new UIScrollBox { ScrollDirection = UIScrollDirection.Vertical };
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        for (int i = 0; i < 50; i++)
            content.AddChild(new UIPanel { FixedSize = new UISize(100f, 20f) });

        scroll.Content = content;
        var canvas = new UICanvas(0) { Size = new Vector2(200f, 200f), Root = scroll };
        canvas.Update(default, CreateTextRenderer());

        // 滚到最底部
        scroll.ScrollOffset = new Vector2(0f, 99999f);
        canvas.Update(default, CreateTextRenderer());

        var viewport = scroll.Bounds;
        var lastChild = content.Children[^1];
        // 底部：最后一个子元素底部应 ≤ 视口底部
        Assert.True(lastChild.Bounds.Bottom <= viewport.Bottom + 0.01f,
            $"At bottom, last item bottom ({lastChild.Bounds.Bottom:F2}) must not exceed viewport bottom ({viewport.Bottom:F2})");
    }

    [Fact]
    public void ListView_ScrollToBottom_ItemsStayWithinViewport()
    {
        var list = new UIListView { FixedSize = new UISize(200f, 200f) };
        for (int i = 0; i < 30; i++)
            list.AddItem($"Item {i}");

        var canvas = new UICanvas(0) { Size = new Vector2(200f, 200f), Root = list };
        canvas.Update(default, CreateTextRenderer());

        // 模拟键盘 End 键滚动到底部
        // 直接设 scrollBox 偏移（ListView 内部 _scrollBox 无法直接访问，经 Selection 驱动 ScrollIntoView）
        list.SelectedIndex = 29;

        // 选中最后一项后重新布局
        canvas.Update(default, CreateTextRenderer());

        var selected = list.SelectedItem!;
        var viewport = list.Bounds; // ListView 自身即视口
        Assert.True(selected.Bounds.Y < viewport.Bottom,
            $"Selected last item Y ({selected.Bounds.Y:F2}) should be visible within viewport bottom ({viewport.Bottom:F2})");
    }

    [Fact]
    public void ScrollBox_ScrollToBottom_LastItemBottomAlignsWithViewport()
    {
        // 滚动到底时，最后一个子元素底部应精确对齐视口底部（不超出）
        var scroll = new UIScrollBox { ScrollDirection = UIScrollDirection.Vertical };
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        for (int i = 0; i < 10; i++)
            content.AddChild(new UIPanel { FixedSize = new UISize(100f, 20f) }); // 总高 200

        scroll.Content = content;
        var canvas = new UICanvas(0) { Size = new Vector2(100f, 150f), Root = scroll };
        canvas.Update(default, CreateTextRenderer());

        // 内容 200 > 视口 150 → maxScroll = 50
        scroll.ScrollOffset = new Vector2(0f, 99999f);
        canvas.Update(default, CreateTextRenderer());

        var viewport = scroll.Bounds;
        var lastChild = content.Children[^1];
        // 最后一个项底部 = 视口底部（内容高 200，偏移 50 → 底部 = 50 + 200 - ... 精确计算）
        Assert.True(lastChild.Bounds.Bottom <= viewport.Bottom + 0.01f,
            $"Last item bottom ({lastChild.Bounds.Bottom:F2}) must not exceed viewport bottom ({viewport.Bottom:F2})");
        // 且内容最后一项应完整可见（不被底部裁掉一半）
        Assert.True(lastChild.Bounds.Bottom > viewport.Bottom - 20.01f,
            $"Last item bottom ({lastChild.Bounds.Bottom:F2}) should reach viewport bottom ({viewport.Bottom:F2}) when scrolled fully");
    }

    [Fact]
    public void ScrollBox_ScrollToTop_FirstItemTopAlignsWithViewport()
    {
        var scroll = new UIScrollBox { ScrollDirection = UIScrollDirection.Vertical };
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        for (int i = 0; i < 10; i++)
            content.AddChild(new UIPanel { FixedSize = new UISize(100f, 20f) });

        scroll.Content = content;
        var canvas = new UICanvas(0) { Size = new Vector2(100f, 150f), Root = scroll };
        canvas.Update(default, CreateTextRenderer());

        // 先滚到底再滚回顶，验证完全归位
        scroll.ScrollOffset = new Vector2(0f, 99999f);
        canvas.Update(default, CreateTextRenderer());
        scroll.ScrollOffset = new Vector2(0f, 0f);
        canvas.Update(default, CreateTextRenderer());

        var firstChild = content.Children[0];
        var viewport = scroll.Bounds;
        Assert.True(firstChild.Bounds.Y >= viewport.Y - 0.01f,
            $"First item top ({firstChild.Bounds.Y:F2}) must align with viewport top ({viewport.Y:F2})");
    }

    [Fact]
    public void ScrollBox_Scrolled_PrimitivesHaveScissorClip()
    {
        // 回归：滚动后内容项越出视口的绘制必须带 ScissorRect（否则渲染时越过视口可见）
        var scroll = new UIScrollBox { ScrollDirection = UIScrollDirection.Vertical };
        var content = new UIStackPanel { Orientation = UIOrientation.Vertical };
        for (int i = 0; i < 20; i++)
            content.AddChild(new UIPanel
            {
                Color = new Vector4(1f, 0f, 0f, 1f),
                FixedSize = new UISize(100f, 20f),
            });

        scroll.Content = content;
        var canvas = new UICanvas(0) { Size = new Vector2(100f, 100f), Root = scroll };
        canvas.Update(default, CreateTextRenderer());

        // 滚到中间：内容项顶部/底部都越出视口
        scroll.ScrollOffset = new Vector2(0f, 150f);
        canvas.Update(default, CreateTextRenderer());

        // Paint 产出基元，检查每项的背景基元是否带 scissor
        var ui = new UIManager();
        canvas.Paint(ui);

        // 找红色背景基元（UIPanel 背景）
        var panelPrimitives = ui.Primitives.Span.ToArray()
            .Where(p => p.Color.X > 0.9f && p.Color.Y < 0.1f && p.Color.Z < 0.1f)
            .ToList();

        Assert.NotEmpty(panelPrimitives);

        foreach (var prim in panelPrimitives)
        {
            // 越出视口的项必须带 scissor 裁剪（裁剪区 = 视口 0,0,100,100）
            Assert.True(prim.ScissorRect.Z > 0f && prim.ScissorRect.W > 0f,
                $"Scrolled-out panel must have scissor clip, got {prim.ScissorRect}");
            // scissor 应在视口范围内
            Assert.True(prim.ScissorRect.X >= -0.01f && prim.ScissorRect.Y >= -0.01f,
                $"Scissor origin must be >= 0, got {prim.ScissorRect}");
            Assert.True(prim.ScissorRect.X + prim.ScissorRect.Z <= 100.01f,
                $"Scissor right ({prim.ScissorRect.X + prim.ScissorRect.Z:F1}) must be <= viewport width (100)");
            Assert.True(prim.ScissorRect.Y + prim.ScissorRect.W <= 100.01f,
                $"Scissor bottom ({prim.ScissorRect.Y + prim.ScissorRect.W:F1}) must be <= viewport height (100)");
        }
    }

    [Fact]
    public void ListView_Scrolled_PrimitivesHaveScissorClip()
    {
        // ListView（ListView → _scrollBox → _itemsPanel → item 双层嵌套）滚动后，
        // 越出视口的 item 基元必须带 scissor（否则渲染时越过视口可见）
        // ListView 作为固定高度子元素（非 Root 拉伸），视口 = 150
        var root = new UIStackPanel { Orientation = UIOrientation.Vertical };
        var list = new UIListView { FixedSize = new UISize(200f, 150f) };
        for (int i = 0; i < 30; i++)
            list.AddItem($"Item {i}");
        root.AddChild(list);

        var canvas = new UICanvas(0) { Size = new Vector2(200f, 200f), Root = root };
        canvas.Update(default, CreateTextRenderer());

        // 选中最后一项 → ScrollIntoView 滚到底
        list.SelectedIndex = 29;
        canvas.Update(default, CreateTextRenderer());

        var ui = new UIManager();
        canvas.Paint(ui);

        // 收集所有基元（含文本）
        var primitives = ui.Primitives.Span.ToArray();
        Assert.NotEmpty(primitives);

        // ListView 视口 = 150（FixedSize 高，非 Root 拉伸）
        var viewportBottom = 150f;
        foreach (var prim in primitives)
        {
            // 有裁剪区的基元，裁剪区必须 ≤ 视口（不能放大到父容器/窗口）
            if (prim.ScissorRect.Z > 0f && prim.ScissorRect.W > 0f)
            {
                Assert.True(prim.ScissorRect.Y + prim.ScissorRect.W <= viewportBottom + 0.01f,
                    $"Item primitive scissor bottom ({prim.ScissorRect.Y + prim.ScissorRect.W:F1}) must be <= ListView viewport bottom ({viewportBottom})");
            }
        }

        // 至少有一个基元带裁剪（滚动后有内容被裁剪）
        Assert.True(primitives.Any(p => p.ScissorRect.Z > 0f && p.ScissorRect.W > 0f),
            "Scrolled ListView should produce clipped primitives");
    }

    [Fact]
    public void ScrollBox_DemoScenario_AllPrimitivesClippedToViewport()
    {
        // 完整模拟 Demo ScrollBox 验收场景：StackPanel 内的 ScrollBox，50 行内容，滚动后
        // 所有基元（背景 + 文本）的 scissor 必须限制在 ScrollBox 视口内
        var root = new UIStackPanel
        {
            Orientation = UIOrientation.Vertical,
            Padding = UIEdgeInsets.All(8f),
        };
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
            content.AddChild(new UILabel { Text = $"Item {i}: scrollable line" });
        scrollBox.Content = content;
        root.AddChild(scrollBox);

        var canvas = new UICanvas(0) { Size = new Vector2(400f, 400f), Root = root };
        canvas.Update(default, CreateTextRenderer());

        // 滚到中间
        scrollBox.ScrollOffset = new Vector2(0f, 200f);
        canvas.Update(default, CreateTextRenderer());

        var ui = new UIManager();
        canvas.Paint(ui);

        // ScrollBox 视口（Bounds 内，StackPanel 给了实际位置）
        var viewport = scrollBox.Bounds;
        var primitives = ui.Primitives.Span.ToArray();
        Assert.NotEmpty(primitives);

        // 所有基元的 scissor 必须在 ScrollBox 视口内（不能是全窗口/无裁剪）
        var clippedCount = 0;
        foreach (var prim in primitives)
        {
            if (prim.ScissorRect.Z > 0f && prim.ScissorRect.W > 0f)
            {
                clippedCount++;
                Assert.True(prim.ScissorRect.X >= viewport.X - 0.01f,
                    $"Scissor X ({prim.ScissorRect.X:F1}) must be >= viewport X ({viewport.X:F1})");
                Assert.True(prim.ScissorRect.Y >= viewport.Y - 0.01f,
                    $"Scissor Y ({prim.ScissorRect.Y:F1}) must be >= viewport Y ({viewport.Y:F1})");
                Assert.True(prim.ScissorRect.X + prim.ScissorRect.Z <= viewport.Right + 0.01f,
                    $"Scissor right ({prim.ScissorRect.X + prim.ScissorRect.Z:F1}) must be <= viewport right ({viewport.Right:F1})");
                Assert.True(prim.ScissorRect.Y + prim.ScissorRect.W <= viewport.Bottom + 0.01f,
                    $"Scissor bottom ({prim.ScissorRect.Y + prim.ScissorRect.W:F1}) must be <= viewport bottom ({viewport.Bottom:F1})");
            }
        }

        // 滚动后大量内容应被裁剪（50 行滚出视口的大部分）
        Assert.True(clippedCount > 0, "Scrolled ScrollBox should clip content");

        // 关键：不应有「内容基元」完全没有 scissor（否则渲染层重置全视口，内容越出可见）。
        // 完全滚出视口的项应带「负尺寸 scissor」（完全裁剪标记），而非无 scissor。
        foreach (var prim in primitives)
        {
            bool fullyClipped = prim.ScissorRect.Z < 0f || prim.ScissorRect.W < 0f;
            bool hasClip = prim.ScissorRect.Z > 0f && prim.ScissorRect.W > 0f;
            bool noClip = !fullyClipped && !hasClip;

            Assert.False(noClip,
                $"Content primitive must not be unclipped: Rect=({prim.Rect.X:F0},{prim.Rect.Y:F0},{prim.Rect.Z:F0}x{prim.Rect.W:F0}) Scissor={prim.ScissorRect}");
        }
    }
}
