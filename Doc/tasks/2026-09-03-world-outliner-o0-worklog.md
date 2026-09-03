# World Outliner O0 语义纠正工作日志

日期：2026-09-03

## 本轮目标

按 `World-Outliner-Functional-Spec.md` 完成 O0，让左侧面板先具备 UE 风格 Actor 大纲语义，再进入 Folder、可见性和上下文菜单阶段。

## 完成内容

- Outliner 默认只显示 Actor；`Show Components` 改为默认关闭的 Developer 选项。
- Actor Label 不再附加 Component 数量，新增按 Camera、Light、Mesh 和普通 Actor 区分的颜色图标占位。
- 根据 Actor RootComponent 的跨 Actor AttachParent 构建父子树，组件内部挂载不再伪装成 Actor 层级。
- 过滤命中子 Actor 时保留并临时展开必要祖先，清空过滤后恢复过滤前的展开状态。
- 普通重建按 ActorGuid 保存展开状态，不再每次 `ExpandAll`。
- 树重建前后保留 ScrollOffset，选择恢复不会把用户正在浏览的位置强制拉走。
- Viewport/Inspector 选择 Component 时，Outliner 高亮 Owner Actor，同时不改变原始 EditorSelection 和 Inspector 目标。
- Developer Component 行仍可用于调试选择，但不可作为 Outliner 拖放源或目标。
- Actor→Actor 拖放直接使用 Keep World 挂载；移除默认 Socket/Transform Rule 弹层，非法循环由统一 Attach 命令拒绝。
- 面板标题调整为 `OUTLINER`，搜索提示调整为 `Search actors...`。

## 边界

- Folder、Eye 临时可见性、右键菜单和行内重命名属于 O1。
- 完整类型图标纹理、列系统、搜索语法与 ViewState 持久化属于 O2。
- 本轮保留 Component 类型的隐式搜索兼容性，因为当前 Demo 大量使用通用 Actor + 具体 RootComponent 表达 Actor 类型；O2 将以显式 ActorInfo/Component 查询统一语义。

## 验证

- 新增 Actor 挂载树、Label、图标占位、Component→Owner 选择映射、过滤临时展开和展开状态恢复测试。
- 新增 Outliner Actor 拖放 Keep World 与循环挂载拒绝测试。
- View 菜单测试更新为 Developer Components 默认关闭。
- `dotnet test Tests/Spark.Engine.Tests/Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false`
- 结果：`243/243` 通过。
- Demo Desktop 使用独立临时输出目录构建，结果：0 警告、0 错误。
