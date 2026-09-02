# Spark.Engine 项目设计与现状

> 状态：持续演进中
> 最后更新：2026-09-02
> 本文是项目级设计总览，明确区分「已实现」与「设计（未实现）」。
> 渲染管线详设见 [RenderPipeline-Design.md](./RenderPipeline-Design.md)；逻辑/渲染线程场景同步机制见
> [SceneSync-Design.md](./SceneSync-Design.md)。

## 概述

Spark.Engine 是一个用 C# 从零实现的跨平台游戏引擎，渲染后端基于 **WebGPU**（Silk.NET 绑定，
Native 实现为 wgpu），窗口基于 Silk.NET.Windowing，基础设施基于 Microsoft.Extensions.DependencyInjection
与 Serilog。场景对象模型借鉴 Unreal Engine（World → Actor → Component），逻辑/渲染线程同步借鉴 UE 的
FScene / FSceneProxy / FSceneRenderer 模式。

**差异化定位**：C# 原生 + WebGPU 跨平台现代渲染，纯 .NET 生态，工具链深度集成。

**最终目标**：通用游戏引擎，自举编辑器（用 Spark.Engine 自己的 UI 和渲染能力构建编辑器）。

当前处于**早期原型阶段**：核心渲染管线已具备前向光照（Blinn-Phong + 法线贴图 + 阴影贴图）、
声明式帧图（RenderGraph）、保留模式 UI 系统（控件树 + 输入 + overlay 渲染）、
场景代理同步（SceneGen 源生成器）。编辑器已具备工作台骨架、命令历史和 `.scene` 持久化基础，下一阶段优先补齐
UE 风格层级编辑、资产导入/Cook 和 Edit/Play 隔离。

## 解决方案结构

```
Spark.Engine.slnx
├─ Src/
│  ├─ Spark.Engine/          核心引擎库（net11.0，唯一 WebGPU 依赖点）
│  │  ├─ Math/               包围球/视锥（渲染线程剔除）
│  │  ├─ Resources/          CPU 侧资源资产：StaticMesh/Texture2D/Material/SkeletalMesh + ResourceManager
│  │  ├─ Render/             场景同步（Scene/SceneProxy/SceneSnapshot）
│  │  │  ├─ Common/          通用渲染类型：RenderTarget/RenderSurface/RenderTargetRegistry/Viewport/TextureRenderTarget/FrameTexture
│  │  │  ├─ Resources/       GPU 资源（IGPUResource + Mesh/Texture/Material/SkeletalMesh GPU 表示）
│  │  │  ├─ Pipeline/        管线抽象（IRenderPipeline/ShaderPass/IRenderStage/IGraphOverlay）+ BlinnPhong/
│  │  │  │  └─ BlinnPhong/   Blinn-Phong 管线（BlinnPhongRenderer + shader + MaterialShaderCache/Codegen + Stages/）
│  │  │  ├─ RenderGraph/     帧图（RenderGraph + 资源句柄/池 + 可视化 + 图形化配置基础）
│  │  │  └─ UI/              渲染线程 UI overlay（UIRenderer + UI.wgsl）
│  │  ├─ Input/              输入抽象（Key/KeyMask/MouseButton/WindowInput/InputState/InputManager）
│  │  ├─ UI/                 保留模式控件树（UIElement/UIPanel/UILabel/UIButton/UITextBox/UICheckbox/UISlider/UIProgressBar/UIStackPanel/UIGridPanel/UIDockPanel/UIWrapPanel/UIRenderView/UIManager/TextRenderer/UITheme）
│  │  ├─ Components/         组件（CameraComponent + LightComponent/Point/Directional/Spot + StaticMeshComponent + SkeletalMeshComponent）
│  │  ├─ Threads/            RenderThread（外壳）/EngineSynchronizationContext
│  │  └─ Worlds/             World（含 Scene）/WorldContext
│  ├─ Spark.Engine.SceneGen/ 源生成器（netstandard2.0，按 [SceneProxy]/[ScenePayload] 生成 proxy/payload）
│  ├─ Spark.Engine.Desktop/  桌面平台后端（net11.0，Silk.NET.Windowing + Silk.NET.Input）
│  └─ Spark.Engine.Editor/   编辑器（net11.0，工作台/场景文档/Play/导入/Cook）
├─ Demo/
│  ├─ Demo/                  演示内容类库（场景搭建 + WallSwinger + UI 验收场景，平台无关）
│  └─ Demo.Desktop/          桌面入口（引导 + 平台启动，引用 Demo）
├─ Tests/
│  └─ Spark.Engine.Tests/    单元测试（xunit，当前仅占位 + TextRenderer 回归测试）
└─ Doc/                      设计文档
```

## 架构分层

