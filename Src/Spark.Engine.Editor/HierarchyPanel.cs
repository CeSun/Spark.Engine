using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.UI;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>
/// 场景层级面板（编辑器 MVP 第一步）：把 <see cref="World"/> 的 Actor → Component 树显示为
/// <see cref="UITreeView"/>。结构变化（Actor/Component 增删）时签名比对后全量重建，保留展开/选中态。
/// </summary>
public sealed class HierarchyPanel
{
    private readonly World _world;
    private readonly UITreeView _tree;

    private string _lastSignature = string.Empty;

    /// <summary>选中项变化：参数为选中的 Actor/Component 或 null。</summary>
    public Action<object?>? SelectionChanged { get; set; }

    public HierarchyPanel(World world)
    {
        _world = world;
        _tree = new UITreeView { BackgroundColor = new(0f, 0f, 0f, 0f) };
        _tree.FixedSize = new UISize(0f, 0f); // ≤0 = 拉伸填满（否则高度只有一行 ItemHeight）
        _tree.SelectionChanged += item => SelectionChanged?.Invoke((item as WorldTreeItem)?.Target);
    }

    /// <summary>树控件本身（挂进编辑器布局）。</summary>
    public UIElement Element => _tree;

    /// <summary>当前选中的目标（Actor 或 Component；无选中为 null）。</summary>
    public object? SelectedTarget => (_tree.SelectedItem as WorldTreeItem)?.Target;

    /// <summary>每帧调用：结构签名变化时重建树（O(n) 快速比对，展开/选中态跨重建保留）。</summary>
    public void Refresh()
    {
        var signature = BuildSignature();
        if (signature == _lastSignature)
            return;

        _lastSignature = signature;
        Rebuild();
    }

    private string BuildSignature()
    {
        // 内容无关的结构指纹：Actor 引用序列 + 每个 Actor 的组件引用序列
        // （World.Update 后 Add/Remove 已生效；组件只增不减，引用序列足够判断结构变化）
        var sb = new System.Text.StringBuilder();
        foreach (var actor in _world.Actors)
        {
            sb.Append(actor.GetHashCode()).Append(':').Append(actor.Name).Append(';');
            foreach (var component in actor.Components)
                sb.Append(component.GetType().Name).Append(',');
        }
        return sb.ToString();
    }

    private void Rebuild()
    {
        var selected = SelectedTarget;

        _tree.Clear();
        foreach (var actor in _world.Actors)
        {
            var displayName = string.IsNullOrWhiteSpace(actor.Name) ? actor.GetType().Name : actor.Name;
            var actorItem = new WorldTreeItem(actor, $"{displayName} [{actor.Components.Count()}]");
            foreach (var component in actor.Components)
                actorItem.AddSubItem(new WorldTreeItem(component, component.GetType().Name));
            _tree.AddRoot(actorItem);
        }

        // 恢复展开状态 + 选中态
        _tree.ExpandAll();
        if (selected != null && FindItem(_tree.Roots, selected) is { } item)
            _tree.SelectItem(item);
    }

    private static WorldTreeItem? FindItem(IReadOnlyList<UITreeViewItem> items, object target)
    {
        foreach (var item in items)
        {
            if (item is WorldTreeItem worldItem && ReferenceEquals(worldItem.Target, target))
                return worldItem;
            if (FindItem(item.SubItems, target) is { } sub)
                return sub;
        }
        return null;
    }

    public void SelectTarget(object? target)
    {
        if (target == null)
        {
            _tree.SelectItem(null);
            return;
        }

        if (FindItem(_tree.Roots, target) is { } item)
            _tree.SelectItem(item);
    }

    /// <summary>持有引擎对象引用的树项（选中回调向上抛 Target）。</summary>
    public sealed class WorldTreeItem : UITreeViewItem
    {
        /// <summary>绑定的 Actor 或 Component。</summary>
        public object Target { get; }

        public WorldTreeItem(object target, string text) : base(text)
        {
            Target = target;
            IsExpanded = true;
        }
    }
}
