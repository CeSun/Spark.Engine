# World Outliner O3 Play 与规模性能工作日志

日期：2026-09-03

## 本轮目标

按 `World-Outliner-Functional-Spec.md` 完成 O3，使 World Outliner 能在 Play 中浏览动态 RuntimeWorld，
同时消除空闲帧全量扫描，并让万级 Actor 的滚动和选择保持稳定。

## 完成内容

- View 菜单新增 `Active World` / `Editor World` 数据源选择；Edit 状态两者均指向 EditorWorld，Play 默认切到
  RuntimeWorld，也可在不中止 Play 的情况下回看 EditorWorld。
- RuntimeWorld 标题和状态栏显示 `PLAY · READ ONLY`，Actor 行显示 `PIE` 标记；Play 期间两个数据源均锁定
  场景编辑，运行时行仍支持选择、搜索、聚焦与 Eye 临时隐藏。
- Play 开始时按 ActorGuid/ComponentGuid 把编辑选择映射到运行时副本；两个数据源分别记忆选择。
  Stop 前主动清除运行时选择和行缓存，随后恢复 EditorWorld 原选择，避免已释放运行时对象悬挂。
- EditorWorld 与 RuntimeWorld 分别保存 Actor 展开和滚动位置；搜索、列、排序等实例设置继续共享。
- `World.StructureRevision` 和 `StructureChanged` 覆盖 Actor 接受加入/移除、Actor 名称、动态组件、
  RootComponent 与 Attachment 变化。
- `HierarchyPanel.Refresh()` 从每帧拼接完整结构字符串改为比较 World、Outliner、View 三个整数版本；
  空闲帧不再枚举 Actor、组件或重新排序。
- Actor 加入/移除使用包含 pending 状态的逻辑 World 视图，因此 gameplay 动态生成对象无需额外等待一帧注册
  即可进入 Outliner，被请求移除后也会立即离开。
- 树节点按对象身份缓存和复用；动态加入仅创建新行对象，移除会清理对应缓存引用。
- 查询记录按对象缓存，仅在 World 或 Folder 数据版本变化时失效；输入不同搜索词可复用 Label、Type、Folder、
  Socket、ID 和 Component 字段记录。
- `UITreeView.SetRoots()` 支持一次批量替换，避免逐根节点反复扁平化导致的 O(n²) 构建。
- 新增虚拟树行面板：完整扁平列表只保存逻辑节点，视觉树仅挂载视口与前后 overscan 行；滚动条仍使用完整
  内容高度，键盘选择未实例化行时通过逻辑索引定位。

## 验证

- 覆盖 Play 默认 RuntimeWorld、EditorWorld 手动切换、PIE/read-only 行状态和 Stop 编辑选择恢复。
- 覆盖 RuntimeWorld 动态 Actor 搜索、删除后选择清理和运行时对象引用释放路径。
- 覆盖 World 结构版本对加入、移除、重命名和动态组件的响应。
- 新增 10,000 Actor 基线：逻辑行完整、视觉行保持在 32 个以内；连续 120 次空闲刷新和滚动/选择不触发
  Outliner 重建；动态加入只增加一个节点实例。
- 全量测试：`276/276` 通过。
- Demo Desktop 使用独立临时输出目录构建：0 警告、0 错误。

## 后续边界

- O4：多 Outliner 实例、Level/Data Layer、未加载 Actor，以及可注册节点、列、过滤器和上下文菜单扩展点。
- 当前结构变化按版本触发一次树拓扑重算并复用行对象；若未来达到十万级 Actor，再引入按父节点脏集更新。
