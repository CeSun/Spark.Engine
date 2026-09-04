# Actor 编辑器与工作区调整任务日志

日期：2026-09-04  
范围：编辑器工作区、World Outliner、Actor 资产与 Actor 编辑器

## 已落地

- 主工作区调整为「Viewport / Asset Editor | World Outliner」，Outliner 固定在右侧；窗口宽度变化时仍由分割面板保持比例。
- Details/Inspector 不再挂载到主工作区。Actor 属性编辑统一由 Actor Editor 的组件属性面板承载；浮动视口、Outliner、Content Browser 关闭恢复时不会重新挂回 Details。
- Outliner Actor 右键菜单新增 `Open Actor Editor`，可直接打开当前 Actor 的编辑标签页。
- 新增 `ActorAsset`，以 `SceneActorDocument` 保存 Actor 类型、组件类型、变换、挂载关系、Socket 和标记为 SceneProperty 的属性。
- `.asset` 编解码新增 Actor 类型，支持保存、加载、元数据扫描、资产复制时的组件资产引用重映射，并将组件引用列入依赖表。
- Content Browser 的新建菜单与右键菜单支持创建空白 Actor Asset（默认 SceneComponent 根节点）。
- Actor Editor 支持：组件列表选择、添加 Scene/Static Mesh/Camera/Point Light/Directional Light 组件、组件属性编辑、移除组件、设为根组件、保存 Actor Asset。
- Actor Editor 工作区按 UE 习惯调整为「左：组件树 | 中：透视预览 | 右：属性面板」，顶部集中放置保存和组件操作按钮。
- 中间预览列采用 Fill 测量语义，自动占满剩余空间并随窗口大小、分割线位置和 Content Browser 高度变化实时适应。
- 添加组件遵循 UE 层级规则：第一个 SceneComponent 自动成为 Root；后续有选中 SceneComponent 时挂到选中项下，未选中或选中非空间组件时挂到 Root 下，并自动选中新组件。
- Actor Editor 组件属性面板补充资源引用行；选中 `StaticMeshComponent` 后可通过下拉资产选择器或从 Content Browser 拖放设置 `Mesh`，并支持清空引用。
- 修复 Actor 预览 Mesh 链路：从 AssetRegistry 恢复磁盘 Actor 的 Mesh/Material 引用，并在透视预览中绘制 StaticMesh 线框；资源缺失时仍保留变换和标量属性预览。
- 预览面板支持场景视口式交互：左键点击组件选择，左键拖动组件调整位置，右键拖动旋转摄像机，中键拖动平移摄像机，滚轮缩放。
- 从场景 Outliner 打开的临时 Actor 编辑器在配置项目时自动提供 `Content/Actors/<ActorName>.asset` 保存路径；无项目上下文时需调用 `SaveActorAsAsset` 指定路径。
- 组件列表使用 `UITreeView` 展示真实父子关系；拖动组件到另一个 SceneComponent 可按 UE Attach 语义重挂（KeepWorld），拖到树空白处可解除挂载。
- 预览区使用透视视图矩阵（LookAt + PerspectiveFov），绘制透视网格、三轴和组件位置标记；属性修改后即时刷新。
- `Actor.RemoveOwnedComponent` 提供组件生命周期对称注销、解除挂载和根组件重选逻辑。

## 使用流程（UE 风格）

1. 在 Outliner 右键 Actor，选择 `Open Actor Editor`；或在 Content Browser 双击 `.asset`。
2. 使用 `+ Add Component` 添加组件，列表中选择组件后在右侧属性面板编辑；Vector/Rotation 按分量输入，Bool/Enum 使用下拉框。
3. 需要更换空间根节点时选择 SceneComponent 并点击 `Make Root`；删除非最后一个组件使用 `Remove Component`。
4. 点击 `Save Actor Asset` 保存回 Content 目录；Content Browser 重新扫描后可再次打开。

## 边界与后续

- 当前预览已使用透视矩阵，但仍是编辑器 UI 内的轻量几何标记，不替代完整 RenderTarget 3D Preview World；后续可接入独立 Preview World 和真实 Mesh/Material 渲染。
- 组件拖拽重挂、组件命名、Undo/Redo、Dirty 状态提示和实例化 Actor 尚未纳入本轮。
- 旧的 Inspector 资源字段测试针对已移除的 Details 布局，需要迁移为 Actor Editor 属性面板测试。

## 验证

- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false`：通过（0 警告、0 错误）。
- 资产编解码与编辑器控件相关测试：通过。
- 全量测试中保留 2 个旧 Inspector 布局断言失败，以及 1 个既有同步上下文时序失败；不恢复已取消的 Details 面板。
