# Spark.Engine 编辑器落地实施计划

## 1. 目标与现状

编辑器的目标不是展示 World 数据，而是提供可持续使用的场景生产工具。当前已有：

- 层级树：Actor -> Component 展示和选择
- Inspector：基础属性反射、编辑和实时刷新
- Viewport：`UIRenderView` 注入和窗口自适应布局
- UI 基础控件：树、列表、分栏、菜单、工具栏、滚动、属性网格
- 初版 `EditorCommandHistory`：可执行、撤销、重做

当前状态：SceneDocument 和自定义二进制 `.scene` 保存/读取基础已落地；Viewport 还不能拾取和变换对象，
`EditorContext` 已接入 Play/Stop 状态机，可从 `SceneDocument` 创建并释放独立 RuntimeWorld；主循环已支持
EditorWorld 与 RuntimeWorld 并存，内置静态/骨骼资产和光照状态可恢复，宿主行为可通过初始化器注入。
场景层级、Socket 和挂载规则按 [SceneHierarchy-Design.md](./SceneHierarchy-Design.md) 实施，资产格式和 Cook 按
[AssetPipeline-Design.md](./AssetPipeline-Design.md) 实施。编辑器侧已落地无 GPU 依赖的 glTF 2.0 StaticMesh 导入器，
支持内嵌/外部 buffer、TRS 节点层级和 TRIANGLES 原语；GLB、骨骼、动画和材质纹理导入仍在后续里程碑。

## 2. 架构边界

```text
EditorApplication
├── EditorContext       当前场景、选择、模式、脏状态
├── EditorCommandHistory 撤销/重做和事务
├── EditorSelection      单选、多选、树/视口同步
├── EditorSceneService   新建、加载、保存、恢复（`.scene`）
├── EditorAssetService   资源索引、搜索、引用
├── EditorInput          快捷键和鼠标命令路由
└── EditorUi              只负责布局、呈现和事件转发
```

约束：UI 不直接编排复杂 World 修改；所有修改通过服务或 `IEditorCommand` 完成。IO、反射扫描、资源编译不得阻塞渲染线程。

## 3. 里程碑计划

### M0：工作台稳定性（当前）

交付：菜单栏、工具栏、层级树、Viewport、Inspector、状态栏和响应式布局。

验收：窗口缩放后左右面板保持宽度、Viewport 填充剩余区域；树节点可选中；编辑器相关测试全通过。

### M1：编辑闭环

交付任务：

1. `EditorContext`：`CurrentWorld`、`Selection`、`Mode`、`IsDirty`。
2. `PropertyChangeCommand`：记录对象、属性、旧值、新值，接入 Inspector。
3. `CreateActorCommand`、`DeleteActorCommand`、`DuplicateActorCommand`。
4. Actor 重命名和删除确认弹窗。
5. 快捷键路由：`Ctrl+S/Z/Y`、`Delete`、`F2`、`W/E/R`。
6. 命令失败回滚和状态栏错误反馈。

验收：完成“创建 -> 选择 -> 修改 -> 撤销 -> 重做 -> 删除 -> 恢复”的完整流程；任何失败操作不留下半状态。

### M2：场景持久化

交付任务：

1. ✅ `SceneDocument`、二进制 `.scene` 格式和版本号。
2. ✅ `BinaryEditorSceneService.Save/LoadDocument`；`Reload(World)` 当前只校验并缓存文档。
3. 脏状态、关闭确认、最近文件。
4. 自动保存和崩溃恢复文件。
5. 加载错误面板和不可恢复资源引用提示。

验收：序列化 round-trip 保留场景 GUID、Actor/Component GUID、挂载关系、Socket 和相对变换；不支持的版本给出明确错误。

### M3：Viewport 编辑

交付任务：

1. 射线拾取和树/视口双向选择。
2. Transform Gizmo：平移、旋转、缩放。
3. 网格、吸附、世界/局部坐标。
4. 聚焦选中对象、相机控制和视图书签。
5. 所有 Gizmo 操作封装成可撤销命令。

验收：Viewport 中拖动对象后 Inspector 同步；撤销/重做恢复精确变换；多选不会误改未选对象。

### M4：运行与调试

交付任务：

1. `Edit/Play/Simulate/Stop` 状态机。
2. ✅ 从 `SceneDocument` 实例化 Runtime World、Play/Stop 回收、双 World 调度和内置资产/光照恢复；宿主行为初始化器已提供。
3. Console、日志过滤、错误定位。
4. RenderGraph、GPU 错误、帧耗时和 Draw Call 面板。
5. 帧捕获、截图和运行时对象定位。

验收：运行时修改不污染编辑场景；Stop 后编辑状态和选择恢复；异常可定位到 Actor/Component。

### M5：资源与扩展

交付任务：

1. 资源浏览器、索引、搜索和过滤（`SceneResource.AssetGuid` 已作为持久化身份基础）。
2. 引用选择器、缩略图、导入任务队列。
3. Inspector 自定义绘制器和面板注册 API。
4. 菜单、工具栏、Gizmo 扩展点。
5. 后台资源编译、进度、取消和失败重试。

验收：新增资源类型无需修改核心 EditorUi；后台任务不阻塞 UI 和渲染线程。

## 4. 测试策略

- 单元测试：命令历史、选择模型、序列化、版本拒绝/兼容性检查、快捷键映射。
- 布局测试：窗口缩放、最小尺寸、分栏拖动、滚动裁剪、焦点路由。
- 集成测试：创建/删除/属性修改/保存/加载完整工作流。
- GPU 验收：Viewport 拾取、Gizmo、RenderGraph Overlay、动态分辨率。
- 性能基线：1000/5000/10000 节点下树刷新、搜索和 Inspector 切换耗时。

## 5. 风险与控制

| 风险 | 控制措施 |
|---|---|
| UI 直接修改 World 导致无法撤销 | 强制所有修改走 Command/Service |
| 反射属性编辑写入错误类型 | 统一转换器，失败时保留旧值并显示错误 |
| Play 污染编辑场景 | 从 SceneDocument 实例化独立 Runtime World，停止时完整回收 |
| 大场景刷新卡顿 | 增量树更新、索引缓存、后台扫描 |
| 布局回归 | 每个控件补 Measure/Arrange/HitTest 回归测试 |

## 6. 当前执行顺序

1. `EditorContext` 和 `EditorSelection`
2. `PropertyChangeCommand` 接入 Inspector
3. Actor 创建、删除、复制和重命名
4. 快捷键、脏状态和保存确认
5. 场景序列化与加载服务
6. Viewport 拾取和 Gizmo

每完成一个任务，必须同时提交逻辑测试和至少一个端到端验收场景，避免继续积累“看起来有控件、实际不能工作”的功能。
