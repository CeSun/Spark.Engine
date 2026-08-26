# Spark.Engine 引擎现状问题梳理（全面审计）

> 日期：2026-08-20
> 方法：全量构建（0 错误）+ 三路并行代码审计（渲染管线/GPU 资源、场景同步/多线程、UI/输入/平台）+ 设计文档与 worklog 逐条对照。所有问题均有代码证据（文件+行号）。
> 严重级别定义：**严重** = 会崩溃/死锁/渲染结果错误；**中** = 并发竞态/资源泄漏/顺序与一致性缺陷，特定条件下触发；**低** = 潜伏缺陷/健壮性/易用性。

---

## 修复记录

| 日期 | 问题 | 修复 |
|---|---|---|
| 2026-08-25 | S1 渲染线程异常→双线程死锁 | ✅ `RenderThread.Run`：`Render` + `ReturnEmpty` 包进 try/finally，finally 无条件归还帧槽 |
| 2026-08-25 | S2 主循环异常→帧槽泄漏 | ✅ `DualFrameBuffer` 新增 `Abandon()`（归还空槽不提交帧）；`EngineApplication` 主循环取缓冲后包 try/catch，异常路径 `Abandon()` + 重抛 |
| 2026-08-25 | S3 RenderGraph acquire 循环在 try 外 | ✅ 帧级 acquire 循环移入 try 内，finally 统一 dispose 已 acquire 的 session（交换链槽位不再永久占用） |
| 2026-08-25 | S4 窗口关闭竞态（Silk 窗口在渲染线程仍渲染时被销毁） | ✅ `IWindow` 新增 `DisposeNative()`；`DesktopWindow.Uninitialize()` 只释放输入上下文；渲染线程帧末释放 surface 后经 `RenderTargetRegistry` 握手队列登记，逻辑线程下一帧/退出时 `WindowManager.ProcessNativeDisposals` 调 `DisposeNative` 销毁原生窗口（Silk/GLFW 原生窗口必须在逻辑线程销毁，避免跨线程销毁导致关闭失效） |
| 2026-08-25 | S5 静态/骨骼网格各自独立深度缓冲 → 类别间无遮挡 | ✅ `BlinnPhongStageContext` 新增按 target id 共享的深度附件注册表（`GetSharedDepthTarget`）；静态 pass 保持 Clear、骨骼 pass 改 Load，同 target 共享一份深度缓冲，恢复遮挡正确性并省一份全屏深度缓冲 |
| 2026-08-25 | S6 FrameIndex 双缓冲重复（1,1,2,2…） | ✅ `EngineApplication` 自持单调计数器 `_frameIndex`，`snapshot.FrameIndex = ++_frameIndex` |
| 2026-08-25 | 中1 SyncProxy 顺序（运动相机下世界滞后 1 tick） | ✅ `SceneProxyGenerator` Update 模板改为 `base.Update → OnUpdate → SyncProxy`，代理抓取本 tick 逻辑之后的状态 |
| 2026-08-25 | 中8 RenderGraph 缺 read→write / write→write 边 | ✅ `Compile` 按资源收集访问事件，同一资源任意两个冲突事件（至少一个写）之间建边，补齐三类边，同资源多写者顺序确定（双相机叠加层级错误修复） |
| 2026-08-25 | 中9 `DependsOn` 只认注册序在前写者 | ✅ 维护「资源→最后写者」映射建边（只认注册序在前的写者）；修正初版"扫描全部写者"会连到后注册 UI pass 与 write→write 边成环的缺陷 |
| 2026-08-25 | 中10 CommandBuffer/Encoder 从不释放 | ✅ 4 处 `QueueSubmit`（BlinnPhongStage/SkeletalMeshStage/ShadowDepthStage/UIRenderer）后补 `CommandBufferRelease` + `CommandEncoderRelease`；交换链 acquire 纹理按 wgpu 语义由 surface 管理，不手动 release |
| 2026-08-25 | 中2 同帧 Add+Remove 被静默丢弃 | ✅ `World.RemoveActor` 命中待加队列则取消添加并清世界，actor 与其场景代理不再泄漏 |
| 2026-08-25 | 中3 生命周期回调重入改集合崩溃 | ✅ `World.Update` / `Actor.BeginPlay/Update/EndPlay` 对副本迭代，避免 "Collection was modified" |
| 2026-08-25 | 中5 异常路径 World 半更新 | ✅ add 侧异常回滚（移出 `_actors` + 清世界）；remove 侧 try/finally 保证移出 `_actors` + 清世界 |
| 2026-08-25 | 中4 CreateRenderView 逻辑线程直接调 WebGPU device | ✅ `TextureRenderTarget` 支持延迟 GPU 创建；创建请求经 `RenderTargetRegistry` 队列封送到渲染线程帧首（`SceneRenderPipeline.ProcessRenderViewCreations`），UI 采样在 GPU 就绪前回退白纹理 |
| 2026-08-25 | 中6 生成代理状态机缺保护（跨世界/换主幽灵对象） | ✅ 生成器缓存注册场景 `_registeredScene`（EndPlay 用它反注册，不再重推 scene）；BeginPlay 防重入；`Actor.AddOwnedComponent` 在 actor 已 BeginPlay 后补调组件 BeginPlay；移除恒 null 的 `RootComponent` 与死字段 `_attachParent` |
| 2026-08-25 | 中7 RenderSurface 尺寸跨线程数据竞争 | ✅ `_targetWidth/_targetHeight/_targetPresentMode` 改 `volatile`（逻辑线程写、渲染线程读） |
| 2026-08-25 | 中11 UI scissor 完全越界回退全视口 | ✅ clamp 后 `sw/sh ≤ 0` 时跳过该批（不再画出未裁剪内容） |
| 2026-08-25 | 中12 渲染视图 bind group 泄漏/悬垂 | ✅ 缓存记录创建时 View 指针，目标重建后校验版本并释放旧 bind group |
| 2026-08-25 | 中13 阴影→无阴影切换 `_frameBindGroup` 滞留 | ✅ 无阴影分支释放旧阴影 bind group，transient 阴影纹理不再滞留 |
| 2026-08-25 | 中14 被剔除 pass 的 transient 资源仍每帧全量分配 | ✅ `Execute` 只分配被未剔除 pass 使用的 transient 资源，无相机/无灯光帧不再建+毁阴影纹理 |

