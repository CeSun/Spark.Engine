# Spark.Editor World Outliner 功能规格

> 状态：O0–O4 已完成；Level Instance 的加载语义随子场景系统后续实施
>
> 日期：2026-09-03
>
> 参考：Unreal Engine 5.8 官方 Outliner 行为；实现优先级以本文为准

## 1. 定位与术语

当前左侧面板应统一称为 **World Outliner（场景大纲）**，职责是浏览和组织当前 World 中的 Actor，
而不是展示属性或完整组件结构。属性和 Component 选择属于 Details/Inspector。

UE 默认把 Outliner 放在右上、Details 放在其下方，但 Outliner 本身是可停靠面板，位置不是数据模型的一部分。
Spark 在 E6 工作区阶段应提供 UE 风格默认布局；在停靠系统完成前可以暂时保留左侧位置。

本文区分三类能力：

- **UE 对齐**：用户从 UE 带来的基础操作习惯，优先实现。
- **Spark 扩展**：项目已有或明确需要、但不应伪装成 UE 原生行为的能力。
- **后续能力**：依赖大型场景、子关卡或扩展 API，不进入首轮实现。

## 2. UE 行为基线

根据 Epic 官方文档，Outliner 的核心职责包括：

- 以层级树显示当前 Level 的 Actor，用于选择、修改和 Actor 挂载。
- 支持单选、多选、右键 Actor 上下文菜单，以及拖动 Actor 到 Actor 建立挂载关系。
- `F` 在 Outliner 聚焦时让视口聚焦选中 Actor；从视口选择 Actor 时可自动滚动并定位到对应行。
- 搜索支持部分匹配、多词 AND、`-` 排除、`+` 精确词；搜索范围包含隐藏列。
- 过滤条件可以保存为用户级自定义过滤器。
- 列可显示/隐藏、调整宽度和排序；UE 内置列体系包括 Label、ActorInfo、Level、Layer、Mobility、Socket、IDName 等。
- 行提供编辑器临时可见性开关；它与运行时 `Hidden in Game` 是两种语义。
- 支持创建、移动和删除 Actor Folder，并可把 Folder 设为当前编辑上下文，让新 Actor 自动进入该 Folder。
- 最多可打开四个 Outliner 实例，各自保存列与过滤配置。
- Play/Simulate 时可以显示运行时动态生成的 Actor。

官方参考：

