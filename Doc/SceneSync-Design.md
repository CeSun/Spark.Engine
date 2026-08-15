# Spark.Engine 逻辑线程 ↔ 渲染线程场景同步设计

> 状态：已实现（本文描述最终落地的实现；§1 为重构前的旧状态背景）
> 前置：见 [RenderPipeline-Design.md](./RenderPipeline-Design.md)（ADR-1 值快照、ADR-7 延迟删除）
> 关联代码：`Src/Spark.Engine/Render/*`、`Src/Spark.Engine.SceneGen/`、`Src/Spark.Engine/Components/*`、
> `Src/Spark.Engine/EngineApplication.cs`

## 1. 背景与问题

重构前，帧数据通路只覆盖两类对象：`FrameData.Cameras`（`CameraRenderInfo`）与
`FrameData.RenderItems`（`RenderItem` = MeshId + 世界矩阵）。`EngineApplication.FillFrameData` 每帧调
`World.CollectCameras` / `World.CollectRenderItems` 两次全量遍历场景图，临时拼出两个列表；静态网格经
`ConcurrentQueue<StaticMesh>` 上传一次，渲染线程按 `MeshId` 建 `MeshGPUResource`。

这套机制无法平滑扩展到光源、骨骼网格等后续类别：

1. **无稳定身份**：`RenderItem` 每帧临时构造，渲染侧没有跨帧持久的"场景对象"来挂接状态。
2. **无包围盒**：只有矩阵，无法在渲染线程做视锥剔除。
3. **生命周期隐式**：对象"在/不在"列表即增删，没有显式 create/destroy 信号。
4. **每类一套机制**：每加一类就要复制"遍历 + 列表 + 队列 + 字典"。

本设计把"逻辑线程 → 渲染线程"收敛为**单通道**：`Scene`（注册代理）→ `SceneSnapshot`（值快照）→
`SceneRenderer`（镜像 + GPU 资源），让所有场景对象（网格/光源/未来的骨骼/粒子…）共用一套身份与
生命周期协议、一份线程安全契约，并把剔除所需的 bounds 随快照送达渲染线程。

## 2. 设计原则（在 ADR-1~ADR-7 上新增）

- **P7 一条通道，分类 payload**：所有场景对象共用 `SceneProxy → SceneSnapshot → SceneRenderer`
  单通道；差异只在分类 payload 结构与渲染侧消费者。
- **P8 静态上传一次，动态每帧快照**：几何/纹理等不可变数据走 upload-once 资源注册表；变换/包围盒/
  光源参数/骨骼姿态等可变数据走每帧快照。
- **P9 稳定 ID + 生命周期 diff**：每个场景对象有全局单调 `ProxyId`；渲染侧用「快照 ID 集合 vs 本地
  状态字典」比对得出新增/存活/销毁。
- **P10 剔除归渲染线程**：逻辑线程提交完整（未剔除）对象集 + bounds，渲染线程按相机剔除。
- **P11 语义手写、样板生成**：component 是唯一权威（字段/默认值/Bounds 规则手写）；proxy/payload/
  快照登记点等传输样板由 SceneGen 源生成器产出。

## 3. 总体模型（三层架构）

```
┌──────────────────── 逻辑线程 ─────────────────────────┐
│  World（Actor → Component）                            │
│    │ 组件生命周期 BeginPlay/Update/EndPlay（生成）     │
│    ▼                                                   │
│  Scene（逻辑侧渲染场景注册表，稳定 ProxyId）           │
│    └─ SceneProxy：StaticMeshSceneProxy / LightSceneProxy│
│            │ Capture() 每帧序列化（生成器产出的代理）  │
│            ▼                                           │
│  SceneSnapshot（值快照：header 数组 + 分类 payload）   │
└──────────────┼─────────────────────────────────────────┘
               ▼  DualFrameBuffer<SceneSnapshot>（双缓冲，超前≤1帧）
┌──────────────┴─────────────────────────────────────────┐
│  SceneRenderer（渲染线程）                             │
│    ├─ 上传处理 + 生命周期 diff（ADR-7 延迟删除）        │
│    ├─ GPU 资源注册表：MeshGPUResource（几何）          │
│    ├─ 每实例状态：StaticMeshRenderState（MVP，按 ProxyId）│
│    └─ acquire → 剔除 → clear → draw → present          │
└─────────────────────────────────────────────────────────┘
```

与 UE 的映射：`Scene` ≈ `FScene`，`SceneProxy` ≈ `FPrimitiveSceneProxy`，`SceneSnapshot` ≈ 场景基元提交，
`SceneRenderer` ≈ `FSceneRenderer` 输入侧。骨架同构，差异是早期简化（单一双缓冲、无命令流、无 RHI 线程）。