> S1/S2/S3 三处合计约 30 行改动，解除"任何一次异常 = 引擎冻结"的最大风险；S4/S5/S6 补齐窗口销毁竞态（逻辑线程握手销毁，见 S4 行）、遮挡正确性与帧号单调性。
> **严重级 S1-S6 与中等问题 1-14 已全部落地**（提交：`4023107` S1-S6 / `5e2b7e6` 中1,2,3,5,8,9 / `4c9decb` 中7,10,11,13 / `bb988fd` 中12,14 / `8d35f02` 中6 / `13cd036` 中4；另 `0f80e8c` 同步本文档）。剩余为轻微问题（低优先级）与性能/功能路线图项。

---

## 一、严重问题（6 个）

### S1. 渲染线程异常 → 双线程永久死锁 ★最高优先级

`Src/Spark.Engine/Threads/RenderThread.cs:39-47`：`GetReadyBuffer` / `Render` / `ReturnEmpty` 同在一个 try 内，`Render()` 一旦抛异常，`ReturnEmpty()` 永不执行 → `_readySlotAvailable`/`_emptySlots` 永久泄漏 → 渲染线程阻塞在 `GetReadyBuffer`（`DualFrameBuffer.cs:62`）、逻辑线程阻塞在 `SubmitReady`（`DualFrameBuffer.cs:53`）。退出流程（`IsClosing`/`Dispose`，`EngineApplication.cs:172-179`）在主循环之后，**永远执行不到**，只能杀进程。一次 shader 编译失败/图环检测/NRE 即冻结整个引擎。
**修复**：try/finally，finally 中无条件 `ReturnEmpty()`。

### S2. 主循环异常 → 帧槽泄漏，连续 2 次后主循环永久卡死