```
Demo（入口：EngineBuilder → InitializeWebGPU → UseDesktop → UseBlinnPhong → UseEditor → Build → Run）
  │
  ▼
EngineApplication（主循环：窗口事件 → 同步上下文 → 世界更新 → 填场景快照 → 提交）
  ├─ WindowManager（窗口生命周期 + Viewport 创建 + 原生窗口销毁握手）
  ├─ WorldContext → World（Actor 增删/更新 + Scene 场景注册表 + 相机收集）
  └─ DualFrameBuffer<SceneSnapshot>（双缓冲，逻辑→渲染）
       │
       ▼
RenderThread（线程外壳 → IRenderPipeline，DI 注入）
  └─ BlinnPhongRenderer : IRenderPipeline（上传处理 → 生命周期 diff → BuildGraph → Compile → Execute → 延迟删除）
       ├─ RenderGraph（声明式依赖图：ShadowDepthStage → BlinnPhongStage / SkeletalMeshStage → UIOverlay）
       ├─ RenderTargetRegistry（窗口视口 + 离屏目标注册表）
       ├─ IGPUResource 注册表（单注册表：几何/纹理/材质，上传一次）
       ├─ StaticMeshRenderState / SkeletalMeshRenderState 注册表（每实例 object uniform，按 ProxyId）
       └─ MaterialShaderCache（MaterialShaderKey × ShaderPass → ShaderModule + RenderPipeline，按 format 缓存）
```

---

## 一、已实现

### 1. 引导与依赖注入（Builder）

- `EngineBuilder.Create(args)`：配置 Serilog 日志（控制台 + 滚动文件）、`EngineOptions`、
  `ResourceManager`、`RenderTargetRegistry`、`WindowManager`
- `InitializeWebGPU()`：创建 instance 并注册 `WebGPUContext`；首个 surface 创建后按兼容性选择
  adapter，再创建 device/queue
- `UseDesktop()`：注册 `IWindowBackend`（桌面实现）
- `UseBlinnPhong()`：注册 `IRenderPipeline → BlinnPhongRenderer`（Blinn-Phong 前向渲染管线）
- `UseUI()`：注册 `IGraphOverlay → UIRenderer`（UI overlay 渲染）
- `EngineOptions`：`Width`/`Height`/`TargetFrameRate`

### 2. 平台抽象层

- `IWindow`：`Size`/`FramebufferSize`/`Title`/`IsClosing`/`Surface` + 生命周期方法 + `DisposeNative()`
- `IWindowBackend`：窗口工厂
- `DesktopWindow` / `DesktopWindowManager`：Silk.NET.Windowing 实现
- 桌面窗口使用 `GraphicsAPI.None`，避免 Silk 默认图形上下文占用原生窗口并与 WebGPU 交换链冲突
- `WebGPUContext`：持有 api/instance/adapter/device/queue，`CreateSurface` 创建 `RenderSurface`
- 原生窗口销毁经渲染线程握手队列（`RenderTargetRegistry._pendingNativeDisposals`），由逻辑线程下一帧
  调用 `DisposeNative()`，避免跨线程销毁 Silk/GLFW 原生窗口导致关闭失效

### 3. 交换链封装（RenderSurface）

- `RenderSurface`：持有原生 `Surface*`，**裸指针不外泄**；懒重配（尺寸/PresentMode/lost）；
  `AcquireNextTexture`/`Present`/`Resize`/`SetPresentMode`/`EnsureConfigured`
- `FrameTexture`：acquire 结果的 RAII 包装（纹理 + 默认视图）
- 尺寸一律用物理像素 `FramebufferSize`（为 0 时回退到 `Size`，修 HiDPI 时序问题）
- 首次配置及后续 resize/lost 均由渲染线程在 acquire 前懒重配
- 跨线程尺寸用 `volatile` 字段（逻辑线程写、渲染线程读）

### 4. 渲染目标体系（RenderTarget）

- `RenderTarget`（抽象）：`Id`/尺寸/宽高比/`Format`/`BeginRenderSession`
- `Viewport`（窗口实现）：窗口 + 表面 + 尺寸，**不持有相机**（相机归属由组件决定）
- `TextureRenderTarget`（离屏实现）：用于阴影贴图、深度缓冲、UIRenderView 离屏渲染
- `RenderTargetSession`（RAII）：`Dispose` 时释放视图并 present
- `RenderTargetRegistry`：跨线程注册表（逻辑线程注册/渲染线程查询，`ConcurrentDictionary`）
- 离屏目标创建经渲染线程请求队列封送（`ProcessRenderViewCreations`），避免逻辑线程直接调 WebGPU device

### 5. 场景快照（SceneSnapshot）

- `SceneSnapshot`：`DeltaTime`/`FrameIndex`/`Cameras`/`Objects` + 分类 payload 缓冲——**值快照 +
  资源 ID**，绝不携带 GPU 指针或跨线程对象引用
- `SceneObjectHeader`：`ProxyId` + `Category` + `WorldTransform` + `Bounds` + `Visibility` +
  `PayloadIndex`（统一剔除面）
- 分类 payload（`StaticMeshPayload`/`SkeletalMeshPayload`/`LightPayload`）与缓冲字段由
  **SceneGen 源生成器**产出；`AddObject<T>` 辅助把「写 header + 写 payload」收口成一行
- `CameraSnapshot`：`TargetId` + 视图/投影矩阵 + 清屏色（`CameraComponent.ClearColor`）
- `FrameBuffer<T>`：池化数组，每帧只归零计数复用，避免每帧 GC
- `FrameIndex` 由 `EngineApplication` 自持单调计数器（`++_frameIndex`），保证全局单调递增

### 6. 场景代理体系（Scene / SceneProxy，借鉴 UE FScene）

- `Scene`：逻辑侧场景注册表，`Register`/`Unregister`/`Capture`，分配稳定 `ProxyId`
- `SceneProxy`（抽象）：`WorldTransform`/`Bounds`/`Visibility` + `Capture` 快照序列化
- **组件是唯一权威**：`[SceneProxy(类别)]` 标记组件（快照字段名由生成器从类别推导），`[ScenePayload]`
  标记进 payload 的字段/属性（默认值只在此处）
