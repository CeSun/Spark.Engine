# World Outliner O1 日常组织闭环工作日志

日期：2026-09-03

## 本轮目标

按 `World-Outliner-Functional-Spec.md` 完成 O1，使 World Outliner 具备 UE 风格 Folder 组织、行内编辑、
上下文操作、临时可见性和完整保存/重载闭环。

## 完成内容

- 新增按 EditorWorld 绑定的 `EditorWorldOutlinerData`，以稳定 `FolderGuid` 管理 Folder、父子关系和 Actor 归属；Folder 不进入 `World.Actors`。
- `SceneDocument` 升级到 v6，保存 `EditorFolders` 和 `SceneActorDocument.EditorFolderGuid`；读取时兼容并升级 v5，RuntimeWorld 明确忽略编辑器 Folder 元数据。
- 空 Folder、嵌套 Folder 和 Actor 归属可保存/Reload；Current Folder 与 Eye 状态在同一编辑器会话 Reload 后按 Guid 恢复。
- 新增 Folder 创建、重命名、移动、删除和 Actor 移入 Folder 命令；非空 Folder 删除只把直接内容提升到父 Folder，不删除 Actor。
- 新增组合命令与 Detach 命令；Actor/Folder 拖到 Outliner 空白处可作为一次 Undo 事务回到根层，Actor 保持世界变换。
- `+ Folder` 创建后直接进入真实 `UITextBox` 行内重命名；F2、Enter、Escape、失焦提交以及同级 Folder 重名/非法名称校验走同一流程。
- Actor、Folder 和空白区域提供基础上下文菜单；Folder 可新建子 Folder、设为 Current、选择后代，Actor 可聚焦、复制、Detach、移到 Current Folder、选择子 Actor。
- 单击 Folder 只激活 Folder 行，不把 Folder 混入 Actor Selection；Current Folder 必须显式设置，并在标题与 Folder 图标上提示。
- Outliner 拖放明确区分 Actor→Actor 挂载、Actor→Folder 组织、Folder→Folder 组织，以及 Content Asset→Folder 创建 Actor。
- Actor 新建、复制、视口资源放置统一归入 Current Folder；直接拖到指定 Folder 的资源只归入该目标 Folder。
- Eye 列保存会话级 ActorGuid 集合，支持 Folder 级联和 Visible/Hidden/Mixed 三态；不会修改 SceneDocument 或标脏。
- 编辑器临时隐藏会同步 Mesh、SkeletalMesh 和 Light SceneProxy 的可见性/投影标志，视口 CPU 拾取同时跳过隐藏 Actor。
- Folder 名称参与现有搜索，过滤结果保留必要 Folder 与 Actor 挂载祖先；Folder/Actor 展开和滚动状态继续跨重建保留。

## 验证

- 新增 Folder 命令 Undo/Redo、循环拒绝、删除内容提升、创建 Actor 自动归属测试。
- 新增 Folder→Actor→挂载 Actor 树、Eye 混合状态与不标脏测试。
- 新增 SceneDocument v6 Folder/空 Folder 二进制往返、v5 读取升级、Editor/Runtime 实例化隔离测试。
- 新增 Reload 会话状态恢复和临时隐藏拾取过滤测试。
- 全量测试：`252/252` 通过。
- Demo Desktop 使用独立临时输出目录构建：0 警告、0 错误。

## 后续边界

- O2：多词/排除/字段搜索、Filter Bar、列系统、排序和跨启动 ViewState。
- O3：ActiveWorld 浏览、增量模型、虚拟化和万级 Actor 性能。
- 停靠系统完成前仍保留现有左侧 SplitPanel 位置，不在 O1 重复改固定布局。