`Src/Spark.Engine/EngineApplication.cs:152-169`：`GetEmptyBuffer()` 之后、`SubmitReady()` 之前的任何异常（`OnUpdate`/`FillFrameData`/用户游戏逻辑）导致已取缓冲既不提交也不归还；`_emptySlots` 容量 2，每失败一次 -1，连续 2 次失败后 `GetEmptyBuffer` 永久阻塞。日志里 "execution will continue" 名不副实。
**修复**：异常路径增加 `Abandon()` 归还槽位（或把取/提交包进 try/finally 协议化）。

### S3. RenderGraph 帧级 acquire 循环在 try 之外 → 异常遗留未 present 的交换链 session

`Src/Spark.Engine/Render/RenderGraph/RenderGraph.cs:247-253`：多 viewport 时第 2 个 `BeginRenderSession()` 抛异常，前面已 acquire 的 session 无人 dispose/present → 交换链槽位永久占用。这是异常传入 S1 死锁的主要通道。
**修复**：acquire 循环整体纳入 try/finally。

### S4. 窗口关闭竞态：Silk 窗口在渲染线程仍在渲染时被销毁

`Src/Spark.Engine/WindowManager.cs:92` + `Src/Spark.Engine.Desktop/DesktopWindow.cs:73-80`：`Uninitialize()` 立即 `_window.Dispose()`（销毁 GLFW 原生窗口），但 `RenderTargetRegistry` 的延迟删除只推迟了 `Viewport.Dispose`/surface 释放；渲染线程本帧可能仍持有该 Viewport 并调用 `Window.FramebufferSize`/acquire（`Viewport.cs:30-42`、`RenderGraph.cs:252`）→ 原生层 use-after-free / 异常 → 沿 S3 → S1 死锁链。双窗口演示关任一窗口即有机会触发。
**修复**：Silk 窗口销毁也排入渲染线程帧末延迟队列，与 surface 释放同帧。

### S5. 静态/骨骼网格双 Stage 各自独立深度缓冲且每 pass Clear → 类别间无深度遮挡

`BlinnPhongRenderer.cs:96-102` 每相机注册两个 pass（静态+骨骼）；`BlinnPhongStage.cs:151-157` 与 `SkeletalMeshStage.cs:116-122` 各自把**自己的** `_depthTarget`（`BlinnPhongStage.cs:325-334` / `SkeletalMeshStage.cs:283-292`，两份全屏 Depth24Plus）Clear 到 1.0。角色三角形不与静态墙体做深度比较 → **角色永远画在墙之上**，遮挡关系错误；且每 target 白占两份深度缓冲。
**修复**：同 target 共享一份深度附件（RenderGraph 声明为共享 transient），或合并为一个 forward pass 按 draw 切换 skinned pipeline。

### S6. FrameIndex 双缓冲重复：渲染线程看到的帧序号是 1,1,2,2,3,3…

`EngineApplication.cs:190` `snapshot.FrameIndex++` 自增在**缓冲区实例**上，而 `DualFrameBuffer` 有 2 个交替复用的 `SceneSnapshot` 实例 → 每个值出现两次。任何按帧序号做时序逻辑（抖动、缓存失效）的消费者都会出错。
**修复**：`EngineApplication` 自持单调计数器，填入快照。

---

## 二、中等问题（14 个）

### 同步/并发