- **SceneGen 源生成器**产出：proxy 子类、payload struct、组件的 partial（`_proxy` + 生命周期 +
  `SyncProxy`）、`SceneSnapshot` 的 payload 字段与 `ClearPayloads`
- 语义钩子 `OnProxyMapped`：组件里手写每类专属的 Bounds 规则（生成器声明、用户实现）
- **资源成员降级**：`[ScenePayload]` 成员若实现 `ISceneResource`（`int ResourceId`），生成器自动降级为
  `{Name}Id` 进 payload，并自动触发上传
- **`ResourceManager`**：按 `MeshId`/`MaterialId` 去重的自动上传 + GPU 延迟释放（挂 `Scene.ResourceManager`）；组件首次引用资源即上传
- 组件经生成的 `BeginPlay`/`EndPlay` 注册/注销，`Update` 同步；`Actor` 转发组件生命周期
- 代理状态机完整保护：`_registeredScene` 缓存注册场景、BeginPlay 防重入、`AddOwnedComponent` 在 actor 已 BeginPlay 后补调组件 BeginPlay

### 7. 双缓冲帧同步（DualFrameBuffer）

- 单生产者/单消费者双缓冲，逻辑线程最多超前渲染线程 1 帧
- Present 回压闭环隐式成立：present 慢 → acquire 阻塞 → 逻辑线程降速
- 异常安全：`Abandon()` 归还空槽不提交帧；渲染线程 try/finally 无条件归还

### 8. 多线程

- `RenderThread`：线程外壳（循环 + 异常兜底 + 释放），`Render` + `ReturnEmpty` 包进 try/finally
- `IRenderPipeline` / `BlinnPhongRenderer`：管线抽象与具体实现；上传处理 → 生命周期 diff → BuildGraph → Compile → Execute → 延迟删除
- `EngineSynchronizationContext`：`Post`/`Send` 把异步回调封送到主引擎线程
- 主循环异常安全：取缓冲后包 try/catch，异常路径 `Abandon()` + 重抛

### 9. 场景系统（World → Actor → Component）

- `World`：`AddActor`/`RemoveActor`（延迟增删，同帧 Add+Remove 正确处理）、`Update`（对副本迭代防重入崩溃）、
  `CollectCameras`、`Scene` 注册表；异常路径 try/finally 保证列表一致
- `WorldContext`：`CurrentWorld` 与独立 `RuntimeWorld` 并存，`ActiveWorld` 优先驱动运行时 World
- `Actor`：`BeginPlay`/`Update`/`EndPlay` 生命周期、`Components`/`GetComponent<T>`/世界归属，转发组件生命周期
- `ActorComponent`：`BeginPlay`/`Update`/`EndPlay`（对应 UE 的 TickComponent）；带 `[SceneProxy]` 的
  组件的这些生命周期由 SceneGen 生成的 partial 实现，组件只写 `[ScenePayload]` 字段与 `OnProxyMapped`
- `SceneComponent`：相对位置/旋转/缩放（可读写）、`WorldTransform`
- `CameraComponent`：`RenderTarget`（可写）、`Viewport` 便捷属性、FOV/Near/Far/`ClearColor`、
  `GetViewMatrix`/`GetProjectionMatrix`

### 10. 静态网格渲染 + 渲染线程剔除

- `StaticMesh`：CPU 顶点（位置+颜色+UV+法线）/索引 + 本地包围球 `Bounds` + 全局 `MeshId`
- `StaticMeshComponent`：持有网格 + 材质 + `StaticMeshSceneProxy`，每帧同步世界变换与包围球
- `MeshGPUResource`：顶点/索引缓冲（**几何，按 MeshId 上传一次**）
- `StaticMeshRenderState`：每实例 object uniform（world + 法线矩阵）+ bind group（按 `ProxyId` 生命周期管理）
- `BlinnPhongRenderer` 视锥剔除：`Frustum`（Gribb-Hartmann 提取）+ `BoundingSphere`，逐相机剔除 → 按类别分流
- WGSL 由 `MaterialShaderCodegen` 按材质 key 生成，`MaterialShaderCache` 缓存 ShaderModule/RenderPipeline
- draw：每相机一个 render pass（首个 clear、后续 Load 叠加），`clip = viewProj × world × position`

### 11. 骨骼网格渲染

- `SkeletalMesh`：CPU 顶点（含骨骼索引/权重）+ 骨骼层级 + 绑定姿态逆矩阵
- `SkeletalMeshComponent`：持有骨骼网格 + 材质 + 动画姿态（`BoneMatrices`），每帧同步变换 + 包围球 + 骨骼矩阵
- `SkeletalMeshRenderState`：动态骨骼矩阵 buffer（每对象独立）+ bind group
- `SkeletalMeshStage`：与 `BlinnPhongStage` 共享深度附件（同 target 同份深度缓冲），类别间正确遮挡

### 12. 光源数据通路

- `LightComponent`（抽象基类，`[SceneProxy]`）→ `PointLightComponent`/`DirectionalLightComponent`/
  `SpotLightComponent` 构造时固定 `Type` → 生成统一的 `LightSceneProxy`/`LightPayload` → `Scene` →
  `SceneSnapshot.Lights`（+ header 包围球）→ `BlinnPhongRenderer` 剔除/收集
