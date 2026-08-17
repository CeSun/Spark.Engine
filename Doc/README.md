# Spark.Engine 项目设计与现状

> 状态：持续演进中
> 本文是项目级设计总览，明确区分「已实现」与「设计（未实现）」。
> 渲染管线详设见 [RenderPipeline-Design.md](./RenderPipeline-Design.md)；逻辑/渲染线程场景同步机制见
> [SceneSync-Design.md](./SceneSync-Design.md)。

## 概述

Spark.Engine 是一个用 C# 从零实现的跨平台游戏引擎，渲染后端基于 **WebGPU**（Silk.NET 绑定，
Native 实现为 wgpu），窗口基于 Silk.NET.Windowing，基础设施基于 Microsoft.Extensions.DependencyInjection
与 Serilog。场景对象模型借鉴 Unreal Engine（World → Actor → Component），逻辑/渲染线程同步借鉴 UE 的
FScene / FSceneProxy / FSceneRenderer 模式。

当前处于**早期原型阶段**：渲染管线已能绘制静态网格（三角形），场景对象（网格/光源）经统一快照通道
传入渲染线程并做视锥剔除；场景代理由 SceneGen 源生成器生成；材质系统（Material/MaterialInstance、
shader 编译缓存）、前向光照着色（Blinn-Phong）、帧图（RenderGraph）、法线贴图与阴影贴图已落地，
节点图编辑器与 PBR 尚未实现。

## 解决方案结构

```
Spark.Engine.slnx
├─ Src/
│  ├─ Spark.Engine/          核心引擎库（net10.0，唯一 WebGPU 依赖点）
│  │  ├─ Math/               包围球/视锥（渲染线程剔除）
│  │  ├─ Resources/          CPU 侧资源资产：StaticMesh/Texture2D/Material + ResourceManager
│  │  ├─ Render/             场景同步（Scene/SceneProxy/SceneSnapshot）
│  │  │  ├─ Common/          通用渲染类型：RenderTarget/RenderSurface/RenderTargetRegistry/Viewport/TextureRenderTarget/FrameTexture
│  │  │  ├─ Resources/       GPU 资源（IGPUResource + Mesh/Texture/Material GPU 表示）
│  │  │  ├─ Pipeline/        管线抽象（IRenderPipeline/ShaderPass）+ BlinnPhong/
│  │  │  │  └─ BlinnPhong/   Blinn-Phong 管线（BlinnPhongRenderer + shader + Passes/）
│  │  │  └─ RenderGraph/     帧图（RenderGraph + 资源句柄/池）
│  │  ├─ Components/         组件（含 LightComponent 基类 + Point/Directional/Spot 光源、StaticMeshComponent）
│  │  ├─ Threads/            RenderThread（外壳）/EngineSynchronizationContext
│  │  └─ Worlds/             World（含 Scene）/WorldContext
│  ├─ Spark.Engine.SceneGen/ 源生成器（按 [SceneProxy]/[ScenePayload] 生成 proxy/payload）
│  ├─ Spark.Engine.Desktop/  桌面平台后端（net11.0，Silk.NET.Windowing）
│  └─ Spark.Engine.Editor/   编辑器（net11.0，空壳）
├─ Demo/
│  ├─ Demo/                  演示内容类库（场景搭建 + WallSwinger，平台无关）
│  └─ Demo.Desktop/          桌面入口（引导 + 平台启动，引用 Demo）
└─ Doc/                      设计文档
```

## 架构分层