1. **同一快照内相机与物体 1 tick 相位差**（P3 帧一致性被破坏）：生成器模板 `SceneProxyGenerator.cs:200-205` 产出的 `Update` 是 `base.Update → SyncProxy() → OnUpdate()`——代理抓取的是用户本 tick 逻辑**之前**的状态；而相机矩阵在 `FillFrameData`（`EngineApplication.cs:221-226`）用**之后**的 `WorldTransform` 现算。运动相机下世界整体滞后 1 tick（60Hz ≈ 16.7ms）。**修复**：模板改为 `OnUpdate → SyncProxy`。
2. **World 同帧 Add+Remove 被静默丢弃** → actor 与其场景代理永久泄漏：`World.cs:29-36` 的 `RemoveActor` 只查 `_actors`，不查 `_pendingAddActors`。**修复**：命中待加队列则取消添加。
3. **生命周期回调重入改集合 → "Collection was modified" 崩溃**：`World.cs:40-53`（BeginPlay 里 AddActor / EndPlay 里 RemoveActor 其他 actor）、`Actor.cs:41-45`（组件回调里 AddOwnedComponent）。**修复**：对副本迭代或延迟变更队列。
4. **CreateRenderView 在逻辑线程直接调 WebGPU device 创建纹理**：`EngineApplication.cs:68-77`，运行期经 `UIRenderView` 布局回调触发（`UIRenderView.cs:91-112` ← `FillFrameData`），与渲染线程的 `QueueSubmit/DeviceCreate*` 并发，WebGPU device 无内部互斥 → 未定义行为。**修复**：创建/销毁经渲染线程请求队列（与 `ResourceManager._pendingUploads` 同模式）。
5. **异常路径 World 半更新**：`World.cs:40-53` 先改列表后回调（add）/先回调后改列表且异常时清空待删队列（remove）→ actor 滞留、代理半注册。**修复**：try/finally 保证列表一致。
6. **生成的代理状态机缺保护 → 跨世界/换主幽灵对象**：`SceneProxyGenerator.cs:187-218` —— BeginPlay 不查旧 `_proxy` 直接覆盖；EndPlay 从 `Owner?.World?.Scene` **重推** scene 而非用注册时缓存的引用 → actor 换世界/组件换主时旧代理在旧 Scene 里永不 Unregister，渲染端永判"存活"。另 `Actor.RootComponent`（`Actor.cs:12`）从未赋值恒为 null；`SceneComponent._attachParent` 是死字段（编译警告 CS0649），父子挂载未实现且 `WorldTransform` 不复合父级（`SceneComponent.cs:26-30`）；运行时 `AddOwnedComponent` 的组件若 actor 已 BeginPlay 则其 BeginPlay/代理注册**永不被调用**。
7. **第二窗口起 RenderSurface 尺寸跨线程数据竞争**：`WindowManager.cs:62-69` 逻辑线程 `Initialize` → `RenderSurface.Resize` 写普通字段（`RenderSurface.cs:50-56`），渲染线程 `EnsureConfigured` 并发读。**修复**：volatile 或封送。

### 渲染/资源

8. **RenderGraph 依赖图缺 read→write / write→write 边**：`RenderGraph.cs:91-150` 只建"写者→其后读者"边，同资源多写者顺序退化为 Kahn 队列 FIFO。已构造反例：双相机同 target 时执行序 0,1,3,2,4,5（B 相机静态 pass 插到 A 相机骨骼 pass 之前），叠加层级错误。单相机恰好正确掩盖了缺陷。**修复**：按资源建读写事件序列，补齐三类边。
9. **`DependsOn` 只认注册序在前的写者**（`RenderGraph.cs:128-149` `j < i`）：写者后注册时依赖被静默丢弃；装配器（`RenderGraphAssembler`）路径极易触发。**修复**：维护"资源→最后写者"映射建边。
10. **CommandBuffer 从不释放**（4 处 `QueueSubmit`，全仓 `CommandBufferRelease` 零命中）：`BlinnPhongStage.cs:176-177`、`SkeletalMeshStage.cs:139-140`、`ShadowDepthStage.cs:157-158`、`UIRenderer.cs:217-218`（CommandEncoder 同样未 Release）。每帧每 pass 泄漏一个命令缓冲，长跑线性增长。**另**：交换链 acquire 纹理只释放 view 不释放 texture（`FrameTexture.cs:33-37` vs `RenderSurface.cs:86-87`），疑似每帧泄漏 swapchain 纹理引用（建议按 wgpu-native 语义核实并补 `TextureRelease`）。
11. **UI scissor 完全越界时回退全视口**：`UIRenderer.cs:248-253` clamp 后 `sw/sh ≤ 0` 时本应跳过该批，实际设为全视口 → 未裁剪内容被画出。
12. **UIRenderer 渲染视图 bind group 泄漏/悬垂**：`UIRenderer.cs:289-296` 缓存只按 renderViewId 查存在性，不校验 View 版本；目标销毁后若不再被引用，bind group + view + 纹理保活至 `UIRenderer.Dispose`。
13. **阴影→无阴影切换时 `_frameBindGroup` 滞留 transient 阴影纹理**：`BlinnPhongStage.cs:180-222`（`SkeletalMeshStage` 同构）无阴影分支不释放旧 `_frameBindGroup` → 1024×1024 深度显存滞留到下次阴影帧或管线 Dispose。
14. **被剔除 pass 的 transient 资源仍每帧全量分配**：`RenderGraph.cs:241-245` 无条件 `_pool.Allocate`，无相机/无灯光帧也每帧建+毁 1024×1024 阴影纹理。与 RenderGraph-Design §6 验收标准不符（Phase D 未落地）。