## 4. 逻辑侧：`Scene` + `SceneProxy`（手写框架）

`Scene` 是逻辑线程拥有的"渲染场景"注册表，从 World 的 Actor 图解耦；任何要让渲染线程看到的东西都注册
一个 `SceneProxy`。组件在 `BeginPlay` 注册、每帧 `SyncProxy` 更新、`EndPlay` 注销。

```csharp
public sealed class Scene
{
    private int _nextProxyId;
    private readonly Dictionary<int, SceneProxy> _proxies = new();

    public ResourceManager? ResourceManager { get; set; }   // 资源自动上传 + GPU 延迟释放（由组合根接线）

    public T Register<T>(T proxy) where T : SceneProxy;  // 分配 ProxyId 并登记
    public void Unregister(int proxyId);
    public void Capture(SceneSnapshot snapshot);          // 每帧序列化（逻辑线程独占）
}

public abstract class SceneProxy : IDisposable
{
    public int ProxyId { get; internal set; }
    public Matrix4x4 WorldTransform { get; set; }
    public BoundingSphere Bounds { get; set; }            // 世界空间包围球（剔除用）
    public VisibilityFlags Visibility { get; set; } = VisibilityFlags.Visible;

    public abstract void Capture(SceneSnapshot snapshot); // 写 header + 分类 payload
    public virtual void Dispose() { }
}

public enum SceneCategory : byte { StaticMesh = 1, Light = 2 }
[Flags] public enum VisibilityFlags : byte { None = 0, Visible = 1, CastShadow = 2, ReceiveShadow = 4 }
public enum LightType : byte { Point = 1, Directional = 2, Spot = 3 }
```

组件是**唯一权威**：`[SceneProxy(类别)]` 标记组件、`[ScenePayload]` 标记进 payload 的字段（默认值只在此处）。
组件生命周期（`ActorComponent.BeginPlay/Update/EndPlay`，对应 UE TickComponent）已落地，`Actor` 转发；
带 `[SceneProxy]` 的组件的注册/同步/注销由 SceneGen 生成的 partial 实现（见 §6）。

## 5. 传输层：`SceneSnapshot`（值快照，ADR-1 扩展）

`SceneSnapshot`（partial）只含 blittable 值类型与资源 ID，绝不携带 GPU/原生指针或跨线程对象引用。
分类 payload 缓冲（`StaticMeshes`/`Lights`）与 payload struct 由生成器产出；缓冲用 `FrameBuffer<T>` 池化。

```csharp
public sealed partial class SceneSnapshot
{
    public float DeltaTime;
    public uint FrameIndex;
    public readonly FrameBuffer<CameraSnapshot> Cameras = new();   // 视图（消费者）
    public readonly FrameBuffer<SceneObjectHeader> Objects = new(); // 场景对象统一 header

    public void Clear();                                            // Cameras/Objects + 生成的 ClearPayloads()
    public SceneObjectHeader AddObject<T>(... FrameBuffer<T> payloads, in T payload);  // 收口成一行
}

public readonly struct SceneObjectHeader   // 剔除与生命周期所需的最小公共面
{
    public readonly int ProxyId;            // 稳定 ID → 渲染侧状态索引 + 生命周期 diff
    public readonly SceneCategory Category;
    public readonly Matrix4x4 WorldTransform;
    public readonly BoundingSphere Bounds;  // 世界空间包围球（视锥剔除）
    public readonly VisibilityFlags Visibility;
    public readonly int PayloadIndex;       // 指向本类别 payload 数组的紧凑下标
}

public readonly struct CameraSnapshot       // 视图快照（逻辑线程算好矩阵）
{
    public readonly int TargetId;
    public readonly Matrix4x4 ViewMatrix;
    public readonly Matrix4x4 ProjectionMatrix;
    public readonly Vector4 ClearColor;     // 来自 CameraComponent.ClearColor
}
```

`AddObject<T>` 把「算 PayloadIndex → 追加 payload → 追加 header」收口成一行，供生成的 `Capture` 复用。

## 6. 源生成器（SceneGen）：写 Component，生成 Proxy

proxy/payload/Capture/生命周期等传输样板由 `Spark.Engine.SceneGen`（`IIncrementalGenerator`，netstandard2.0）
按 attribute 驱动生成。**语义手写、样板生成**的边界：

- 组件标记 `[SceneProxy(类别)]`，`[ScenePayload]` 字段/属性带默认值（**默认值只在组件**）；快照字段名由
  生成器从类别推导（Mesh 结尾 → +es，其余 → +s）。
- 生成器产出：proxy 子类（字段镜像 + 一行 `Capture`）、payload struct、组件的 partial（`_proxy` +
  `BeginPlay/Update/EndPlay` + `SyncProxy`）、`SceneSnapshot` 的分类 payload 字段与 `ClearPayloads`。