- 光源与网格走同一套快照通道；每帧把可见光打包进 group0 帧 uniform（`MAX_LIGHTS` 上限）
- 光照着色（Blinn-Phong，点光/平行光/聚光 + 衰减）在片元着色器 `shade_lit` 里一次完成（前向着色）

### 13. 引擎应用与演示

- `EngineApplication`：主循环（窗口事件 → 同步上下文 → 世界更新 → 填 `SceneSnapshot` → 提交）、
  `InitializeCallback`（初始化回调）、`ResourceManager`（资源自动上传 + GPU 延迟释放）、`ExitGame`；
  窗口在 `Run` 时创建；`CreateRenderView`/`DestroyRenderView` 离屏目标便捷方法
- `Demo`（类库，平台无关）：演示内容——`DemoApp.Initialize` 搭建 World / 相机 / 网格 / 材质 / 光源；
  UI 验收场景（`VerifyHub` + 4 个 Overlay）；`Demo.Desktop` 作为桌面入口只做引导 + 平台选择

### 14. 材质系统 + 光照着色（P0~P3）

- `Material`（静态属性 + 默认参数）与 `MaterialInstance : Material`（参数覆写）——实例不产生新 shader（ADR-13）
- `MaterialShaderKey`（值类型）折叠静态属性（着色模型/混合/双面/纹理开关）→ 编译缓存，跨资产共享（ADR-14）
- `MaterialShaderCodegen` 按 key 生成 WGSL（模板为嵌入式资源 `Render/Pipeline/BlinnPhong/Shaders/Forward*.wgsl`）；
  `MaterialShaderCache` 缓存 ShaderModule + RenderPipeline（按 target format）
- 绑定组四层：group0 帧 / group1 对象 / group2 材质参数 / group3 材质纹理（5 槽恒绑定 + fallback 纹理，ADR-15/16）
- 光照：`shade_lit`（Blinn-Phong，点光/平行光/聚光）+ 自发光 + MetallicRoughness/Mask 纹理
- 法线贴图：`TextureFlags.Normal` 开关 + 屏幕空间导数法 TBN（无需切线顶点属性）采样法线纹理，`NormalStrength`
  控制强度
- `StaticMeshComponent.Material` 走 `[ScenePayload]` 资源降级（`MaterialId`）+ `ResourceManager` 自动上传（ADR-19）

### 15. 管线抽象（IRenderPipeline）

- `IRenderPipeline`：可替换的「消费 `SceneSnapshot` → 提交绘制」契约（`Render` + `IDisposable`）
- `BlinnPhongRenderer : IRenderPipeline`：当前唯一实现（Blinn-Phong 前向渲染）；`RenderThread` 只依赖接口（DI 注入）
- 换管线 = 换 DI 注册：`UseBlinnPhong()` ↔ 未来的 `UseDeferred()`，渲染线程/场景同步零改动（ADR-21）
- 多 pass shader：`ShaderPass`（Forward/ShadowDepth/DepthOnly）+ 缓存键 `(MaterialShaderKey, ShaderPass)`，
  同一材质按 pass 编多份 shader；阴影贴图已落地（ShadowDepth pass → 前向采样，ADR-22，见 [ShadowMapping-Design.md](./ShadowMapping-Design.md)）
- 多 stage 架构：`IRenderStage`（`AddToGraph` + `IDisposable`），`ShadowDepthStage` / `BlinnPhongStage` / `SkeletalMeshStage`

### 16. 帧图（RenderGraph）

- `RenderGraph`（声明式依赖图）：`RegisterTexture`（transient）/ `ImportTexture`（external）/ `AddPass`
  （声明读写 + 执行回调）/ `Compile`（建三类依赖边 + 拓扑排序 + 环检测 + 简化剔除）/ `Execute`
  （分配 transient → 按拓扑序执行 pass → 帧末释放 transient）
- `RenderGraphContext`：pass 执行时把句柄解析成真实 GPU 对象（`GetRenderTarget` / `GetTextureView` / `GetTransientTarget`）
- `TransientResourcePool`：按 `TextureResourceDesc` 跨帧复用 transient 纹理，帧末归还空闲池，管线销毁时统一释放；内存别名复用留待后续阶段
- 三 stage 已落地：`ShadowDepthStage`（写 transient 深度贴图）+ `BlinnPhongStage`（静态网格）+ `SkeletalMeshStage`
  （骨骼网格），经 RenderGraph 编排执行
- 窗口 backbuffer 作为 external 资源接入：`RenderGraph.Execute` 帧级 acquire/present 各一次（多相机/多 pass
  共享同一帧），pass 经 `GetTextureView` 取 acquire 的视图
- 可视化：`RenderGraph.Dump()` 编译后导出纯数据快照，`RenderGraphVisualizer` 输出 Mermaid / DOT / JSON
- 图形化配置基础（独立可选模块）：`RenderPassType`/`RenderPassTypeRegistry` + 可序列化 `RenderGraphDefinition` +
  `RenderGraphAssembler`；运行时 `BlinnPhongRenderer` 仍命令式建图，未来编辑器以配置层为入口
- CommandBuffer/Encoder 每帧正确释放（4 处 `QueueSubmit` 后补 `Release`）
- 被剔除 pass 的 transient 资源不再分配
- 阴影→无阴影切换时旧 bind group 正确释放
- 详见 [RenderGraph-Design.md](./RenderGraph-Design.md)

