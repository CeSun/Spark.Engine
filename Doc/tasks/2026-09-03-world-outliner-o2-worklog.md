# World Outliner O2 查找与信息架构工作日志

日期：2026-09-03

## 本轮目标

按 `World-Outliner-Functional-Spec.md` 完成 O2，让 World Outliner 具备接近 UE 的搜索、过滤、信息列、
排序和独立视图状态，并保持 O1 Folder/Attachment 语义不变。

## 完成内容

- 新增 `EditorOutlinerQuery`：空格分词按 AND 组合，支持 `-` 排除、`+` 完整词、双引号短语，以及
  `label:`、`type:`、`folder:`、`id:`、`socket:`、`component:` 字段查询。
- 普通搜索覆盖 Label、Type、Folder 路径、ID 与 Socket，即使对应列未显示；Component 类型只通过
  `component:` 显式检索，避免默认 Actor 浏览混入内部实现细节。
- 搜索和过滤命中会保留 Folder/Attachment 必要祖先并临时展开，退出过滤后恢复此前展开和滚动位置。
- Filter 菜单统一承载 Only Selected、Hide Temporarily Hidden、Show Internal Actors 和动态 Actor 类型；
  按钮显示活动条件数量，并提供 Clear Filters。
- 可把当前搜索与 Actor 类型保存为 Custom Filter，后续可直接应用或清空保存项。
- 新增固定 Eye/Label 主区和 Type、Socket、ID 信息列；Type 默认开启，其余默认关闭，长文本在列内截断。
- 新增列头：点击列名切换升降序，拖动信息列左边界调整列宽，右键打开列显示设置。
- 排序只重排同一父节点的 UI 行，Folder 始终先于 Actor；不修改 World Actor 顺序、Folder 归属或 Attachment。
- 行内重命名 `UITextBox` 只占 Label 列，不覆盖 Type/Socket/ID；Eye 固定在行左侧，不随树深度漂移。
- 新增 `EditorOutlinerViewState` 与项目级 JSON 存储，持久化搜索、过滤、列显示/宽度、排序、
  Always Frame Selection、Actor/Folder 展开、滚动和 Current Folder。
- ViewState 文件按项目路径哈希隔离并使用临时文件原子替换；损坏、不可读配置安全回退默认值，
  反序列化后恢复 Actor 类型过滤的大小写无关语义。
- `UITreeView.AutoScrollSelection` 接入 Always Frame Selection；树展开和滚动变化实时回写该 Outliner
  自己的 ViewState，不进入命令历史，也不标记场景脏。

## 验证

- 覆盖多词、排除、精确词、短语及全部字段查询，并验证普通查询不会隐式匹配 Component。
- 覆盖 Actor 类型与临时隐藏组合过滤、信息列内容、升降序显示重排且不改变 World 数据顺序。
- 覆盖展开/滚动回写、ViewState JSON 往返、大小写 comparer 恢复和损坏配置回退。
- 更新菜单交互测试，确认 Show Internal Actors 从 View 归入 Filter 后仍可显示不可编辑内部 Actor。
- 全量测试：`272/272` 通过。
- Demo Desktop 使用独立临时输出目录构建：0 警告、0 错误。

## 后续边界

- O3 实施 ActiveWorld/EditorWorld 数据源切换、运行时只读标识、增量树模型、虚拟化与规模基线。
- 当前结构变化仍通过每帧结构签名检测；事件驱动更新和搜索索引统一留给 O3。
- 多 Outliner、Level/Data Layer 和可注册列/过滤器扩展点仍属于 O4。
