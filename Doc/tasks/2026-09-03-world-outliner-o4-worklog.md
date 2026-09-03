# World Outliner O4 高级组织工作日志

日期：2026-09-03

## 本轮目标

按 `World-Outliner-Functional-Spec.md` 完成 O4 的可安全基础设施：提供最多四个独立 Outliner、开放稳定扩展点，
并建立 Level/Data Layer/未加载 Actor 的编辑器数据模型，同时不伪装当前尚不存在的子场景流送能力。

## 完成内容

- 新增标签页式 `EditorOutlinerHost`，最多创建四个实例；主实例不可关闭，其余实例可关闭。
- 每个实例使用 `primary`、`secondary-1`、`secondary-2`、`secondary-3` 独立 ViewState 槽位，分别保存搜索、
  Filter、列显示/宽度/排序、展开、滚动与 ActiveWorld/EditorWorld 数据源。
- 所有实例共享底层 World、EditorSelection 和同一套编辑命令回调；切换实例时按 ActorGuid/ComponentGuid 在
  EditorWorld 与 RuntimeWorld 间恢复有效选择。
- 新增 `EditorOutlinerExtensionRegistry`：支持注册稳定 ID 的信息列、实例过滤器、上下文动作和节点提供器。
- 扩展列可以设置默认显示、宽度、文本、排序键与是否参与搜索；隐藏扩展列仍可通过普通词或列 ID 字段查询。
- 扩展 Filter 与 Custom Filter 一起按实例持久化；扩展上下文动作声明是否修改 World，Play 只读时自动禁用。
- 节点提供器通过稳定 Node ID 和 Parent ID 接入现有树；重复 ID、父级循环和扩展异常不会破坏内置树。
- 固定 Type/Socket/ID 列迁移到 descriptor 驱动，同时保留旧 `EditorOutlinerColumn` 状态字段以兼容已有 JSON。
- SceneDocument 升级到 v7，并继续读取 v5/v6。新增 Editor Level、Data Layer、Actor Level/Data Layer 归属和
  未加载 Actor descriptor 的二进制往返。
- `null` Level 表示隐式 Persistent Level；Data Layer 保持多对多编辑器标签，不参与 Transform。
- 未加载 Actor 只保留 Guid/Label/Type/Level/Data Layer，Outliner 显示只读 `UNLOADED` 行；编辑器实例化不会
  创建对应 Actor，RuntimeWorld 完全忽略组织元数据。
- 新增默认关闭的 Level 与 Data Layer 列，统一参与搜索、排序和列宽持久化。

## 边界说明

- 本轮没有实现 World Partition、流送开关或 One File Per Actor。
- Level Instance 需要子场景资产引用、加载生命周期与实例 Transform，当前不创建占位 Actor，也不提供会误导
  用户的伪加载命令。
- 停靠框架尚未完成，因此多实例先使用单一 Outliner 区域内的标签页，不引入浮动窗口。

## 验证

- 覆盖扩展列显示、数值排序、隐藏列搜索、扩展 Filter 和扩展节点显示。
- 覆盖四实例上限、独立查询状态、关闭次级实例与主实例不可关闭。
- 覆盖 Scene v7 Level/Data Layer/未加载 Actor 往返、编辑器恢复、只读行与 RuntimeWorld 忽略行为。
- 覆盖 v6 空场景升级到 v7；既有 v5 兼容测试继续通过。
- 全量测试：`281/281` 通过。