- **资源成员降级**：`[ScenePayload]` 成员若其类型实现 `ISceneResource`（`int ResourceId { get; }`），
  生成器自动把它降级为 `{Name}Id`（int）进 payload，并在 `SyncProxy` 里发
  `_proxy.XId = X?.ResourceId ?? 0` 与 `Owner?.World?.Scene?.ResourceManager?.EnsureUploaded(X)`。
- 每类专属语义经 `partial void OnProxyMapped(<Proxy> proxy)` 钩子手写（如 Bounds 规则），生成器声明、
  用户实现。

```csharp
// 资源成员示例：Mesh 自动降级为 MeshId 进 payload，自动上传，无需手写桥接
[SceneProxy(SceneCategory.StaticMesh)]
public partial class StaticMeshComponent : SceneComponent
{
    [ScenePayload] public StaticMesh? Mesh { get; set; }   // : ISceneResource
    [ScenePayload] public int MaterialId { get; set; }

    partial void OnProxyMapped(StaticMeshSceneProxy proxy)
        => proxy.Bounds = Mesh == null ? default : Mesh.Bounds.Transform(WorldTransform);
}
```

**`ResourceManager`**（`Scene.ResourceManager`，由组合根接线）：按 `MeshId` 去重的「首次引用自动上传」；
渲染侧 `ProcessUploads` 再按 `MeshId` 去重兜底。GPU 几何的释放：`StaticMesh.Dispose`/终结器把 `MeshId`
入队（静态队列，终结器无实例可达），渲染线程帧末 drain 并 `_gpuResources.Remove` 释放——CPU 数据由 .NET GC
管理，GPU 几何由 ADR-7 延迟删除，两层都不用手写引用计数。

## 7. 渲染侧：`SceneRenderer`

渲染线程的镜像。持久部分（GPU 资源、每实例状态）跨帧保留，快照部分每帧覆盖：

```csharp
public sealed class SceneRenderer
{
    private readonly Dictionary<int, IGPUResource> _gpuResources;   // 单注册表，按 ResourceId 上传一次
    private readonly Dictionary<int, StaticMeshRenderState> _proxyStates;  // 每实例 MVP，按 ProxyId
    private readonly Queue<StaticMeshRenderState> _pendingDelete;   // ADR-7 延迟删除

    public void Render(SceneSnapshot snapshot);   // ProcessUploads → SyncProxyStates → 分组 → acquire →
                                                  // 剔除 → clear → draw → present → FlushPendingDelete
}
```

- `MeshGPUResource`：顶点/索引缓冲（**几何资产**，按 MeshId 上传一次，多实例共享）。
- `StaticMeshRenderState`：每实例 MVP uniform + bind group（按 ProxyId 生命周期管理，修复多实例共享
  单 buffer 的问题）。

## 8. 身份与生命周期协议

`SyncProxyStates` 用**集合 diff**（全量快照 + ID 比对）得出三类信号：

```
1. 快照有、本地无 → 新增：创建 StaticMeshRenderState（懒建 GPU 资源）
2. 两边都有    → 存活：变换/参数来自每帧快照，无需持久更新
3. 本地有、快照无 → 销毁：移入 _pendingDelete
```

帧末 `FlushPendingDelete` 统一释放——这就是 ADR-7 延迟删除的落地。v1 为全量快照；`IsDirty` 增量快照
（只带 Added/Changed/Removed delta）是 P1-3 的既定升级路径。

## 9. 线程安全契约

| 对象 | 归属线程 | 访问规则 |
|---|---|---|
| `Scene` / `SceneProxy` / 组件 | 逻辑线程 | 仅逻辑线程读写；渲染线程**永不触碰** |
| `SceneSnapshot`（双缓冲两槽） | 各自独立 | 逻辑线程独占"空槽"，渲染线程独占"就绪槽"；`DualFrameBuffer` 保证互不重叠 |
| `SceneRenderer._proxyStates` | 渲染线程 | 仅渲染线程读写 |
| GPU 资源注册表（`_gpuResources`） | 渲染线程 | 逻辑线程经 `ResourceManager` 单向入队请求创建/释放 |
| 资源销毁 | 渲染线程 | 逻辑线程只发"注销"信号，渲染线程帧末延迟释放（ADR-7） |

不变式：**值快照 + 资源 ID**（无指针/对象引用/GPU 句柄）、**单写者双缓冲**（≤1 帧，槽互不重叠）、
**所有权单向**（GPU 资源归渲染线程，逻辑线程经 ID 间接引用）、**销毁单向**（物理释放延迟到帧末）。

## 10. 渲染线程剔除流水线

