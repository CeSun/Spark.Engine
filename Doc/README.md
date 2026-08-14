# Spark.Engine 项目设计与现状

> 状态：持续演进中
> 本文是项目级设计总览，明确区分「已实现」与「设计（未实现）」。
> 渲染管线详设见 [RenderPipeline-Design.md](./RenderPipeline-Design.md)。

## 概述

Spark.Engine 是一个用 C# 从零实现的跨平台游戏引擎，渲染后端基于 **WebGPU**（Silk.NET 绑定，
Native 实现为 wgpu），窗口基于 Silk.NET.Windowing，基础设施基于 Microsoft.Extensions.DependencyInjection
与 Serilog。场景对象模型借鉴 Unreal Engine（World → Actor → Component）。

当前处于**早期原型阶段**：渲染管线已能绘制静态网格（三角形），场景系统已接入主循环，
但材质、纹理、光照、编辑器等尚未实现。

## 解决方案结构

```
Spark.Engine.slnx
├─ Src/
│  ├─ Spark.Engine/          核心引擎库（net10.0，唯一 WebGPU 依赖点）
│  ├─ Spark.Engine.Desktop/  桌面平台后端（net11.0，Silk.NET.Windowing）
│  └─ Spark.Engine.Editor/   编辑器（net11.0，空壳）
├─ Demo/
│  ├─ Demo/                  空类库
│  └─ Demo.Desktop/          可运行演示（三角形渲染）
└─ Doc/                      设计文档
```

## 架构分层

```
Demo（入口：EngineBuilder → UseDesktop → Build → Run）
  │
  ▼
EngineApplication（主循环：窗口事件 → 世界更新 → 填帧数据 → 提交）
  ├─ WindowManager（窗口生命周期 + Viewport 创建）
  ├─ WorldContext → World（Actor 增删/更新/相机与网格收集）
  └─ DualFrameBuffer<FrameData>（双缓冲，逻辑→渲染）
       │
       ▼
RenderThread（渲染循环：上传处理 → 分组 → acquire → clear → draw → present）
  ├─ RenderTargetRegistry（窗口视口/离屏贴图注册表）
  ├─ MeshGPUResource 注册表（网格 GPU 资源）
  └─ RenderPipeline / BindGroupLayout / ShaderModule
```

---

## 一、已实现

### 1. 引导与依赖注入（Builder）

- `EngineBuilder.Create(args)`：配置 Serilog 日志（控制台 + 滚动文件）、`EngineOptions`、
  `RenderTargetRegistry`、`WindowManager`
- `InitializeWebGPU()`：创建 instance 并注册 `WebGPUContext`；首个 surface 创建后按兼容性选择
  adapter，再创建 device/queue
- `UseDesktop()`：注册 `IWindowBackend`（桌面实现）
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

### 5. 帧数据（FrameData）

- `FrameData`：`DeltaTime`/`FrameIndex`/`Cameras`/`RenderItems`——**值快照 + 资源 ID**，
  绝不携带 GPU 指针或跨线程对象引用
- `CameraRenderInfo`：`TargetId` + 视图/投影矩阵 + 清屏色
- `RenderItem`：`MeshId` + 世界矩阵
- 帧由**相机驱动**：逻辑线程遍历活跃相机，渲染线程按目标分组渲染

### 6. 双缓冲帧同步（DualFrameBuffer）

- 单生产者/单消费者双缓冲，逻辑线程最多超前渲染线程 1 帧
- Present 回压闭环隐式成立：present 慢 → acquire 阻塞 → 逻辑线程降速

### 7. 多线程

- `RenderThread`：渲染循环（上传处理 → 分组 → acquire → clear → draw → present）+ 资源释放
- `EngineSynchronizationContext`：`Post`/`Send` 把异步回调封送到主引擎线程

### 8. 场景系统（World → Actor → Component）

- `World`：`AddActor`/`RemoveActor`（延迟增删）、`Update`、`CollectCameras`、`CollectRenderItems`
- `WorldContext`：`CurrentWorld` 可设置
- `Actor`：`BeginPlay`/`Update`/`EndPlay` 生命周期、`Components`/`GetComponent<T>`/世界归属
- `ActorComponent`：`Owner` 归属
- `SceneComponent`：相对位置/旋转/缩放（可读写）、`WorldTransform`、父子挂载（挂载 API 未完成）
- `CameraComponent`：`RenderTarget`（可写，指向窗口视口或离屏贴图）、`Viewport` 便捷属性、
  FOV/Near/Far、`GetViewMatrix`/`GetProjectionMatrix`

### 9. 静态网格渲染

- `StaticMesh`：CPU 顶点（位置+颜色）/索引 + 全局 `MeshId`
- `StaticMeshComponent`：持有网格，世界变换来自 `WorldTransform`
- `MeshGPUResource`：渲染线程顶点/索引/MVP uniform buffer + bind group
- 上传队列：`EngineApplication.UploadMesh` → `ConcurrentQueue` → 渲染线程创建 GPU buffer + 上传
- WGSL 着色器（MVP uniform）+ `RenderPipeline` + bind group layout/pipeline layout
- draw 循环：每相机一个 render pass（首个 clear、后续 Load 叠加），`MVP = Transpose(World×View×Proj)`