### 17. 阴影贴图

- 单阴影贴图：每帧找第一个 `CastShadow` 的聚光/平行光，渲一张 1024×1024 深度贴图，前向 pass 采样
- 深度比较方向已验证（wgpu `Compare=Less` 语义下 `depth_ref < sampled_depth`，bias 取负）
- 详见 [ShadowMapping-Design.md](./ShadowMapping-Design.md)

### 18. UI 系统（保留模式控件树 + overlay 渲染）

- 与 3D 场景解耦的**并行子系统**：不进 `SceneProxy`/`SceneCategory` 通道，经 `IGraphOverlay` 挂到
  RenderGraph，在场景 pass 之后、写入同一 backbuffer 的最后一次绘制（共享帧级 acquire/present）
- **逻辑线程**：`UIManager`（基元收口 + 每窗口 `UICanvas` + 纹理上传队列）→ 每帧 `canvas.Update(input)`
  （Arrange 布局 + 命中测试/事件路由）→ `canvas.Paint(ui)` 产出屏幕空间 `UIPrimitive` → 拷贝进
  `SceneSnapshot.UIPrimitives`（值快照）
- **控件树**（保留模式，对齐 Slate/WPF/UGUI）：
  - 布局容器：`UIStackPanel`（垂直/水平）、`UIGridPanel`（行列定义 + RowSpan/ColumnSpan + Auto/Star/Pixel 轨）、
    `UIDockPanel`（Top/Bottom/Left/Right/Fill）、`UIWrapPanel`（自动换行）
  - 叶控件：`UIPanel`、`UILabel`、`UIButton`、`UITextBox`（v1 单行）、`UICheckbox`、`UISlider`、`UIProgressBar`
  - 树操作：`AddChild`（重挂自动摘除旧父 + 环检测）、`RemoveChild`、`ClearChildren`
  - 两阶段布局：`Measure`（内容自适应）→ `Arrange`（分配空间）；`FixedSize ≤ 0` = 拉伸填充
  - 裁剪：scissor 裁剪 + `ClipToBounds`（含 HitTest 受裁剪约束）；裁剪栈按 targetId 隔离
  - 焦点：Tab 导航 + 焦点环可视化
- **输入系统**（平台无关，`Input/`）：`Key`/`MouseButton` 引擎枚举 + 位掩码，`WindowInput` → `InputManager`
  → 每帧 `InputState`（down/pressed/released 三态 + 文本）；Silk 枚举在 Desktop 层映射
- **文本渲染**（字符串级 v1）：`TextRenderer` 用 SixLabors 把整段文本栅格化为白字透明底 RGBA8，按字符串
  缓存纹理；全墨水包围盒（含 descender/ascender/斜体悬突 + 四向 1px 抗锯齿余量）
- **渲染线程**：`UIRenderer`（多纹理按 `TextureId` 分批、动态顶点/索引缓冲、白纹理 + 纹理注册表、
  256 对齐纹理上传）经 `UseUI()` 注册为 `IGraphOverlay`
- 渲染视图 bind group 缓存随目标重建自动失效清理
- UI scissor 完全越界时正确跳过该批
- 详见 [UI-System-Design.md](./UI-System-Design.md)

### 19. 渲染视图控件（UIRenderView，引擎画面显示）

- `UIRenderView`：把离屏 `TextureRenderTarget` 的内容实时显示到 UI 画布（编辑器视口/小地图/分屏预览）
- **跨线程 ID 引用**：逻辑线程只发 `TextureId = -renderViewId` 基元；渲染线程 `UIRenderer.GetBindGroup`
  从 `RenderTargetRegistry` 解析真实纹理视图并建 bind group（缓存 + 失效清理）
- **采样依赖**：UI pass 对实际引用的渲染视图声明 `Read(Sample)`，保证在写该离屏目标的场景 pass 之后执行
- **自适应分辨率**：`AutoResize`（默认开）随显示区域动态重建离屏目标，`ResolutionScale` 超采样、
  `ResizeThreshold` 防抖，消除放大模糊；重建走「新建 + ADR-7 延迟销毁旧目标」，当帧生效
- `EngineApplication.CreateRenderView`/`DestroyRenderView`：离屏目标创建/注册/延迟销毁便捷方法
- 详见 [UIRenderView-Design.md](./UIRenderView-Design.md)

### 验证状态

| 能力 | 编译 | 运行 |
|---|---|---|
| 渲染管线骨架（清屏） | ✅ | ✅ |
| World 场景接入 | ✅ | ✅ |
| StaticMesh 三角形渲染 | ✅ | ✅ |
| Scene/SceneProxy 统一同步 + 视锥剔除 + 光源数据通路 | ✅ | ✅ |
| 材质系统（Material/MaterialInstance + shader 编译缓存） | ✅ | ✅ |
| 前向光照着色（Blinn-Phong） | ✅ | ✅ |
| 阴影贴图（ShadowDepth pass + 前向采样） | ✅ | ✅ |
| RenderGraph 声明式多 pass 编排（ShadowDepth → BlinnPhong → SkeletalMesh） | ✅ | ✅ |
| 骨骼网格渲染（SkeletalMeshStage + 共享深度附件） | ✅ | ✅ |
| 法线贴图（Normal Mapping，导数法 TBN） | ✅ | ✅ |
| UI 系统（控件树 + 输入 + 文本 + overlay 渲染 + 交互） | ✅ | ✅ |
| 渲染视图控件（UIRenderView：离屏渲染 → UI 采样 + 自适应分辨率） | ✅ | ✅ |