有了完整 header 数组（含 bounds），剔除在渲染线程完成：

```csharp
var frustum = Frustum.FromViewProjection(camera.ViewMatrix * camera.ProjectionMatrix);
foreach (ref readonly var obj in snapshot.Objects.Span)
{
    if ((obj.Visibility & VisibilityFlags.Visible) == 0) continue;
    if (!obj.Bounds.Intersects(frustum)) continue;   // 球-视锥粗剔除
    switch (obj.Category) { StaticMesh → 画；Light → 收集进 visibleLights； }
}
```

`Frustum`（Gribb-Hartmann 提取 6 平面）+ `BoundingSphere`。后续演进：AABB/遮挡剔除、BVH/八叉树
（P2-7）、GPU-driven culling、光源 tile-based/deferred 分桶。

## 11. 各类别接入方式

| 类别 | 静态数据（上传一次） | 动态数据（每帧快照） | 渲染侧消费者 |
|---|---|---|---|
| 静态网格 | 顶点/索引/材质 | 变换 + bounds（header） | 剔除后直接画 |
| 骨骼网格（未来） | 网格 + 骨架绑定 | 变换 + bounds + 皮肤矩阵 | 剔除 → 蒙皮 → 画 |
| 光源 | （阴影贴图槽按需） | 类型/颜色/强度/范围/锥角 | 剔除 + 衰减 → light buffer |
| 实例化（未来） | 网格 + 材质 | 每实例变换数组 | 剔除后合批 instanced draw |

要点：**新增一个类别 = 写一个带 `[SceneProxy]` 的组件（`[ScenePayload]` 字段，资源则 `ISceneResource`）
+ 在 `SceneRenderer` 加消费分支**；proxy/payload/快照登记点/生命周期全部由生成器产出，同步机制、身份
协议、线程契约、剔除循环零改动。

## 12. 迁移路径（已完成）

- **步骤 1（统一结构）** ✅：`SceneSnapshot` + `SceneObjectHeader` + `BoundingSphere` + `FrameBuffer<T>`；
  相机改 `CameraSnapshot`、清屏色下沉 `CameraComponent.ClearColor`。
- **步骤 2（生命周期）** ✅：`Scene` + `SceneProxy` + 组件 `BeginPlay/Update/EndPlay`；渲染侧集合 diff +
  ADR-7 延迟删除（`SceneRenderer._pendingDelete`）。
- **步骤 3（生成 + 扩展）** ✅：SceneGen 源生成器（proxy/payload 生成 + 资源成员降级/自动上传）+
  渲染线程剔除正式启用。

> 注：步骤 2 中「`RenderTargetRegistry` 视口销毁走延迟删除」尚未落地（仍直接 Remove），见 §13。

## 13. 决策记录（ADR，续 RenderPipeline-Design.md §12）

| ID | 决策 | 备选 | 理由 |
|---|---|---|---|
| ADR-8 | 所有场景对象共用 `SceneProxy → SceneSnapshot → SceneRenderer` 单通道 | 每类独立一套列表/队列/注册表 | 一条线程契约、一套身份与生命周期协议，新增类别不复制机制 |
| ADR-9 | 静态数据 upload-once 资源注册表，动态数据每帧值快照 | 每帧重传全部（含几何） | 几何带宽与 GPU 重建成本不可接受 |
| ADR-10 | 场景对象用稳定 `ProxyId` + 集合 diff 表达新增/存活/销毁 | 依赖列表出现/消失隐式表达 | 渲染侧需要持久状态，必须显式 create/destroy 信号 |
| ADR-11 | 剔除归渲染线程：逻辑提交完整对象集 + bounds | 逻辑线程剔除后提交 | 剔除与逻辑 tick 解耦，便于未来遮挡/GPU-driven |
| ADR-12 | 传输样板由 SceneGen 按 `[SceneProxy]`/`[ScenePayload]` 产出，语义手写 | 每类手写 proxy/payload | component 唯一权威；新增类别不复制样板，一致性由生成器保证 |

## 14. 未决事项

- `RenderTargetRegistry` 视口销毁仍直接 Remove（ADR-7 收尾，窗口 surface 销毁竞态）。
- 资源生命周期收尾：GPU 几何延迟释放已落地（`StaticMesh.Dispose`/终结器 → `ResourceManager` → 渲染线程
  帧末 drain）；CPU 数据驱逐与磁盘流式加载留待 P3-9。
- P1-3：dirty 增量快照（当前每帧全量快照）。
- P2-4/5/6/7：`TextureRenderTarget`、材质/纹理/实际光照着色、帧内依赖拓扑、BVH/遮挡剔除。
- 动态 buffer（骨骼皮肤矩阵、实例变换）分配策略：ring buffer vs 每对象独立 buffer。
