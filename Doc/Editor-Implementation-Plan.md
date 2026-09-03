# Spark.Engine 编辑器落地实施计划

## 1. 目标与现状

编辑器的目标不是展示 World 数据，而是提供可持续使用的场景生产工具。当前已有：

- 层级树：Actor -> Component 展示和选择
- Inspector：基础属性反射、编辑和实时刷新
- Viewport：`UIRenderView` 注入和窗口自适应布局
- UI 基础控件：树、列表、分栏、菜单、工具栏、滚动、属性网格
- 初版 `EditorCommandHistory`：可执行、撤销、重做

当前状态：SceneDocument 和自定义二进制 `.scene` 保存/读取基础已落地；编辑器预览只执行组件注册和渲染代理刷新，不进入 BeginPlay/Update/EndPlay gameplay 生命周期，Viewport 已支持 CPU 包围球拾取、轴命中、Gizmo Overlay 和可撤销变换；选择模型已支持主选对象加选择集合，层级树 Ctrl/Shift 多选、Viewport 修饰键多选、批量删除和主选枢轴组变换已贯通。
`EditorContext` 已接入 Play/Stop 状态机，可从 `SceneDocument` 创建并释放独立 RuntimeWorld；主循环已支持
EditorWorld 与 RuntimeWorld 并存，内置静态/骨骼资产、光照状态和 Camera 视图参数可恢复，RenderTarget 按 ComponentGuid 精确绑定；Mesh/Texture 共享，Material 使用 RuntimeWorld 独立副本。`AssetRegistry` 已统一 AssetGuid 解析，
`RuntimeActorFactory` 已提供自定义组件和 Runtime 行为注册入口；旧的 `RuntimeWorldInitializer` 仅作为兼容接口保留。
场景层级、Socket 和挂载规则按 [SceneHierarchy-Design.md](./SceneHierarchy-Design.md) 实施，资产格式和 Cook 按
[AssetPipeline-Design.md](./AssetPipeline-Design.md) 实施。编辑器侧已落地无 GPU 依赖的 glTF 2.0 StaticMesh 导入器，
基于 SharpGLTF 支持 `.gltf`/`.glb`、内嵌/外部 buffer、TRS 节点层级和 TRIANGLES 原语；骨骼、动画和材质纹理导入仍在后续里程碑。

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
2. ✅ `BinaryEditorSceneService.Save/Load`；Reload 先构建并验证新的 EditorWorld，成功后通过 `WorldContext` 原子切换，失败不修改当前场景。
3. ✅ v5 保存 Actor 类型和 `[SceneProperty]` 类型化组件属性块；`[SceneTransient]` 排除仅运行时 Actor，旧版本明确拒绝且暂不迁移。
4. 脏状态、最近文件（`EditorRecentFiles` MRU 已完成，`EditorUi.RecentScenePaths` 已提供宿主菜单接入）；`EditorUi.RequestClose` 已接入支持取消关闭的 Desktop 原生窗口事件。
5. 自动保存和崩溃恢复文件。
6. 加载错误面板和不可恢复资源引用提示。

验收：序列化 round-trip 保留场景 GUID、Actor/Component GUID、具体类型、挂载关系、Socket、相对变换和显式标注属性；
EditorWorld 共享资产、RuntimeWorld 隔离可变材质；不支持的版本给出明确错误。

### M3：Viewport 编辑

交付任务：

1. ✅ 射线拾取和树/视口双向选择（首版 CPU 包围球，后续可替换 GPU ID buffer）。
2. ✅ Transform Gizmo：平移、旋转、缩放、主选对象枢轴、顶层选择过滤和单事务撤销/重做。
3. ✅ 网格吸附设置（平移/旋转/缩放增量、工具栏开关）和选择集合；世界/局部轴向组变换已接入。
4. ✅ 编辑器相机：右键 + WASD/QE 飞行、Middle 平移、Alt+Left 轨道、滚轮推拉、F 聚焦和 0..9 会话视图书签。
5. 所有 Gizmo 操作封装成可撤销命令。

验收：Viewport 中拖动对象后 Inspector 同步；撤销/重做恢复精确变换；多选不会误改未选对象。

### M4：运行与调试

交付任务：

1. `Edit/Play/Simulate/Stop` 状态机。
2. ✅ 从 `SceneDocument` 实例化 Runtime World、Play/Stop 回收、双 World 调度、注册/gameplay 生命周期隔离、AssetGuid 解析和内置资产/光照恢复；RuntimeActorFactory 已提供行为注册。
3. Console、日志过滤、错误定位。
4. RenderGraph、GPU 错误、帧耗时和 Draw Call 面板。
5. 帧捕获、截图和运行时对象定位。

验收：运行时修改不污染编辑场景；Stop 后编辑状态和选择恢复；异常可定位到 Actor/Component。

### M5：资源与扩展

交付任务：

1. ✅ 资源浏览器首版：基于 `AssetRegistry.Records` 的 `EditorContentBrowserModel` 与底部 `EditorContentBrowserPanel`，支持多级目录树、右侧文件夹项、搜索、类型过滤、刷新、导入状态/GUID/路径详情；双击/回车文件夹会进入目录，激活资源会在中间文档区打开对应的 StaticMesh、Material 或 Texture2D 编辑器标签。资源目录固定为项目 `Content`，启动时扫描其中的 `.asset` 元数据，快捷键为 `Ctrl+Shift+R`。无筛选时默认定位 `Textures`（存在时）并只显示当前目录直接资源，`All Assets` 显示 `Content` 根目录资源和直接子文件夹；启用搜索或类型筛选后递归匹配子目录。当前场景未保存资源通过 `Scene refs: On` 单独查看。新增 `EditorAssetImportService`/`EditorUi.ImportTexture`（PNG/JPG 等 ImageSharp 支持格式）和 `ImportModel`（glTF StaticMesh）入口，导入目标为当前 Content 目录且不会自动创建场景 Actor；Windows 桌面可将源文件直接拖入窗口导入。StaticMesh 可从内容浏览器拖入场景视口，按射线落点和网格吸附创建 Actor，并支持选择及 Undo/Redo。内容浏览器缩略图、引用关系和 Windows 文件选择器待补。
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

1. ✅ 编辑器选择集合：层级树 Ctrl/Shift 多选、Viewport 修饰键多选、主选对象和批量删除
2. ✅ 层级树拖拽挂载：Actor/Component 重挂、Socket 和挂载规则、原子撤销/重做
3. ✅ Actor 深复制：具体类型、组件、类型化属性、内部/外部挂载、Socket 和资产引用，支持多选批量复制并生成新 GUID
4. ✅ 编辑器相机：飞行/轨道/平移/推拉、F 聚焦、Reload/Play 瞬态相机隔离和会话视图书签
5. ✅ 多选组变换：以主选对象为枢轴，排除已选祖先下的重复后代，并以单事务撤销/重做

每完成一个任务，必须同时提交逻辑测试和至少一个端到端验收场景，避免继续积累“看起来有控件、实际不能工作”的功能。