> 注：UI P6-fix / P8 控件 / P8 审计修复三轮均已在本机 GPU 实机运行验收场景逐项目确认，
> 用户验收通过（2026-08-31）；当前自动化测试共 105 个。

### 20. 编辑器控件集（P8，2026-08-26）

- **滚动容器** `UIScrollBox`：垂直/水平/双向滚动 + 滚轮（`OnMouseWheel` 冒泡）+ 滚动条拖拽 + `ScrollIntoView`
- **列表** `UIListView`/`UIListItem`：垂直列表 + 单选 + 键盘导航（Up/Down/Home/End/Enter）+ 选择回调
- **树** `UITreeView`/`UITreeViewItem`：层级树——`SubItems` 逻辑子项与扁平化可视列表分离（TreeView 重挂不破坏树结构）、
  展开/折叠、单选、键盘导航（含左右键折叠/展开）、`ExpandAll`/`CollapseAll`
- **菜单** `UIMenuBar`/`UIMenuBarItem`/`UIMenuPanel`/`UIMenuItem`：菜单栏 + 弹出菜单（Overlay 注册、
  按 `Position` 定位、分隔线/快捷键显示、选中后自动关闭）
- **对话框** `UIDialog`：模态遮罩（Overlay 铺满画布拦截鼠标）+ 居中面板 + 按钮（默认/取消）+ Escape/Enter
- **标签页** `UITabView`/`UITabItem`：动态标签宽度 + 关闭按钮（`CanClose`）+ 内容切换
- **下拉框** `UIComboBox`：点击展开/收起 + 键盘导航 + 选中回调 + Overlay 绘制层
- **分割面板** `UISplitPanel`：水平/垂直分割 + 拖拽调整比例 + 最小尺寸约束
- **工具栏** `UIToolbar`/`UIToolbarButton`：水平按钮组 + 分隔符
- **属性网格** `UIPropertyGrid`：反射对象属性生成标签 + 值编辑行（int/float/bool/string）+ `PropertyChanged`
- **Overlay 弹出层**：`UICanvas.Overlays`——菜单/对话框覆盖在兄弟元素之上、不参与布局流；
  绘制在 Root 之后、命中测试优先；每帧注入 TextRenderer/Canvas
- 单元测试：`EditorControlTests`（覆盖滚动钳位/列表选择/树层级/标签页/分割/下拉/属性网格/
  布局稳定性/文本高度/滚动裁剪等）
- 验收入口：`Demo/Demo/EditorControlsVerifyOverlay.cs`（VerifyHub 第 5 个按钮，9 个子场景），**已通过用户逐场景验收（2026-08-31）**

### 21. UI 布局审计修复轮（P8-audit，2026-08-31）

- **滚动裁剪修复**：scissor「空交集」与「无裁剪」语义混淆——`UIManager.Intersect` 空交集改返回负尺寸
  （完全裁剪标记），`UIRenderer.DrawBatch` 检测负尺寸跳过；滚动内容不再越过视口可见
- **文本高度稳定**：`MeasureBlock` 高度固定为 `行数 × LineHeight`（不随字符波动），
  状态文字变化不再引起布局位移；同时不裁剪墨水（端到端验证）
- **容器基准一致**：Measure 约束减自身 Padding、FixedSize 早退先测子元素、复合控件补内部面板布局
- **交互修复**：SplitPanel 拖拽（新增 `OnMouseMove` 悬停通知）、Dialog 关闭（`OnMouseClick` + Focusable）、
  Toolbar 按钮文本自适应宽度、树 `LogicalParent` 导航
- **精确文本截断**：`TextRenderer.Truncate` 逐字符测量替代字符数比例
- 详见 `Doc/tasks/2026-08-31-editor-controls-audit-fixes-worklog.md` 与
  `Doc/UI-System-Design.md`「踩坑经验」11~22

---

## 二、待实现（按优先级分组）

### P0 —— 当前阶段：编辑器 MVP 落地

**目标**：控件基础已具备，下一步把 `Spark.Engine.Editor` 推进为可持续工作的场景编辑器。
当前 UI 控件覆盖率约 70%（编辑器刚需控件已落地）。

#### 已落地（2026-08-26，见「一、已实现 §20」）

| 控件 | 状态 |
|------|------|
| `UIScrollBox` | ✅ 滚动容器 + 滚轮 + 滚动条拖拽 |
| `UIListView` / `UIListItem` | ✅ 列表 + 单选 + 键盘导航 |
| `UITreeView` / `UITreeViewItem` | ✅ 层级树 + 展开/折叠 + 键盘导航 |
| `UIMenuBar` / `UIMenuPanel` / `UIMenuItem` | ✅ 菜单栏 + 弹出菜单（Overlay） |
| `UIDialog` / `UIDialogButton` | ✅ 模态对话框（Overlay 遮罩） |
| `UITabView` / `UITabItem` | ✅ 标签页 + 关闭按钮 |
| `UIComboBox` | ✅ 下拉选择 + Overlay 绘制层 |
| `UISplitPanel` | ✅ 可拖拽分割面板 |
| `UIPropertyGrid` | ✅ 属性网格（反射 + 编辑） |
| `UIToolbar` / `UIToolbarButton` | ✅ 工具栏 |
| Overlay 弹出层机制 | ✅ `UICanvas.Overlays` |
| 滚轮事件路由 | ✅ `OnMouseWheel` 冒泡 |