---

## 三、轻微问题（摘选 14 个）

- **输入**：按键 repeat 被边沿计算丢弃（`DesktopWindow.cs:112` → `InputManager.cs:28-31`），文本框按住 Backspace 不连删；鼠标离开窗口后 hover/拖拽状态卡死；`SilkInputMapper` 未映射小键盘/KeypadEnter；`InputManager` 字典随窗口关闭只增不减。
- **UI**：`UIElement.AddChild` 同父重复挂载产生双份布局/事件（`UIElement.cs:60-64`）；焦点环不受祖先裁剪（`UICanvas.cs:62-76`）；`UIWrapPanel` Measure/Arrange 主轴限额差 Padding 换行点不一致；叶子控件 Measure 用注入渲染器、Paint 用 `ui.Text`，自定义渲染器时布局/绘制字号不一致；`UIRenderer.EnsureVertexCapacity` 超限静默 clamp 会越界写 GPU 缓冲（>16384 四边形/目标/帧）。
- **渲染/资源**：每帧 `new List`+LINQ 分配热点；`WebGPUContext` 的 Instance/Adapter/Device/Queue 从不 Release；UI 纹理/`TextRenderer` 字符串缓存只增不减（文档 §1.3/5.2 已自认，字形图集是根本解）；`SceneResource` 释放通知器竞态（`SceneResource.cs:17-31`）；`MaterialShaderCache` 键缺深度附件格式（`MaterialShaderCache.cs:159-179` 硬编码 Depth24Plus）；1×1 白纹理 `BytesPerRow=4` 未 256 对齐（`UIRenderer.cs:645`，依赖单行豁免，跨后端脆弱）；`RenderSurface.AspectRatio` 用目标尺寸而 `Width/Height` 用已配置尺寸，重配前不一致；`ImportTexture` 同 Id 覆盖（`RenderGraph.cs:58-63`）；`EngineSynchronizationContext.Send` 潜伏死锁（当前无调用方）；`FrameBuffer.Clear` 对引用类型 payload 是坑（当前全 blittable，已确证无泄漏）；`DualFrameBuffer` 协议依赖调用方纪律（双取同槽潜伏缺陷）。
- **Demo**：`TreeOpsVerifyOverlay` 场景 4 用例不构成环，验收恒显示 FAIL；相机摆放/资源加载/UI 接线样板代码重，缺 `CameraComponent.CreateLookAt` 等便捷 API。

---

## 四、性能问题（结构性，文档多已自认）

1. **主循环忙等**：`EngineApplication.cs:147-148` 帧率限制用 `continue` 自旋，无 Sleep → 占满一个核心。
2. **每帧全量快照**：无 dirty 增量更新（P1-3 未做）。
3. **TransientResourcePool 每帧新建/帧末释放**，无别名复用（Phase C）；无图编译缓存（每帧重建拓扑）。
4. **字符串级文本纹理**：动态文本每变一字符串一张纹理，GPU 内存持续增长（→ P9 字形图集）。
5. **剔除只有球-视锥**：无 BVH/八叉树/遮挡剔除（P2-7）。
6. **骨骼/实例动态 buffer** 每对象独立分配，未定 ring buffer 策略（SceneSync §14）。

---

## 五、功能缺口（文档自认路线图，按优先级）