### 10. 引擎应用与演示

- `EngineApplication`：主循环（窗口事件 → 同步上下文 → 世界更新 → 填帧数据 → 提交）、
  `UploadMesh` 入口、`ExitGame`
- `Demo.Desktop`：创建 World → 相机 Actor → 三角形网格 → 渲染

### 验证状态

| 能力 | 编译 | 运行 |
|---|---|---|
| 渲染管线骨架（清屏） | ✅ | ✅（本地 GPU 环境验证通过） |
| World 场景接入 | ✅ | ✅（本地 GPU 环境验证通过） |
| StaticMesh 三角形渲染 | ✅ | ✅（本地 GPU 环境验证通过） |

---

## 二、设计（未实现 / 待实现）

按优先级分组。详见 [RenderPipeline-Design.md §14](./RenderPipeline-Design.md#14-未决事项--后续阶段)。

### P1 —— 资源生命周期与性能（下一步）

1. **`StaticMeshHandle` 引用计数**：当前 `StaticMeshComponent.Mesh` 直接持有完整 CPU 网格数据，
   上传 GPU 后仍占用内存。改为轻量 Handle（`MeshId` + 引用计数），上传后释放 CPU 数据。
2. **ADR-7 延迟删除队列**：资源销毁跨线程安全化——逻辑线程标记删除，渲染线程帧末批量释放
   GPU buffer（当前 `RenderTargetRegistry` 直接 Remove，未走延迟删除）。
3. **dirty 标记 + 增量更新**：`SceneComponent` 变换 setter 标记 dirty，逻辑线程只重算/提交
   变化的物体，静态物体复用上一帧变换（当前每帧全量遍历 + 全量快照）。

### P2 —— 渲染能力扩展

4. **`TextureRenderTarget`**：离屏渲染目标（无交换链），解锁后处理链/阴影贴图/小地图/编辑器预览。
5. **材质系统 + 纹理采样 + 光照**：当前只有纯色顶点着色，无纹理、材质、光照。
6. **帧内渲染依赖 / 拓扑排序**：后处理链（相机 A 渲到贴图 → 相机 B 采样）、阴影贴图的
   pass 顺序。当前只保证"填写顺序 = 渲染顺序"。
7. **视锥剔除 + 空间划分**：可见性剔除（BVH/八叉树），当前全量提交。

### P3 —— 引擎完善

8. **输入系统**：当前无键盘/鼠标输入。
9. **资源管理器**：异步加载、资源缓存。
10. **`ViewportRect` 分屏 / 编辑器多视图**：一个 surface 渲染多个子视口。
11. **PresentMode 由 `EngineOptions` 暴露**：可切 VSync。
12. **surface lost 完整恢复**：当前策略是跳过本帧 + 下次 acquire 重配。
13. **`EngineApplication` 生命周期回调公开化**：`OnInitialize`/`OnUpdate`/`OnUninitialize` 当前为
    private 空实现，需公开供子类/游戏逻辑覆写。
14. **Editor 项目落地**：`UseEditor` 当前为空壳。
15. **单元测试**：`DualFrameBuffer` 已具备可测性；`RenderSurface` 的 dirty 判定可抽纯函数测试
    （`Directory.Packages.props` 已引入 xunit 但未写测试）。

---

## 设计原则

> 详见 [RenderPipeline-Design.md §2](./RenderPipeline-Design.md#2-设计原则)

- **P1 裸指针不出核心库**：`Surface*` 等只存在于 `RenderSurface` 内部
- **P2 资源线程归属唯一**：GPU 资源归渲染线程，逻辑线程经资源 ID + 注册表间接引用
- **P3 帧数据一致性**：双缓冲 + 值快照，渲染线程读到的永远是一帧完整一致的数据
- **P4 懒重配**：surface 尺寸/PresentMode/lost 变化在 acquire 前检查并重配
- **P5 所有权单向**：平台层创建/销毁 `RenderSurface`，渲染系统只引用
- **P6 渲染目标统一**：相机输出不限于窗口，`RenderTarget` 抽象统一窗口与贴图

## 决策记录（ADR）

> 详见 [RenderPipeline-Design.md §12](./RenderPipeline-Design.md#12-决策记录adr)

| ID | 决策 |
|---|---|
| ADR-1 | FrameData 值快照 + 资源 ID，帧由相机驱动 |
| ADR-2 | Surface resize 每帧懒重配 |
| ADR-3 | 裸指针封装为 `RenderSurface` |
| ADR-4 | 尺寸用物理像素 `FramebufferSize` |
| ADR-5 | `RenderSurface` 由平台层创建/销毁 |
| ADR-6 | 渲染目标统一 `RenderTarget` 抽象 |
| ADR-7 | 资源销毁走延迟删除队列（**未落地**） |

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