#### 配套能力（待补）

- 样式系统初版（颜色/字体/间距可配置，替代硬编码 `UITheme`）
- 键盘焦点增强（Tab 导航完善、焦点环改进）
- 缺控件：Image / RadioButton / Spinner / Tooltip / Window

#### 编辑器 MVP 落地（下一步）

- 场景层级面板（TreeView 展示 Actor 树）
- 属性面板（选中 Actor/Component 后显示属性）
- 渲染视口（UIRenderView 嵌入编辑器面板）
- 菜单栏 + 工具栏（新建/打开/保存场景）
- 资产浏览器（浏览/拖拽 StaticMesh/Material 到场景）
- UE 风格 SceneComponent 层级、RootComponent、Socket 和挂载规则
- EditorWorld / RuntimeWorld 隔离，Play/Stop 不污染编辑场景
- 自定义 `.scene` / `.asset` 场景资产与 Windows `.pak` Cook
- glTF StaticMesh 导入（保留节点层级）

### P1 —— 性能与稳定性

1. **UE 场景层级**：RootComponent、AttachParent/AttachChildren、Socket、挂载规则和层级 dirty 传播已完成基础运行时实现；编辑器拖拽和资产 Socket 待补
2. **场景持久化与编辑器运行隔离**：SceneDocument/`.scene`、独立 RuntimeWorld 实例化、EditorContext Play/Stop 和 EngineApplication 双 World 调度已完成
3. **dirty 标记 + 增量更新**：`SceneComponent` 变换 setter 标记 dirty，只重算/提交变化的对象，静态对象复用上一帧快照（当前每帧全量快照）
4. ~~UI 三轮验收~~：✅ 已完成（2026-08-31 用户 GPU 实机逐场景确认通过）
5. **单元测试补齐**：当前自动化测试共 105 个；`BoundingSphere`/`Frustum`/`SceneSnapshot` 及 GPU 集成测试仍需补齐

### P2 —— 渲染能力扩展

4. **PBR 着色器**：Cook-Torrance / GGX BRDF，metallic/roughness 通道已就绪（ADR-20）
5. **后处理链**：相机 A → 贴图 → 相机 B 采样，接入 RenderGraph
6. **半透明排序**：`BlendMode.Translucent` 按深度排序 + 与不透明分两批
7. **剔除加速结构**：BVH / 八叉树 / 遮挡剔除
8. **`MAX_LIGHTS` 超限策略**：按强度排序截断或逐光源 pass

### P3 —— 引擎完善

9. **资产管线完善**：`.gltf` StaticMesh 导入和 Windows Cook 包写入已完成；Asset Registry、GLB/材质纹理导入、运行时 `.pak` 加载待补，后续再做异步/增量 Cook
10. **`ViewportRect` 分屏 / 编辑器多视图**：一个 surface 渲染多个子视口
11. **PresentMode 可配置**：VSync 开关由 `EngineOptions` 暴露
12. **surface lost 完整恢复**：当前跳帧+重配，需更完整策略
13. **材质节点图编辑器**：P4（`MaterialExpression` 节点图 + 任意节点 codegen）
14. **shader 磁盘缓存**：进程内缓存重启即失效，磁盘 hash → WGSL/pipeline

### P4 —— UI 打磨

15. **文本框进阶（P7）**：选择/复制粘贴/词删除/Undo/剪贴板/IME/多行/掩码
16. **更多控件（P8）**：Image/RadioButton/Spinner/Tooltip/Window（ProgressBar 已完成）
17. **渲染质量（P9）**：字形图集 + 嵌入字体（中文支持）、圆角/边框/阴影/渐变/九宫格/DPI 缩放/脏标记增量绘制
18. **样式系统 + 数据绑定（P10）**：可定制主题、MVVM 绑定

### P5 —— RenderGraph 深化

19. **别名复用（Phase C）**：存活区间不重叠的 transient 纹理共用物理内存
20. **barrier 与 pass 级剔除（Phase D）**：完整 `FirstWrite/LastRead` 覆盖
21. **图编译缓存**：跨帧缓存拓扑与资源池
22. **编辑器 UI**：读 `RenderPassTypeRegistry` 生成节点面板 → 拖线产 `RenderGraphDefinition` → JSON 持久化

---

## 设计原则

> 详见各子文档

- **P1 裸指针不出核心库**：`Surface*` 等只存在于 `RenderSurface` 内部
- **P2 资源线程归属唯一**：GPU 资源归渲染线程，逻辑线程经资源 ID + 注册表间接引用
- **P3 帧数据一致性**：双缓冲 + 值快照，渲染线程读到的永远是一帧完整一致的数据
- **P4 懒重配**：surface 尺寸/PresentMode/lost 变化在 acquire 前检查并重配
- **P5 所有权单向**：平台层创建/销毁 `RenderSurface`，渲染系统只引用
- **P6 渲染目标统一**：相机输出不限于窗口，`RenderTarget` 抽象统一窗口与贴图
- **P7 一条通道，分类 payload**：所有场景对象共用 `SceneProxy → SceneSnapshot` 单通道，差异只在 payload 结构与消费者
- **P8 静态上传一次，动态每帧快照**：几何/纹理上传一次；变换/包围盒/光源参数/骨骼姿态每帧快照
- **P9 稳定 ID + 生命周期 diff**：`ProxyId` + 集合比对得出新增/存活/销毁
- **P10 剔除归渲染线程**：逻辑线程提交完整对象集 + bounds，渲染线程按相机剔除
- **P11 语义手写、样板生成**：component 是唯一权威；proxy/payload/快照登记点等样板由 SceneGen 源生成器产出
- **P17 管线可替换**：`IRenderPipeline` 抽象 + DI 注册切换，渲染线程与场景同步只依赖接口