- **P1**：dirty 增量快照；资源 CPU 数据驱逐/磁盘流式（P3-9）。
- **P2**：PBR（ADR-20 顺延）、材质节点图（P4）、半透明深度排序分批、`MAX_LIGHTS` 超限策略、shader 磁盘缓存、后处理链（相机 A→贴图→相机 B 采样）接入 RenderGraph、BVH/遮挡剔除。
- **P3**：异步资源加载/缓存、`ViewportRect` 分屏、`PresentMode` 可配置（VSync 开关）、surface lost 完整恢复（当前跳帧+重配）、`EngineApplication` 生命周期回调公开化（代码已是 protected virtual，文档过时）、Editor 项目（空壳）、单元测试（xunit 已在中央包管理声明但**无测试工程**）。
- **UI**：P7 文本框进阶（选择/剪贴板/Undo/多行/IME 组合态）、P8 控件覆盖率 ~30%（缺 TreeView/ListView/Menu/Dialog/TabView/ComboBox/ScrollBox 等编辑器刚需）、P9 渲染质量（圆角/边框/阴影/渐变/九宫格/DPI 缩放/脏标记增量绘制/嵌入字体）、P10 样式系统与数据绑定；**UI 不支持中文**（系统字体无 CJK 字形）。

---

## 六、工程配置与文档不一致

1. **NU1507 双包源**：仓库无 `NuGet.config`，第二个源（Avalonia nightly feed）来自用户级 `%APPDATA%\NuGet\NuGet.Config` → 全部项目警告。**修复**：仓库根加 `NuGet.config`（包源映射或单一 nuget.org）。
2. **TFM 混用 + 预览依赖**：核心库 net10.0，其余 5 项目 net11.0；`Microsoft.Extensions.*` 固定 11.0.0-preview.6；构建需 net11 preview SDK → 换机/稳定 SDK 即失败。建议统一 TFM 并换正式版包。
3. **CS0649**：`SceneComponent._attachParent` 从未赋值（父子挂载半成品）。
4. **文档与实现脱节**（均需回刷）：
   - `Doc/README.md` P3-8 仍称"当前无键盘/鼠标输入"，与 §16 输入系统已实现矛盾；
   - README P1-2 / `RenderPipeline-Design.md` §14 / `SceneSync-Design.md` §14 仍称"`RenderTargetRegistry` 仍直接 Remove，未走延迟删除"——代码已有 `_pendingRemovals` 队列（9d5d044 已收尾，真正残留的是 S4 的窗口销毁竞态）；
   - README 验证状态表"Scene/SceneProxy 统一同步+视锥剔除+光源数据通路 ⏳ 待验证"——其后所有功能均已 GPU 验证，此行过时；
   - `RenderPipeline-Design.md` §14 仍把 `TextureRenderTarget`、材质注册表、RenderGraph 命令流列为未决——均已落地；
   - `Material.cs:53` 注释"预留：法线贴图（codegen 尚未实现）"——法线贴图已实现（d13c745）；
   - README 结构图未列 SkeletalMeshStage/UIRenderer 等新模块。
5. **验证缺口**：P6-fix 补丁轮（Grid/裁剪/树操作/文本包围盒）仅编译验证、**未 GPU 实机验收**（`2026-08-19-p6fix-worklog.md` 自认）；`TreeOpsVerifyOverlay` 场景 4 用例本身错误。

---

## 修复优先级建议

1. **S1+S2+S3**（异常安全：try/finally + Abandon + acquire 收进 try）——三处合计约 30 行改动，解除"任何一次异常=引擎冻结"的最大风险。
2. **S4**（窗口销毁延迟到渲染线程）+ **S6**（全局帧号）。
3. **S5**（共享深度附件）——恢复遮挡正确性并省一份全屏深度缓冲。
4. **中 1**（SyncProxy 顺序）+ **中 2/3/5**（World 集合健壮性）+ **中 8/9**（依赖图补边）。
5. **中 10**（CommandBuffer/Encoder/纹理 Release 补齐）+ **中 4**（渲染视图创建封送到渲染线程）。
6. 工程配置（NuGet.config、TFM 统一、测试工程）+ 文档回刷。

## 审计中确证无问题的关键项

DualFrameBuffer 内存屏障与"超前 ≤1 帧"约束、ProxyId 单调不复用、生命周期 diff + ADR-7 时序、SceneGen 生成契约（编译通过）、退出流程 Dispose→Join 顺序、MVP/法线矩阵行列约定（双重转置自洽）、Frustum 提取与球-视锥测试、三组 uniform 布局（1200/128/64 字节）、阴影比较方向、正常路径 acquire/present 配对、每帧 transient bind group 重建、UI pass 顺序/混合、输入三态帧边界、裁剪栈按 target 隔离、文本全墨水包围盒。