```
Demo（入口：EngineBuilder → InitializeWebGPU → UseDesktop → UseBlinnPhong → Build → Run）
  │
  ▼
EngineApplication（主循环：窗口事件 → 同步上下文 → 世界更新 → 填场景快照 → 提交）
  ├─ WindowManager（窗口生命周期 + Viewport 创建）
  ├─ WorldContext → World（Actor 增删/更新 + Scene 场景注册表 + 相机收集）
  └─ DualFrameBuffer<SceneSnapshot>（双缓冲，逻辑→渲染）
       │
       ▼
RenderThread（线程外壳 → IRenderPipeline，DI 注入）
  └─ BlinnPhongRenderer : IRenderPipeline（上传处理 → 生命周期 diff → acquire → 剔除 → clear → draw → present → 延迟删除）
       ├─ RenderTargetRegistry（窗口视口注册表）
       ├─ IGPUResource 注册表（单注册表：几何/纹理/材质，上传一次）
       ├─ StaticMeshRenderState 注册表（每实例 object uniform，按 ProxyId）
       └─ MaterialShaderCache（MaterialShaderKey → ShaderModule + RenderPipeline，按 format 缓存）
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
- `EngineOptions`：`Width`/`Height`/`TargetFrameRate`

### 2. 平台抽象层

- `IWindow`：`Size`/`FramebufferSize`/`Title`/`IsClosing`/`Surface` + 生命周期方法
- `IWindowBackend`：窗口工厂
- `DesktopWindow` / `DesktopWindowManager`：Silk.NET.Windowing 实现
- 桌面窗口使用 `GraphicsAPI.None`，避免 Silk 默认图形上下文占用原生窗口并与 WebGPU 交换链冲突
- `WebGPUContext`：持有 api/instance/adapter/device/queue，`CreateSurface` 创建 `RenderSurface`

### 3. 交换链封装（RenderSurface）

- `RenderSurface`：持有原生 `Surface*`，**裸指针不外泄**；懒重配（尺寸/PresentMode/lost）；
  `AcquireNextTexture`/`Present`/`Resize`/`SetPresentMode`/`EnsureConfigured`
- `FrameTexture`：acquire 结果的 RAII 包装（纹理 + 默认视图）
- 尺寸一律用物理像素 `FramebufferSize`（为 0 时回退到 `Size`，修 HiDPI 时序问题）
- 首次配置及后续 resize/lost 均由渲染线程在 acquire 前懒重配

### 4. 渲染目标体系（RenderTarget）

- `RenderTarget`（抽象）：`Id`/尺寸/宽高比/`Format`/`BeginRenderSession`
- `Viewport`（窗口实现）：窗口 + 表面 + 尺寸，**不持有相机**（相机归属由组件决定）
- `RenderTargetSession`（RAII）：`Dispose` 时释放视图并 present
- `RenderTargetRegistry`：跨线程注册表（逻辑线程注册/渲染线程查询，`ConcurrentDictionary`）

### 5. 场景快照（SceneSnapshot，替代原 FrameData）

- `SceneSnapshot`：`DeltaTime`/`FrameIndex`/`Cameras`/`Objects` + 分类 payload 缓冲——**值快照 +
  资源 ID**，绝不携带 GPU 指针或跨线程对象引用
- `SceneObjectHeader`：`ProxyId` + `Category` + `WorldTransform` + `Bounds` + `Visibility` +
  `PayloadIndex`（统一剔除面）
- 分类 payload（`StaticMeshPayload`/`LightPayload`）与 `StaticMeshes`/`Lights` 缓冲字段由
  **SceneGen 源生成器**产出；`AddObject<T>` 辅助把「写 header + 写 payload」收口成一行
- `CameraSnapshot`：`TargetId` + 视图/投影矩阵 + 清屏色（清屏色由 `CameraComponent.ClearColor` 提供，
  不再硬编码）
- `FrameBuffer<T>`：池化数组，每帧只归零计数复用，避免每帧 GC

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
- **`ResourceManager`**：按 `MeshId` 去重的自动上传 + GPU 几何延迟释放（挂 `Scene.ResourceManager`）；组件首次引用资源即上传
- 组件经生成的 `BeginPlay`/`EndPlay` 注册/注销，`Update` 同步；`Actor` 转发组件生命周期

### 7. 双缓冲帧同步（DualFrameBuffer）

- 单生产者/单消费者双缓冲，逻辑线程最多超前渲染线程 1 帧
- Present 回压闭环隐式成立：present 慢 → acquire 阻塞 → 逻辑线程降速

### 8. 多线程

- `RenderThread`：线程外壳（循环 + 异常兜底 + 释放），只依赖 `IRenderPipeline`（DI 注入，换管线不改本类）
- `IRenderPipeline` / `BlinnPhongRenderer`：管线抽象与具体 Blinn-Phong 实现；上传处理 → 生命周期 diff（新增/存活/销毁）
  → 分组 → acquire → 剔除 → clear → draw → present → 延迟删除（ADR-7）
- `EngineSynchronizationContext`：`Post`/`Send` 把异步回调封送到主引擎线程

### 9. 场景系统（World → Actor → Component）

- `World`：`AddActor`/`RemoveActor`（延迟增删）、`Update`、`CollectCameras`、`Scene` 注册表
- `WorldContext`：`CurrentWorld` 可设置
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

### 11. 光源数据通路

- `LightComponent`（抽象基类，`[SceneProxy]`）→ `PointLightComponent`/`DirectionalLightComponent`/
  `SpotLightComponent` 构造时固定 `Type` → 生成统一的 `LightSceneProxy`/`LightPayload` → `Scene` →
  `SceneSnapshot.Lights`（+ header 包围球）→ `BlinnPhongRenderer` 剔除/收集
- 光源与网格走同一套快照通道；每帧把可见光打包进 group0 帧 uniform（`MAX_LIGHTS` 上限）
- 光照着色（Blinn-Phong，点光/平行光/聚光 + 衰减）在片元着色器 `shade_lit` 里一次完成（前向着色）

### 12. 引擎应用与演示

- `EngineApplication`：主循环（窗口事件 → 同步上下文 → 世界更新 → 填 `SceneSnapshot` → 提交）、
  `InitializeCallback`（初始化回调）、`ResourceManager`（资源自动上传 + GPU 延迟释放）、`ExitGame`；窗口在 `Run` 时创建
- `Demo`（类库，平台无关）：演示内容——`DemoApp.Initialize` 搭建 World / 相机 / 网格 / 材质 / 光源；`Demo.Desktop` 作为桌面入口只做引导 + 平台选择

### 13. 材质系统 + 光照着色（P0~P3）

- `Material`（静态属性 + 默认参数）与 `MaterialInstance : Material`（参数覆写）——实例不产生新 shader（ADR-13）
- `MaterialShaderKey`（值类型）折叠静态属性（着色模型/混合/双面/纹理开关）→ 编译缓存，跨资产共享（ADR-14）
- `MaterialShaderCodegen` 按 key 生成 WGSL（模板为嵌入式资源 `Render/Pipeline/BlinnPhong/Shaders/Forward*.wgsl`）；
  `MaterialShaderCache` 缓存 ShaderModule + RenderPipeline（按 target format）
- 绑定组四层：group0 帧 / group1 对象 / group2 材质参数 / group3 材质纹理（5 槽恒绑定 + fallback 纹理，ADR-15/16）
- 光照：`shade_lit`（Blinn-Phong，点光/平行光/聚光）+ 自发光 + MetallicRoughness/Mask 纹理
- 法线贴图：`TextureFlags.Normal` 开关 + 屏幕空间导数法 TBN（无需切线顶点属性）采样法线纹理，`NormalStrength`
  控制强度；PBR 待实现
- `StaticMeshComponent.Material` 走 `[ScenePayload]` 资源降级（`MaterialId`）+ `ResourceManager` 自动上传（ADR-19）

### 14. 管线抽象（IRenderPipeline）

- `IRenderPipeline`：可替换的「消费 `SceneSnapshot` → 提交绘制」契约（`Render` + `IDisposable`）
- `BlinnPhongRenderer : IRenderPipeline`：当前唯一实现（Blinn-Phong 前向渲染）；`RenderThread` 只依赖接口（DI 注入）
- 换管线 = 换 DI 注册：`UseBlinnPhong()` ↔ 未来的 `UseDeferred()`，渲染线程/场景同步零改动（ADR-21）
- 多 pass shader：`ShaderPass`（Forward/ShadowDepth/DepthOnly）+ 缓存键 `(MaterialShaderKey, ShaderPass)`，
  同一材质按 pass 编多份 shader；阴影贴图已落地（ShadowDepth pass → 前向采样，ADR-22，见 [ShadowMapping-Design.md](./ShadowMapping-Design.md)）

### 15. 帧图（RenderGraph）

- `RenderGraph`（声明式依赖图）：`RegisterTexture`（transient）/ `ImportTexture`（external）/ `AddPass`
  （声明读写 + 执行回调）/ `Compile`（建依赖边 + 拓扑排序 + 环检测 + 简化剔除）/ `Execute`
  （分配 transient → 按拓扑序执行 pass → 帧末释放 transient）
- `RenderGraphContext`：pass 执行时把句柄解析成真实 GPU 对象（`GetRenderTarget` / `GetTextureView` / `GetTransientTarget`）
- `TransientResourcePool`：帧内 transient 纹理分配/释放（Phase B：每帧新建、帧末统一释放，别名复用留待 Phase C）
- 两 pass 已落地：`ShadowDepthPass`（写 transient 深度贴图）+ `BlinnPhongPass`（采样阴影贴图 → 写 backbuffer），
  取代原 `BlinnPhongRenderer` 里手写的 `RenderShadowMap` / `DrawView` 命令式顺序
- 窗口 backbuffer 作为 external 资源经 `BeginRenderSession()`（acquire/present）接入，而非 `GetTextureView`
- 详见 [RenderGraph-Design.md](./RenderGraph-Design.md)（目标架构 + Phase B 实现落地与踩坑经验）

### 验证状态

| 能力 | 编译 | 运行 |
|---|---|---|
| 渲染管线骨架（清屏） | ✅ | ✅（本地 GPU 环境验证通过） |
| World 场景接入 | ✅ | ✅（本地 GPU 环境验证通过） |
| StaticMesh 三角形渲染 | ✅ | ✅（本地 GPU 环境验证通过） |
| Scene/SceneProxy 统一同步 + 视锥剔除 + 光源数据通路 | ✅ | ⏳（待本地 GPU 环境运行验证） |
| 材质系统（Material/MaterialInstance + shader 编译缓存） | ✅ | ✅（本地 GPU 环境验证通过） |
| 前向光照着色（Blinn-Phong） | ✅ | ✅（本地 GPU 环境验证通过） |
| 阴影贴图（ShadowDepth pass + 前向采样） | ✅ | ✅（本地 GPU 环境验证通过） |
| RenderGraph 声明式多 pass 编排（ShadowDepth → BlinnPhong） | ✅ | ✅（本地 GPU 环境验证通过） |
| 法线贴图（Normal Mapping，导数法 TBN） | ✅ | ✅（本地 GPU 环境验证通过） |

---

## 二、设计（未实现 / 待实现）

按优先级分组。详见 [RenderPipeline-Design.md §14](./RenderPipeline-Design.md#14-未决事项--后续阶段)
与 [SceneSync-Design.md §13](./SceneSync-Design.md#13-未决事项)。

### P1 —— 资源生命周期与性能（下一步）

1. **资源生命周期（部分落地）**：GPU 几何在 `StaticMesh` 被 `Dispose`/GC 回收时，经 `ResourceManager`
   延迟释放（渲染线程帧末 drain，ADR-7）；CPU 顶点/索引仍常驻（由 .NET GC 管理），磁盘流式加载与
   CPU 数据驱逐留待 P3-9。
2. **ADR-7 延迟删除队列（收尾）**：已落地于场景代理状态（`BlinnPhongRenderer._pendingDelete`）；
   待补：`RenderTargetRegistry` 仍直接 Remove，窗口视口销毁未走延迟删除。
3. **dirty 标记 + 增量更新**：`SceneComponent` 变换 setter 标记 dirty，只重算/提交变化的对象，
   静态对象复用上一帧快照（当前每帧全量快照）。

### P2 —— 渲染能力扩展

4. **`TextureRenderTarget`**：离屏渲染目标（无交换链），解锁后处理链/阴影贴图/小地图/编辑器预览。
5. **材质系统 + 纹理采样 + 实际光照着色（部分落地）**：P0~P3 已实现（结构化材质 + shader 编译缓存 +
   Blinn-Phong 前向着色 + 法线贴图）；节点图（P4）、PBR 未实现。
6. **帧内渲染依赖 / 拓扑排序（已落地 RenderGraph 核心）**：`RenderGraph` 已实现声明依赖 + 拓扑排序 +
   生命周期 + 简化剔除，取代"填写顺序 = 渲染顺序"；后处理链（相机 A 渲到贴图 → 相机 B 采样）待接入。
7. **剔除加速结构 + 遮挡剔除**：基础球-视锥剔除已实现；BVH/八叉树/遮挡剔除未实现。

### P3 —— 引擎完善

8. **输入系统**：当前无键盘/鼠标输入。
9. **资源管理器**：异步加载、资源缓存。
10. **`ViewportRect` 分屏 / 编辑器多视图**：一个 surface 渲染多个子视口。
11. **PresentMode 由 `EngineOptions` 暴露**：可切 VSync。
12. **surface lost 完整恢复**：当前策略是跳过本帧 + 下次 acquire 重配。
13. **`EngineApplication` 生命周期回调公开化**：`OnInitialize`/`OnUpdate`/`OnUninitialize` 当前为
    private 空实现，需公开供子类/游戏逻辑覆写。
14. **Editor 项目落地**：`UseEditor` 当前为空壳。
15. **单元测试**：`DualFrameBuffer`/`BoundingSphere`/`Frustum` 已具备可测性；
    （`Directory.Packages.props` 已引入 xunit 但未写测试）。

---

## 设计原则

> 详见 [RenderPipeline-Design.md §2](./RenderPipeline-Design.md#2-设计原则) 与
> [SceneSync-Design.md §2](./SceneSync-Design.md#2-设计原则在-adr-1adr-7-上新增)

- **P1 裸指针不出核心库**：`Surface*` 等只存在于 `RenderSurface` 内部
- **P2 资源线程归属唯一**：GPU 资源归渲染线程，逻辑线程经资源 ID + 注册表间接引用
- **P3 帧数据一致性**：双缓冲 + 值快照，渲染线程读到的永远是一帧完整一致的数据
- **P4 懒重配**：surface 尺寸/PresentMode/lost 变化在 acquire 前检查并重配
- **P5 所有权单向**：平台层创建/销毁 `RenderSurface`，渲染系统只引用
- **P6 渲染目标统一**：相机输出不限于窗口，`RenderTarget` 抽象统一窗口与贴图
- **P7 一条通道，分类 payload**：所有场景对象共用 `SceneProxy → SceneSnapshot → BlinnPhongRenderer`
  单通道，差异只在 payload 结构与渲染侧消费者
- **P8 静态上传一次，动态每帧快照**：几何/纹理上传一次；变换/包围盒/光源参数/骨骼姿态每帧快照
- **P9 稳定 ID + 生命周期 diff**：`ProxyId` + 集合比对得出新增/存活/销毁
- **P10 剔除归渲染线程**：逻辑线程提交完整对象集 + bounds，渲染线程按相机剔除
- **P11 语义手写、样板生成**：component 是唯一权威（字段/默认值/Bounds 规则手写）；proxy/payload/
  快照登记点等传输样板由 SceneGen 源生成器产出
- **P17 管线可替换**：`IRenderPipeline` 抽象 + DI 注册切换（`UseBlinnPhong`/未来的 `UseDeferred`），
  渲染线程与场景同步只依赖接口

> 材质系统原则 P12~P16（shader 缓存 / 静态动态分离 / 材质即资源 / 绑定组分层 / 结构化参数先行）见
> [MaterialSystem-Design.md §2](./MaterialSystem-Design.md#2-设计原则在-p1p11-上新增)。

## 决策记录（ADR）

> 详见 [RenderPipeline-Design.md §12](./RenderPipeline-Design.md#12-决策记录adr) 与
> [SceneSync-Design.md §12](./SceneSync-Design.md#12-决策记录adr续-renderpipeline-designmd-12)

| ID | 决策 |
|---|---|
| ADR-1 | `SceneSnapshot` 值快照 + 资源 ID，帧由相机驱动（场景对象统一 header + 分类 payload） |
| ADR-2 | Surface resize 每帧懒重配 |
| ADR-3 | 裸指针封装为 `RenderSurface` |
| ADR-4 | 尺寸用物理像素 `FramebufferSize` |
| ADR-5 | `RenderSurface` 由平台层创建/销毁 |
| ADR-6 | 渲染目标统一 `RenderTarget` 抽象 |
| ADR-7 | 资源销毁走延迟删除队列（**已落地于场景代理状态**，视口销毁待接入） |
| ADR-8 | 所有场景对象共用 `SceneProxy → SceneSnapshot → BlinnPhongRenderer` 单通道 |
| ADR-9 | 静态数据 upload-once 资源注册表，动态数据每帧值快照 |
| ADR-10 | 场景对象用稳定 `ProxyId` + 集合 diff 表达新增/存活/销毁 |
| ADR-11 | 剔除归渲染线程：逻辑提交完整对象集 + bounds，渲染线程按相机剔除 |
| ADR-12 | 传输样板（proxy/payload/快照字段/Capture）由源生成器按 `[SceneProxy]`/`[ScenePayload]` 产出，语义手写 |
| ADR-13 | `Material`（静态 shader）与 `MaterialInstance`（参数覆写）分离，实例不产生新 shader |
| ADR-14 | shader 变体用值类型 `MaterialShaderKey` 折叠 + 进程内编译缓存 |
| ADR-15 | 纹理槽恒绑定（5 槽 + fallback），`TextureFlags` 只改生成代码 |
| ADR-16 | 绑定组按更新频率分四层（frame/object/params/textures），布局全局唯一 |
| ADR-17 | 未指定材质回退引擎内置 DefaultMaterial |
| ADR-18 | P0~P3 用固定参数集 + WGSL 模板 codegen，节点图 codegen 后置（P4） |
| ADR-19 | `MaterialInstance : Material`，组件成员统一类型 `Material?`，v1 不引入 `IMaterial` |
| ADR-20 | 先 `Lit`(Blinn-Phong)，`PBR` 作为 metallic/roughness 已就绪的顺延扩展 |
| ADR-21 | 管线抽象 `IRenderPipeline` + DI 注册切换（`UseBlinnPhong`），渲染线程只依赖接口 |

## 构建与运行

```bash
# 构建
dotnet build Spark.Engine.slnx

# 运行演示（需本地 GPU 环境）
dotnet run --project Demo/Demo.Desktop
```

> 注意：WebGPU 依赖原生 wgpu（Silk.NET.WebGPU.Native.WGPU），需硬件 GPU 环境；
> 软件渲染器/远程桌面下可能报 "Invalid surface"。

## 关联文档

- [RenderPipeline-Design.md](./RenderPipeline-Design.md) — 渲染管线详设（含类图、UE 对比）
- [SceneSync-Design.md](./SceneSync-Design.md) — 逻辑/渲染线程场景同步机制（Scene/SceneProxy/SceneSnapshot）
- [MaterialSystem-Design.md](./MaterialSystem-Design.md) — 材质系统设计（资产模型/shader 缓存/绑定组/着色/实例化/多 pass）
- [RenderGraph-Design.md](./RenderGraph-Design.md) — 帧图（RenderGraph）设计（声明式依赖图/资源生命周期/别名复用）
- [ShadowMapping-Design.md](./ShadowMapping-Design.md) — 阴影贴图设计（多 pass 阴影 + 踩坑经验）