- [Outliner](https://dev.epicgames.com/documentation/en-us/unreal-engine/outliner-in-unreal-engine)
- [Unreal Editor Interface](https://dev.epicgames.com/documentation/en-us/unreal-engine/unreal-editor-interface)
- [Actor Editor Context](https://dev.epicgames.com/documentation/en-us/unreal-engine/actor-editor-context-in-unreal-engine)
- [SceneOutliner API](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Editor/SceneOutliner)
- [FSceneOutlinerBuiltInColumnTypes](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Editor/SceneOutliner/FSceneOutlinerBuiltInColumnTypes)
- [World Outliner UI（Play/Simulate 行为参考）](https://dev.epicgames.com/documentation/en-us/unreal-engine/world-outliner-ui?application_version=4.27)

## 3. 当前实现审计

| 范围 | Spark 当前行为 | 目标行为 | 判断 |
|---|---|---|---|
| 树模型 | Folder → Actor → 挂载 Actor；Component 默认隐藏 | Folder + Actor；Actor 子行表达跨 Actor 挂载 | O1 已完成 |
| Actor 挂载 | 被挂载 Actor 直接嵌套在父 Actor 下 | 被挂载 Actor 直接嵌套在父 Actor 下 | O0 已完成 |
| Component | 默认隐藏，仅可从 Developer 选项临时显示且不可拖放 | 默认不在 Outliner 显示，在 Details 中选择 | O0 已完成 |
| 行标签 | Actor Label + 类型颜色标记 | 类型图标 + Actor Label；统计信息放可选列或 Tooltip | O0 占位完成 |
| Folder | 稳定 FolderGuid、空/子 Folder、当前 Folder、保存/重载 | 支持空 Folder、子 Folder、移动和当前 Folder | O1 已完成 |
| 可见性 | Eye 会话级临时隐藏，Folder 级联/混合，影响预览和拾取 | 会话级临时隐藏，Folder 支持级联和混合状态 | O1 已完成 |
| 选择 | 单选、Ctrl/Shift 多选；Component 映射 Owner；可配置自动定位；Play/Edit 独立恢复 | 保留；增加可配置的自动定位 | O3 已完成 |
| 展开状态 | 按 ActorGuid 保留；搜索/Only Selected 临时展开必要祖先 | 按稳定节点 ID 保留，过滤只临时展开祖先 | O0 已完成 |
| 搜索 | 多词 AND、排除、精确词/短语和字段查询；默认不隐式匹配 Component | 多词 AND、排除、精确匹配、字段查询 | O2 已完成 |
| 过滤 | Filter 菜单、类型过滤、Only Selected、临时隐藏过滤和 Custom Filter | Filter 菜单、类型过滤、自定义过滤器 | O2 已完成 |
| 列 | Label/Type/Socket/ID 表头、显示切换、拖动列宽和同级排序 | Label 主列和可选信息列，可调宽、排序 | O2 已完成 |
| 上下文菜单 | Actor、Folder、空白基础菜单；复用现有选择/复制/删除/聚焦命令 | 与视口共用 Actor 命令；Folder 有独立命令 | O1 已完成基础 |
| 重命名 | `F2`/菜单进入真实 `UITextBox` 行内编辑 | 行内编辑，提交/取消和冲突校验 | O1 已完成 |
| 拖放 | Actor→Actor/Folder/空白、Folder→Folder/空白、Asset→Folder | 挂载、组织和创建三类语义明确区分 | O1 已完成 |
| Play | 默认展示 Active RuntimeWorld，可切 EditorWorld；PIE 行与只读状态明确 | 默认浏览 ActiveWorld，并标识运行时生成对象 | O3 已完成 |
| 性能 | 整数版本空闲刷新、节点/搜索记录复用、批量建树和可视行虚拟化 | 增量模型、虚拟化、稳定滚动与选择 | O3 已完成 |
| 多实例 | 标签页式最多四个实例，独立查询/列/展开/滚动/数据源 | 最多四个实例，共享 World 与选择 | O4 已完成 |
| 扩展 | 稳定 ID 的节点提供器、列、过滤器和上下文动作注册表 | UI 不依赖插件具体 Actor 类型 | O4 已完成 |
| 世界组织 | Scene v7 保存 Level/Data Layer/未加载 Actor descriptor | 不把组织元数据伪装为 Folder/Actor | O4 基础完成 |
| 内部对象 | 可过滤显示，显示后只读 | 保留为 Spark 调试扩展，默认关闭 | 合理扩展 |

## 4. 目标界面

### 4.1 面板结构

首轮目标结构：

```text
OUTLINER
[+ Folder] [ Search Actors...                 × ] [Filter ▾] [Settings ▾]
┌──┬──────────────────────────────┬──────────────┐
│眼│ Label                        │ Type         │
├──┼──────────────────────────────┼──────────────┤
│◉ │▾ Environment                 │ Folder       │
│◉ │  ├─ SM_Floor                 │ StaticMesh   │
│◉ │  └─▾ Building                │ Actor        │
│◉ │      └─ Lamp                 │ PointLight   │
│◉ │ CameraActor                  │ Camera       │
└──┴──────────────────────────────┴──────────────┘
```

约束：

- Label 是唯一固定主列；Eye 是固定窄列。
- Type 默认显示；其它信息列默认关闭。
- Component 数量不拼进 Label。
- 默认不显示 World 根节点和 Component 行，避免占用层级深度。
- Folder 和 Actor 使用不同图标；常见 Actor 类型可以注册专用图标。

### 4.2 默认布局

- UE 风格默认：Outliner 位于右上，Details 位于右下，Viewport 占据中央主要区域。
- E6 停靠系统允许用户把 Outliner 移回左侧，并持久化布局。
- 在停靠系统落地前，不为了移动面板重复修改固定 SplitPanel；先完成树语义和交互。

## 5. 树与数据模型

### 5.1 节点类型

首轮只有：

```text
OutlinerFolderNode
OutlinerActorNode
```

后续可扩展：`WorldNode`、`LevelNode`、`LevelInstanceNode`、`UnloadedActorNode`，但不得让 UI 直接依赖
具体 Actor 类型。

每个节点必须有跨刷新稳定的 ID：

- Actor：`ActorGuid`。
- Folder：`FolderGuid`，不能只用路径作为身份。

### 5.2 Folder 与 Transform Attachment 的边界

Folder 是编辑器组织关系，不参与 Transform、运行时 Tick 或组件注册；Attachment 是场景空间关系。

- 未挂载 Actor 按所属 Folder 显示。
- Actor 的 RootComponent 挂到另一个 Actor 的 SceneComponent 时，子 Actor显示在父 Actor 下。
- Actor 内部 Component 不生成 Outliner 行；具体挂载到哪个 Component/Socket 在 Tooltip、Socket 列或 Details 中展示。
- Folder 改名或移动不改变 Actor Transform。
- Actor→Folder 拖放只改变 Folder 归属；Actor→Actor 拖放只改变 Attachment。

为支持空 Folder 和稳定重命名，SceneDocument 增加编辑器组织元数据：

```text
SceneDocument.EditorFolders[] = { FolderGuid, ParentFolderGuid, Name }
SceneActorDocument.EditorFolderGuid
```

运行时实例化忽略这些字段；保存 EditorWorld 时保留。不要把 Folder 伪装成 Actor。

### 5.3 当前 Folder

- Folder 右键 `Make Current Folder` 后，新建、复制或从 Content Browser 创建的 Actor 自动归入该 Folder。
- 当前 Folder 使用强调图标，并在视口角落显示可清除的编辑上下文提示。
- 删除当前 Folder、切换场景或加载失败时必须安全清除上下文。

## 6. 交互规格

### 6.1 选择与定位

- 单击替换选择；Ctrl 单击切换；Shift 单击按当前可见行范围选择。
- Folder 行默认不进入 Actor 选择集合；单击只激活 Folder，双击切换展开。
- 从视口选中 Actor 时，Outliner 展开祖先并滚动到该 Actor。
- Settings 中提供 `Always Frame Selection`，默认开启；关闭后只同步高亮，不强制滚动。
- `F`：视口聚焦主选 Actor；当选择来自视口且焦点在 Outliner 时，确保行可见。
- `Esc` 清除 Actor 选择；左右键展开/折叠，上下键移动主选行。

### 6.2 行内重命名

- `F2`、慢速双击 Label 或上下文菜单 `Rename` 进入 `UITextBox` 行内编辑。
- Enter/失焦提交，Escape 取消；空白、非法字符和同级 Folder 重名不提交并显示错误。
- Actor Label 允许重复，但应提供明确的 ActorGuid/Type 辅助辨识。
- Actor/Folder 重命名必须经过命令历史；一次提交对应一次 Undo。

### 6.3 上下文菜单

Actor 菜单至少包含：

- Focus Selected
- Rename
- Duplicate
- Delete
- Attach To / Detach
- Move To Folder
- Select Children

Folder 菜单至少包含：

- New Subfolder
- Make Current Folder / Clear Current Folder
- Rename
- Select Descendant Actors
- Move To
- Delete

空白区域菜单至少包含 `New Folder` 和 `Clear Current Folder`。

Actor 菜单必须与 Viewport 共用同一命令服务和可用性判断，不能在两个 UI 中复制删除、复制、重命名逻辑。
非空 Folder 的删除属于破坏性操作：实现前需用目标 UE 版本实机确认其精确交互；Spark 不允许静默删除后代 Actor。

### 6.4 拖放

| 来源 → 目标 | 行为 |
|---|---|
| Actor → Actor | 挂载 Actor RootComponent，默认 Keep World；非法循环显示禁止光标 |
| Actor → Folder | 改变 Folder 归属，不改变 Transform |
| Actor → 空白 | Detach 并移到根 Folder；执行前展示明确 Drop 提示 |
| Folder → Folder | 改变 Folder 父级；禁止移入自身后代 |
| Content asset → Folder | Spark 扩展：创建对应 Actor 并归入 Folder |
| Content asset → Actor | Spark 扩展：默认拒绝，避免“创建”与“挂载”隐式耦合；通过上下文命令明确选择 |
| OS 文件 → Folder | Spark 扩展：先导入当前 Content 目录；只有可实例化资源才在确认后创建 Actor |

多选拖放按顶层选择集执行，不能重复移动已被另一选中 Actor 包含的子树。整个操作合并为一个 Undo 事务。

### 6.5 临时可见性

- Eye 只控制编辑器会话中的临时隐藏，不修改运行时 Visibility/HiddenInGame，不把场景标脏。
- Folder Eye 级联影响后代 Actor；子项状态不一致时显示混合状态。
- 被隐藏 Actor 仍保留在 Outliner 中，可再次显示；视口拾取跳过有效隐藏对象。
- 会话状态以 ActorGuid/FolderGuid 保存，Reload 后对仍存在的对象恢复，关闭编辑器后可清除。
- Internal Actor 的显示过滤与 Eye 状态独立。

## 7. 搜索、过滤与列

### 7.1 搜索语法

P2 最小语法：

- `Sky`：部分匹配。
- `Sky Light`：所有词都必须匹配。
- `-Sky`：排除匹配词。
- `+Sky`：完整词匹配。
- `"Sky Light"`：完整短语匹配。
- `type:Camera`、`folder:Lighting`、`id:<guid>`：字段查询。

匹配源包含 Actor Label、Actor 类型、Folder 路径和所有已注册且可检索的列，即使该列当前隐藏。扩展列的稳定
ID 同时成为字段名，例如 `owner.team:lighting`。Component 类型不再作为
默认隐式搜索源；后续如需搜索 Component，应通过 `component:CameraComponent` 显式查询。

过滤时显示所有匹配节点的必要祖先，但不改变持久展开状态。清空搜索恢复过滤前的展开、滚动和选择位置。

### 7.2 Filter 菜单

首轮：

- Actor 类型过滤。
- Only Selected。
- Hide Temporarily Hidden。
- Show Internal Actors（Spark 调试扩展，默认关闭）。
- 保存当前搜索和类型条件为 Custom Filter。

`Show Components` 从默认 View 菜单移除。若保留调试能力，放入 `Developer → Show Components`，默认关闭，
并明确这不是标准 Actor Browsing 模式。

### 7.3 列系统

| 列 | 默认 | 说明 |
|---|---:|---|
| Eye | 开 | 固定窄列，不参与普通排序 |
| Label | 开 | 图标、展开箭头、名称；主排序列 |
| Type | 开 | Actor 类型 |
| Mobility | 关 | Static/Stationary/Movable；引擎有对应概念后接入 |
| Socket | 关 | 跨 Actor 挂载目标的 Component/Socket |
| ID | 关 | ActorGuid，调试用 |
| Level/Data Layer | 后续 | 等子关卡/Data Layer 数据模型落地后注册 |

表头支持升序/降序、拖动调整宽度、右键显示/隐藏列。排序只改变同一父节点下的显示顺序，不改变 Folder 或 Attachment。

## 8. Edit 与 Play 模式

- Edit：展示 EditorWorld，可执行编辑命令。
- Play：默认切换到 Active RuntimeWorld，动态生成 Actor 可见；行使用 Play 标识。
- RuntimeWorld 默认只读，避免调试浏览意外写回；Focus、选择、搜索、临时隐藏仍可用。
- View 菜单提供 `Active World / Editor World` 数据源切换；每个数据源保存独立选择、展开和滚动状态。
- Play 期间切回 Editor World 只用于对照查看，场景编辑命令保持锁定，避免修改 Play 快照来源。
- Stop 后恢复 EditorWorld 选择；不存在的运行时 Actor 引用必须立即释放。

## 9. 状态持久化

| 状态 | 保存位置 | 生命周期 |
|---|---|---|
| Folder 与 Actor Folder 归属 | SceneDocument | 随场景保存、Undo/Redo |
| 列显示、列宽、排序、过滤器 | 用户/项目布局配置 | 跨启动 |
| 展开、滚动、当前 Folder | 场景视图状态 | 跨场景切换，可跨启动 |
| Actor 临时可见性 | Editor Session | 不写入场景 |
| 选择集合 | Editor Session | Reload 尽量按 Guid 恢复 |

多个 Outliner 实例必须拥有独立 ViewState，不能共享搜索、列、展开或滚动状态；它们可以共享底层只读模型和
EditorSelection。

### 9.1 高级组织元数据

- `SceneDocument` v7 新增 Editor Level、Data Layer、Actor 归属和未加载 Actor descriptor；v5/v6 继续可读。
- `EditorLevelGuid == null` 表示隐式 `Persistent Level`，旧场景无需批量迁移。
- Data Layer 是 Actor 的多对多编辑器标签，不参与 Transform，也不在当前阶段提供流送开关。
- 未加载 Actor 只有稳定 ActorGuid、Label、Type、Level/Data Layer 元数据；Outliner 以 `UNLOADED` 只读行展示，
  不创建 Actor 实例，不进入 RuntimeWorld，也不能执行变换或场景编辑命令。
- Level 与 Data Layer 作为默认关闭的信息列接入通用列注册机制。
- Level Instance 仍依赖子场景资产引用、加载生命周期和实例 Transform；这些语义在子场景系统落地前不提供
  占位 Actor 或误导性的编辑命令。

## 10. 实现边界

建议拆分：

```text
EditorOutlinerModel          World/Folder/Attachment → 稳定节点图
EditorOutlinerQuery          搜索语法、类型过滤、祖先保留
EditorOutlinerViewState      展开、列、排序、滚动、过滤器
EditorOutlinerCommandService Folder、Rename、Attach、Move、Visibility
EditorHierarchyPanel         UI 组合与事件转发
UITreeView / UITableTree     虚拟化行、列、行内编辑、上下文菜单、Drop 提示
```

约束：

- `EditorHierarchyPanel` 不直接修改 World 或 SceneDocument。
- Folder、Actor 和 Attachment 操作必须走 `IEditorCommand`。
- ViewState 操作不进入场景 Undo 栈，也不能标记 SceneDocument 脏。
- UI 不通过显示文本恢复身份，所有操作使用稳定 Guid。
- World 变化使用事件或变更版本，不继续每帧拼接完整字符串签名。

## 11. 实施顺序

### ✅ O0：纠正 Outliner 语义（P0，已完成）

1. ✅ 默认只显示 Actor，移除 Label 中 Component 数量。
2. ✅ 根据跨 Actor RootComponent Attachment 构建 Actor 父子树。
3. ✅ 用 ActorGuid 保存展开状态，取消重建后的无条件 `ExpandAll`；过滤上下文只临时展开祖先。
4. ✅ Actor→Actor 使用 Keep World 挂载；Developer Component 行不可作为拖放源或目标。
5. ✅ 补类型颜色图标占位，并让隐藏的 Component 选择映射高亮 Owner Actor。

验收：普通用户不在 Outliner 看到 Component；Actor 挂载层级与场景空间关系一致；刷新不改变展开、选择和滚动位置。

实现说明：重建期间已保持展开、选择和滚动位置；跨场景、跨启动的显式 ViewState 持久化随 O2 列和布局状态统一接入。

### ✅ O1：日常组织闭环（P1，已完成）

1. ✅ Folder 数据模型、SceneDocument v6 持久化、v5 读取兼容、空 Folder 和当前 Folder。
2. ✅ `+ Folder`、真实 `UITextBox` 行内重命名、Actor/Folder/空白上下文菜单。
3. ✅ Actor→Folder/空白、Folder→Folder/空白拖放及统一 Undo 事务。
4. ✅ Eye 临时可见性列、Folder 级联/混合状态、预览渲染与拾取过滤。
5. ✅ Content asset→Folder 的 Spark 扩展接入统一 Actor 创建流程。

验收：可完成“建 Folder → 放入/创建 Actor → 重命名 → 隐藏 → Undo/Redo → 保存/Reload”的完整工作流。

实现说明：Folder 是绑定 EditorWorld 的独立编辑器元数据，不是特殊 Actor；RuntimeWorld 忽略这些字段。
删除非空 Folder 会把直接内容安全提升到父 Folder，不会删除 Actor。当前 Folder 和 Eye 在 Reload 后按稳定 Guid
恢复，但不写入场景脏状态；O2 已将列、过滤、展开、滚动与 Current Folder 接入项目级用户 ViewState。

### ✅ O2：查找与信息架构（P2，已完成）

1. ✅ 多词、排除、精确词、短语和 `label/type/folder/id/socket/component` 字段搜索。
2. ✅ Filter 菜单、Actor 类型过滤、Only Selected、临时隐藏过滤和 Custom Filter。
3. ✅ Label/Type/Socket/ID 列，表头点击排序、显示切换与拖动列宽。
4. ✅ View 设置与 `Always Frame Selection`。
5. ✅ 独立 ViewState 按项目持久化搜索、列、排序、过滤器、展开、滚动和 Current Folder。

验收：数百 Actor 中可仅用键盘快速定位目标；清空过滤后不丢失原视图状态。

实现说明：过滤树只临时展开命中节点的必要祖先；清除搜索或过滤后恢复过滤前的展开和滚动。
排序仅作用于同一父节点的显示顺序，Folder 始终位于 Actor 之前，不修改 Folder 归属、Attachment 或场景脏状态。

### ✅ O3：Play 与规模性能（P3，已完成）

1. ✅ ActiveWorld/EditorWorld 数据源切换、PIE 行标记和运行时只读标识。
2. ✅ World 结构版本驱动动态 Actor 加入/移除，并复用未变化节点。
3. ✅ 树行窗口虚拟化、批量根节点更新和按结构版本失效的搜索索引。
4. ✅ 一万 Actor 构建、空闲刷新、滚动/选择与动态增删性能基线测试。

验收：Play 中动态 Actor 可检索；Stop 无悬空选择；一万 Actor 下滚动和选择不触发整树重建。

实现说明：World 在 Actor 接受加入/移除、名称、组件、RootComponent 和 Attachment 变化时递增
`StructureRevision`；Outliner 空闲帧只比较整数版本。树保留完整逻辑节点用于键盘导航，但 UI 树只挂载视口附近
的行。Play 与 Edit 分别保存选择、Actor 展开和滚动状态，Stop 前清除运行时对象引用并恢复编辑选择。

### ✅ O4：高级世界组织（已完成基础设施）

1. ✅ 标签页式最多四个 Outliner 实例；每个实例使用 `primary` / `secondary-1..3` 独立持久化槽位。
2. ✅ 可注册节点提供器、列、过滤器和上下文动作；扩展异常不破坏内置 Actor/Folder 树。
3. ✅ SceneDocument v7 的 Level、Data Layer、Actor 归属和未加载 Actor descriptor 往返兼容。
4. ✅ Level/Data Layer 可检索列与 `UNLOADED` 只读节点；RuntimeWorld 忽略全部纯编辑器组织元数据。
5. ⏳ Level Instance 等待子场景系统提供资源引用、加载生命周期和 Transform 语义后实施。

验收：四个实例的搜索与列状态互不覆盖；注册列可排序并在隐藏时参与搜索；Play 只读状态会禁用扩展修改动作；
v5/v6 场景升级到 v7 后仍使用隐式 Persistent Level；未加载 descriptor 不实例化进 RuntimeWorld。

## 12. 首轮非目标

- 不在 O0/O1 实现 World Partition、Data Layer 或 One File Per Actor。
- 不把 Details 的 Component 树复制到 Outliner。
- 不把 Folder 实现为特殊 Actor。
- 不把临时 Eye 状态写成运行时可见性属性。
- 不在停靠框架完成前投入复杂浮动/停靠窗口；四实例先使用同一面板内的轻量标签页。