> 材质系统原则 P12~P16 见 [MaterialSystem-Design.md](./MaterialSystem-Design.md#2-设计原则在-p1p11-上新增)。
> RenderGraph 原则 P18~P21 见 [RenderGraph-Design.md](./RenderGraph-Design.md#2-设计原则在-p1p17-上新增)。

## 决策记录（ADR）

| ID | 决策 |
|---|---|
| ADR-1 | `SceneSnapshot` 值快照 + 资源 ID，帧由相机驱动 |
| ADR-2 | Surface resize 每帧懒重配 |
| ADR-3 | 裸指针封装为 `RenderSurface` |
| ADR-4 | 尺寸用物理像素 `FramebufferSize` |
| ADR-5 | `RenderSurface` 由平台层创建/销毁 |
| ADR-6 | 渲染目标统一 `RenderTarget` 抽象 |
| ADR-7 | 资源销毁走延迟删除队列（场景代理已落地，视口销毁待接入） |
| ADR-8 | 所有场景对象共用 `SceneProxy → SceneSnapshot` 单通道 |
| ADR-9 | 静态数据 upload-once 资源注册表，动态数据每帧值快照 |
| ADR-10 | 场景对象用稳定 `ProxyId` + 集合 diff 表达新增/存活/销毁 |
| ADR-11 | 剔除归渲染线程：逻辑提交完整对象集 + bounds，渲染线程按相机剔除 |
| ADR-12 | 传输样板由 SceneGen 源生成器产出，语义手写 |
| ADR-13 | `Material` 与 `MaterialInstance` 分离，实例不产生新 shader |
| ADR-14 | shader 变体用值类型 `MaterialShaderKey` 折叠 + 进程内编译缓存 |
| ADR-15 | 纹理槽恒绑定（5 槽 + fallback），`TextureFlags` 只改生成代码 |
| ADR-16 | 绑定组按更新频率分四层，布局全局唯一 |
| ADR-17 | 未指定材质回退引擎内置 DefaultMaterial |
| ADR-18 | P0~P3 用固定参数集 + WGSL 模板 codegen，节点图后置（P4） |
| ADR-19 | `MaterialInstance : Material`，组件统一类型 `Material?` |
| ADR-20 | 先 `Lit`(Blinn-Phong)，`PBR` 作为顺延扩展 |
| ADR-21 | 管线抽象 `IRenderPipeline` + DI 注册切换 |
| ADR-22 | 多 pass 用 `ShaderPass` 枚举，缓存键 `(MaterialShaderKey, ShaderPass)` |
| ADR-27 | SceneComponent 采用 UE 式 Root/AttachParent/AttachChildren 树，支持 Socket 和三维独立挂载规则 |
| ADR-28 | 编辑器 World 与 Runtime World 隔离；运行时从 SceneDocument 实例化，不共享可变 Actor/Component |
| ADR-29 | 持久资产使用 Guid；运行时 ResourceId 与渲染 ProxyId 独立分配 |
| ADR-30 | 自定义 `.scene` / `.asset` / `.pak` 格式；首版 Windows Cook，Cook 接口保留跨平台扩展点 |
| ADR-31 | glTF 首版只导入 StaticMesh，默认保留节点层级 |

## 构建与运行

```bash
# 构建
dotnet build Spark.Engine.slnx

# 运行演示（需本地 GPU 环境）
dotnet run --project Demo/Demo.Desktop
```

> 注意：WebGPU 依赖原生 wgpu（Silk.NET.WebGPU.Native.WGPU），需硬件 GPU 环境；
> 软件渲染器/远程桌面下可能报 "Invalid surface"。
> 当前依赖 .NET 11 preview SDK；`Microsoft.Extensions.*` 固定 `11.0.0-preview.6`。

## 关联文档

- [RenderPipeline-Design.md](./RenderPipeline-Design.md) — 渲染管线详设（含类图、UE 对比）
- [SceneSync-Design.md](./SceneSync-Design.md) — 逻辑/渲染线程场景同步机制
- [MaterialSystem-Design.md](./MaterialSystem-Design.md) — 材质系统设计
- [RenderGraph-Design.md](./RenderGraph-Design.md) — 帧图（RenderGraph）设计
- [ShadowMapping-Design.md](./ShadowMapping-Design.md) — 阴影贴图设计
- [UI-System-Design.md](./UI-System-Design.md) — UI 系统设计
- [UIRenderView-Design.md](./UIRenderView-Design.md) — 渲染视图控件设计
- [SceneHierarchy-Design.md](./SceneHierarchy-Design.md) — UE 风格场景层级、Socket 与挂载规则
- [AssetPipeline-Design.md](./AssetPipeline-Design.md) — 自定义资产格式、glTF 导入与 Cook
