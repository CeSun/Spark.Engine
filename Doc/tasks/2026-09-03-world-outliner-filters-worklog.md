# World Outliner 搜索与视图过滤工作日志

日期：2026-09-03

## 本轮完成

- World Outliner 标题区新增 `View` 菜单，提供 `Show Internal Actors`、`Show Components` 和 `Only Selected`。
- 新增搜索输入框，不区分大小写匹配 Actor 名称、Actor 类型和组件类型。
- 搜索命中 Actor 时保留其组件上下文；仅命中组件类型时只列出匹配组件。
- `Show Internal Actors` 默认关闭；开启后内部 Actor 和组件以弱化文本显示。
- `UITreeViewItem` 新增 `IsSelectable`、`IsDraggable` 和 `IsDropTarget`，鼠标、编程选择、键盘导航、范围选择和拖放统一遵守能力限制。
- `Only Selected` 使用独立选择快照，过滤重建不会丢失原始编辑器选择。
- `EditorUi` 暴露 Outliner 过滤状态，后续布局持久化无需操作内部控件。

## 边界

- 本轮不实现 Actor Folder、可见性列和锁定列。
- 本轮不持久化过滤状态；该能力随 E6 工作区布局统一落地。
- 本轮交付时编辑器视口相机仍由 `EditorViewportCameraActor` 承载；后续已由 `EditorViewportSession` 解耦，见同日视口会话工作日志。

## 验证

- 覆盖搜索、组件显示、内部对象显式显示、仅显示选中项、只读树项和 View 菜单交互。
- `dotnet test Tests/Spark.Engine.Tests/Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false`
- 结果：`240/240` 通过。
- Demo Desktop 使用独立临时输出目录构建，结果：0 警告、0 错误。
