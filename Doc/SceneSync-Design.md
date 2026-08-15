# Spark.Engine 逻辑线程 ↔ 渲染线程场景同步设计

> 状态：已实现（对齐实际代码；与本文档草案的差异见下方「实现说明」）
> 前置：见 [RenderPipeline-Design.md](./RenderPipeline-Design.md)（ADR-1 值快照、ADR-7 延迟删除）
> 关联代码：`Src/Spark.Engine/Render/Scene.cs`、`SceneSnapshot.cs`、`SceneRenderer.cs`、
> `Src/Spark.Engine/Threads/RenderThread.cs`、`Src/Spark.Engine/Worlds/World.cs`、`Src/Spark.Engine/EngineApplication.cs`

> **实现说明**（本文档草案与实际代码的差异）：ID 统一用 `int`（对齐 `MeshId`/`TargetId`）；代理层次扁平化
> （`SceneProxy` 直接承载 WorldTransform/Bounds/Visibility，未单列 `PrimitiveSceneProxy`）；MVP uniform
> 按**每实例**（`StaticMeshRenderState`，按 ProxyId 管理）而非每网格；ADR-7 延迟删除已落地于
> `SceneRenderer._pendingDelete`；`FrameData`/`RenderItem`/`CameraRenderInfo` 已由 `SceneSnapshot` 取代；
> **proxy/payload 不再手写**——由 `Spark.Engine.SceneGen` 源生成器按 `[SceneProxy]`/`[ScenePayload]`
> 从 component 生成（见 §5.5），component 是唯一权威。

## 1. 背景与问题

当前帧数据通路只覆盖两类对象：

- `FrameData.Cameras`：`List<CameraRenderInfo>`（TargetId + 视图/投影 + 清屏色）
- `FrameData.RenderItems`：`List<RenderItem>`（MeshId + 世界矩阵）

`EngineApplication.FillFrameData` 每帧调 `World.CollectCameras` / `World.CollectRenderItems`
两次全量遍历场景图，临时拼出这两个列表。静态网格经 `ConcurrentQueue<StaticMesh>` 上传一次，
渲染线程在 `_meshes` 字典里按 `MeshId` 建 `MeshGPUResource`。

这套机制能撑住"清屏 + 三角形"，但无法平滑扩展到后续机制（光源、骨骼网格、实例化、粒子、贴花等）：

1. **无稳定身份**：`RenderItem` 每帧从组件临时构造，渲染侧没有一个跨帧持久的"场景对象"
   来挂接状态（骨骼姿态 buffer、阴影贴图槽位）。每加一类对象就要复制一整套
   "遍历 + 列表 + 队列 + 字典"。
2. **无包围盒**：`RenderItem` 只有矩阵，无法在渲染线程做视锥剔除。要剔除，逻辑侧必须先算
   bounds 并传过来。
3. **生命周期隐式**：对象"在/不在"列表即增删，没有显式的 create/destroy 信号；网格上传只有
   单向"建"，没有"删"（泄漏，ADR-7 未落地）。
4. **每类一套机制**：光源、骨骼网格若各自独立实现，会得到 N 条并行的同步通路，碎片化且
   线程安全分析要重复做 N 遍。

本设计要解决的核心问题：**让所有需要进入渲染线程的场景对象（相机、静态网格、骨骼网格、
光源、未来的实例化/粒子…）共用一套同步机制**——一条通道、一套身份与生命周期协议、一份
线程安全契约，同时把剔除所需的数据（bounds）随快照送达渲染线程。

## 2. 设计原则（在 ADR-1~ADR-7 上新增）

- **P7 一条通道，分类 payload**：所有场景对象共用 `SceneProxy → SceneSnapshot → RenderScene`
  单一通道；差异只在"分类 payload 结构 + 渲染侧消费者"。新增类别不改变同步机制本身。
- **P8 静态上传一次，动态每帧快照**：几何/纹理/材质等不可变数据走 upload-once 资源注册表；
  变换/包围盒/光源参数/骨骼姿态等可变数据走每帧快照。二者是同一通道的两个正交维度。
- **P9 稳定 ID + 生命周期 diff**：每个场景对象有全局单调 `ProxyId`；渲染侧用"快照 ID 集合 vs
  本地状态字典"的比对得出新增/存活/销毁，不依赖隐式时序。
- **P10 剔除归渲染线程**：逻辑线程提交完整（未剔除）对象集 + bounds，渲染线程按相机做视锥/
  遮挡剔除（未来可演进为 GPU-driven culling）。

## 3. 总体模型：三层架构

```
┌──────────────────── 逻辑线程 ────────────────────┐
│  World（Actor → Component）                       │
│    │ 组件在 BeginPlay/EndPlay 注册/注销            │
│    ▼                                              │
│  Scene（逻辑侧渲染场景，权威注册表）               │
│    └─ SceneProxy：StaticMeshProxy / LightProxy /   │
│       SkeletalMeshProxy / ...（稳定 ProxyId）      │
│            │ Capture() 每帧序列化                  │
│            ▼                                       │
│  SceneSnapshot（值快照：header 数组 + 分类 payload）│
└──────────────┼────────────────────────────────────┘
               ▼  DualFrameBuffer<SceneSnapshot>（双缓冲，超前≤1帧）
┌──────────────┴────────────────────────────────────┐
│  RenderScene（渲染线程镜像）                       │
│    ├─ 生命周期 diff：新增/存活/销毁（ADR-7 延迟删） │
│    ├─ GPU 资源注册表：Mesh/Skeleton/Material/...    │
│    ├─ 每对象状态：RenderProxyState（骨骼姿态等）    │
│    └─ 剔除 → DrawList → 光照/绘制                  │
└───────────────────────────────────────────────────┘
```

与 UE 的映射：`Scene` ≈ `FScene`，`SceneProxy` ≈ `FPrimitiveSceneProxy`，
`SceneSnapshot` ≈ `FSceneView` 之外的"场景基元提交"，`RenderScene` ≈ 渲染线程侧的
`FSceneRenderer` 输入。骨架同构，差异是早期简化（单一双缓冲、无命令流、无 RHI 线程）。

## 4. 逻辑侧：`Scene` + `SceneProxy`

`Scene` 是逻辑线程拥有的"渲染场景"注册表，从 World 的 Actor 图里解耦出来。任何要让渲染线程
看到的东西，都注册一个 `SceneProxy`。组件在 `BeginPlay` 注册、每帧更新 proxy 字段、
`EndPlay` 注销。

```csharp
// 逻辑侧：渲染场景注册表
public class Scene
{
    private int _nextProxyId;
    private readonly Dictionary<int, SceneProxy> _proxies = new();

    public T Register<T>(T proxy) where T : SceneProxy   // 分配 ProxyId 并登记
    {
        proxy.ProxyId = ++_nextProxyId;
        _proxies.Add(proxy.ProxyId, proxy);
        return proxy;
    }

    public void Unregister(int proxyId)                   // EndPlay 时调用
        => _proxies.Remove(proxyId);

    public void Capture(SceneSnapshot snapshot)           // 每帧序列化（逻辑线程独占）
    {
        foreach (var p in _proxies.Values)
            p.Capture(snapshot);
    }
}
```

`SceneProxy` 基类只承载渲染关心的状态（变换、包围球、可见性标记），不含任何逻辑数据：

```csharp
public abstract class SceneProxy : IDisposable
{
    public int ProxyId { get; internal set; }
    public Matrix4x4 WorldTransform;
    public BoundingSphere Bounds;                 // 世界空间包围球（剔除用）
    public VisibilityFlags Flags = VisibilityFlags.Visible;
    public bool IsDirty = true;                   // 增量路径：变更置位

    public abstract SceneCategory Category { get; }
    public abstract void Capture(SceneSnapshot snapshot);   // 写 header + 分类 payload
    public virtual void Dispose() { }
}

public enum SceneCategory : byte
{
    StaticMesh = 1,
    SkeletalMesh = 2,
    Light = 3,
    // 未来：InstancedMesh / ParticleSystem / Decal / ReflectionProbe ...
}

[Flags]
public enum VisibilityFlags : byte
{
    Visible = 1 << 0,
    CastShadow = 1 << 1,
    ReceiveShadow = 1 << 2,
    Occluder = 1 << 3,
}
```

组件侧集成（示例：`StaticMeshComponent`）：

```csharp
public class StaticMeshComponent : SceneComponent
{
    private StaticMeshProxy? _proxy;

    public StaticMesh? Mesh
    {
        get => _mesh;
        set { _mesh = value; _proxy = new StaticMeshProxy { MeshId = value.MeshId }; /* 世界/场景赋值 */ }
    }

    // 变换变化时同步：_proxy.WorldTransform = WorldTransform; _proxy.IsDirty = true;
    // BeginPlay：_scene.Register(_proxy)；EndPlay：_scene.Unregister(_proxy.ProxyId);
}
```

> 注：当前组件模型没有 BeginPlay/EndPlay 生命周期（只有 Actor 有），迁移时需先补组件生命周期，
> 或沿用"注册到 Scene 由 World 收集时惰性完成"的最小实现（见 §11 迁移步骤 2）。

## 5. 传输层：`SceneSnapshot`（值快照，ADR-1 扩展）

`SceneSnapshot` 替代 `FrameData` 里的 `Cameras` + `RenderItems`。全部是 blittable 值类型 +
资源 ID，不含任何指针或跨线程对象引用。缓冲用池化数组（每帧复用，只归零计数），避免每帧 GC。

```csharp
public sealed class SceneSnapshot
{
    public float DeltaTime;
    public uint FrameIndex;

    // 视图（消费者，驱动剔除与绘制）
    public FrameBuffer<CameraSnapshot> Cameras;

    // 场景对象：连续 header 数组（剔除友好的热数据）+ 分类 payload（SoA）
    public FrameBuffer<SceneObjectHeader> Objects;
    public FrameBuffer<StaticMeshPayload> StaticMeshes;
    public FrameBuffer<SkeletalMeshPayload> SkeletalMeshes;
    public FrameBuffer<LightPayload> Lights;
    // 未来：FrameBuffer<InstancedPayload> / FrameBuffer<ParticlePayload> / ...
}
```

### 5.1 通用 header（剔除所需的最小公共面）

```csharp
public struct SceneObjectHeader
{
    public int ProxyId;               // 稳定 ID：生命周期 + 渲染侧状态索引
    public SceneCategory Category;
    public Matrix4x4 WorldTransform;
    public BoundingSphere Bounds;     // 世界空间包围球
    public VisibilityFlags Flags;
    public int PayloadIndex;          // 指向本类别 payload 数组的紧凑下标
}
```

### 5.2 分类 payload

- **静态网格**：几何是上传一次的 GPU 资源，payload 只放资源 ID + 材质。

```csharp
public struct StaticMeshPayload
{
    public int MeshId;                // → 渲染侧网格 GPU 注册表
    public int MaterialId;            // → 材质注册表（P2）
}
```

- **光源**：全是动态参数，直接进快照。

```csharp
public struct LightPayload
{
    public LightType Type;            // Point / Directional / Spot
    public Vector3 Color;
    public float Intensity;
    public float Range;
    public float InnerConeAngle;
    public float OuterConeAngle;
    public int ShadowMapId;           // 0 = 不投影（渲染侧按需分配）
}
```

- **骨骼网格**：几何上传一次，姿态是每帧动态数据。

```csharp
public struct SkeletalMeshPayload
{
    public int MeshId;
    public int SkeletonId;            // → 骨骼 GPU 注册表（上传一次）
    public int BoneBufferId;          // 本帧皮肤矩阵 → 动态 buffer
    public int PrevBoneBufferId;      // 上一帧皮肤矩阵（运动模糊）
}
```

### 5.3 相机（视图）

相机是"消费场景对象"的一方，与场景对象分开，仍按现有思路（值快照 + 目标 ID）。顺带把硬编码
清屏色下沉为相机属性（当前 `EngineApplication.FillFrameData` 写死了 `0.10/0.15/0.25`）：

```csharp
public struct CameraSnapshot
{
    public int TargetId;
    public Matrix4x4 ViewMatrix;
    public Matrix4x4 ProjectionMatrix;
    public Vector4 ClearColor;        // 来自 CameraComponent.ClearColor
}
```

### 5.4 `FrameBuffer<T>`（池化数组）

```csharp
// 帧内复用、按线程独占访问，避免每帧分配；只归零 Count，不释放底层数组
public sealed class FrameBuffer<T> : IDisposable
{
    public T[] Items = [];
    public int Count;
    public void Add(in T item) { /* 扩容按需 */ }
    public void Clear() => Count = 0;
    public ReadOnlySpan<T> Span => Items.AsSpan(0, Count);
}
```

### 5.5 源生成器（SceneGen）：写 Component，生成 Proxy

草案中手写的 proxy/payload/Capture 样板，实际由 `Spark.Engine.SceneGen`（`IIncrementalGenerator`）
按 attribute 驱动生成。**语义手写、样板生成**的边界：

- 组件标记 `[SceneProxy(类别)]`，`[ScenePayload]` 字段/属性带默认值（**默认值只在组件**）；快照字段名
  由生成器从类别推导（Mesh 结尾 → +es，其余 → +s）；
- 生成器产出：proxy 子类（字段镜像 + 一行 `Capture`）、payload struct、组件的 partial（`_proxy` +
  生命周期 + `SyncProxy`）、`SceneSnapshot` 的分类 payload 字段与 `ClearPayloads`；
- `SceneSnapshot.AddObject<T>` 把「算 PayloadIndex → 写 payload → 写 header」收口成一行，供生成的
  `Capture` 复用；
- 每类专属语义经 `partial void OnProxyMapped(<Proxy> proxy)` 钩子手写（如 Bounds 规则），生成器声明、
  用户实现。

```csharp
[SceneProxy(SceneCategory.Light)]
public partial class LightComponent : SceneComponent
{
    [ScenePayload] public LightType Type { get; set; } = LightType.Point;   // 默认值只在此处
    [ScenePayload] public Vector3 Color { get; set; } = Vector3.One;
    // ...

    partial void OnProxyMapped(LightSceneProxy proxy)    // 唯一手写语义：Bounds 规则
        => proxy.Bounds = new BoundingSphere(WorldTransform.Translation,
            Type == LightType.Directional ? float.MaxValue : MathF.Max(Range, 0f));
}
```

## 6. 渲染侧：`RenderScene`

渲染线程拥有的镜像。持久部分跨帧保留，快照部分每帧覆盖：

```csharp
public class RenderScene
{
    // 持久：GPU 资源注册表（上传一次）
    public Dictionary<int, MeshGPUResource> Meshes;
    public Dictionary<int, SkeletonGPUResource> Skeletons;
    public Dictionary<int, MaterialGPUResource> Materials;

    // 持久：每对象渲染侧状态（骨骼姿态 buffer、阴影贴图槽等）
    public Dictionary<int, RenderProxyState> ProxyStates;

    // ADR-7 延迟删除队列
    private readonly Queue<RenderProxyState> _pendingDelete = new();

    public void ApplySnapshot(SceneSnapshot snapshot);      // 生命周期 diff + 动态数据上传
    public void Cull(CameraSnapshot cam, List<int> visible); // 视锥剔除
    public void CollectLights(CameraSnapshot cam, LightList lights); // 光源剔除/衰减
    public void FlushDeferredDelete();                      // 帧末批量释放
}
```

## 7. 身份与生命周期协议

`ApplySnapshot` 的核心是**集合 diff**（全量快照 + ID 比对），渲染侧据此得出三类信号：

```
1. snapshot.ProxyId 存在 && ProxyStates 不存在  → 新增：创建 RenderProxyState（懒建 GPU 资源）
2. 两边都存在                                  → 存活：更新动态数据（姿态/变换/光源参数）
3. ProxyStates 存在 && snapshot.ProxyId 不存在 → 销毁：移入 _pendingDelete
```

帧末 `FlushDeferredDelete` 统一释放——这就是 ADR-7 延迟删除的落地，同时消除当前
`WindowManager.RemoveWindow` 里 `_targets.Remove` 与 `window.Uninitialize` 之间的竞态窗口。

- **v1（全量快照）**：每帧携带完整活跃对象集，diff 由渲染侧做。简单、正确，与现状一致。
- **v2（增量，P1-3）**：`IsDirty` 置位的 proxy 才重写 header/payload，快照只带
  `Added/Changed/Removed` 三个 delta 段，渲染侧用持久状态补全。带宽与 CPU 随对象数增长而摊薄。

推荐从 v1 起步（与 worklog "全量快照是 v1 合理形态，下一步 dirty 增量更新" 一致），v2 作为
既定升级路径写入本文档。

## 8. 线程安全契约（本设计的核心交付）

| 对象 | 归属线程 | 访问规则 |
|---|---|---|
| `Scene` / `SceneProxy` | 逻辑线程 | 仅逻辑线程读写；渲染线程**永不触碰** |
| `SceneSnapshot`（双缓冲两槽） | 各自独立 | 逻辑线程独占"空槽"填充，渲染线程独占"就绪槽"读取；`DualFrameBuffer` 保证互不重叠 |
| `RenderScene.ProxyStates` | 渲染线程 | 仅渲染线程读写 |
| GPU 资源注册表（Meshes/Skeletons/…） | 渲染线程 | 逻辑线程经单向命令队列（同现有 `PendingMeshUploads`）请求创建 |
| 资源销毁 | 渲染线程 | 逻辑线程只发"注销"信号，渲染线程帧末延迟释放（ADR-7） |

不变式：

1. **值快照 + 资源 ID**：快照里只有 blittable struct 与 ID，无指针、无对象引用、无 GPU 句柄。
2. **单写者双缓冲**：沿用 `DualFrameBuffer<T>`，逻辑线程最多超前 1 帧，每个槽任意时刻只被一个
   线程触碰，池化复用因此安全。
3. **所有权单向**：GPU 资源与渲染侧状态归渲染线程；逻辑线程通过 ID 间接引用。这延续 ADR-2/5
   的"所有权单向"原则到所有场景对象。
4. **销毁单向**：create/destroy 都是单向信号，destroy 的物理释放延迟到帧末安全点，杜绝渲染线程
   读已释放资源。

## 9. 渲染线程剔除流水线

有了完整 header 数组（含 bounds）后，剔除在渲染线程完成：

```csharp
foreach (var cam in snapshot.Cameras)
{
    var frustum = Frustum.FromViewProj(cam.ViewMatrix * cam.ProjectionMatrix);

    var visible = _drawListPool.Rent();          // 池化，避免每帧分配
    foreach (ref readonly var obj in snapshot.Objects.Span)
    {
        if ((obj.Flags & VisibilityFlags.Visible) == 0)
            continue;
        if (obj.Bounds.Intersects(frustum))       // 球-视锥粗剔除
            visible.Add(obj.ProxyId);
    }

    // 排序：按 Category → MaterialId → MeshId 减少状态切换与 overdraw
    // 生成 draw calls：静态网格读 StaticMeshes[PayloadIndex]；骨骼网格先蒙皮再画
    // 光源：CollectLights 对同一 frustum 剔除，写入 per-camera light buffer
}
```

后续演进空间（本设计预留、不实现）：AABB/遮挡剔除、BVH/八叉树加速结构（P2-7）、GPU-driven
culling、光源 tile-based/deferred 分桶。

## 10. 各类别接入方式（"同一套机制"的演示）

| 类别 | 静态数据（上传一次） | 动态数据（每帧快照） | 渲染侧消费者 |
|---|---|---|---|
| 静态网格 | 顶点/索引/材质 | 变换 + bounds（header） | 剔除后直接画 |
| 骨骼网格 | 网格 + 骨架绑定 | 变换 + bounds + 皮肤矩阵 buffer | 剔除 → 蒙皮 → 画 |
| 光源 | （阴影贴图槽按需） | 类型/颜色/强度/范围/锥角 | 剔除 + 衰减 → light buffer |
| 实例化 | 网格 + 材质 | 每实例变换数组 | 剔除后合批 instanced draw |
| 粒子 | 发射器资源 | 每粒子实例 buffer | 剔除发射器 → 更新/绘制 |

要点：**新增一个类别 = 加一个 `SceneProxy` 子类 + 一个 payload struct + 一个渲染侧消费者**，
同步机制、身份协议、线程契约、剔除循环全部复用，不改一行传输层代码。

## 11. 迁移路径（从当前代码，逐步且每步不回归三角形渲染）

- **步骤 1（最小改造）**：引入 `SceneSnapshot` + `SceneObjectHeader` + `BoundingSphere` +
  `FrameBuffer<T>`；`World` 改为生成快照（含 bounds），替换 `RenderItems`；`RenderItem` 的
  `MeshId` 语义并入 `StaticMeshPayload`；相机改 `CameraSnapshot` 并把清屏色下沉到相机。
  渲染线程暂时不做剔除，仅按 header 遍历画网格。→ 得到统一结构 + 稳定 ID + bounds。
- **步骤 2（生命周期落地）**：引入 `Scene` + `SceneProxy`，组件注册/更新/注销；补组件
  BeginPlay/EndPlay 生命周期（或惰性注册）；`RenderScene` 持有 `ProxyStates` 并做集合 diff；
  落地 ADR-7 延迟删除，替换 `RenderTargetRegistry` 的直接 `Remove`。→ 资源可正确销毁。
- **步骤 3（增量 + 扩展）**：`IsDirty` 增量快照（P1-3）；接入 `LightProxy`（光照 pass）与
  `SkeletalMeshProxy`（蒙皮）；渲染线程剔除正式启用。→ 达到本设计目标形态。

## 12. 决策记录（ADR，续 RenderPipeline-Design.md §12）

| ID | 决策 | 备选 | 理由 |
|---|---|---|---|
| ADR-8 | 所有场景对象共用 `SceneProxy → SceneSnapshot → RenderScene` 单通道，差异仅在分类 payload + 渲染侧消费者 | 每类对象独立一套列表/队列/注册表 | 一条线程契约、一套身份与生命周期协议，新增类别不复制机制 |
| ADR-9 | 静态数据 upload-once 资源注册表，动态数据每帧值快照 | 每帧重传全部（含几何） | 几何/纹理带宽与 GPU 重建成本不可接受；分离后快照只含易变字段 |
| ADR-10 | 场景对象用稳定 `ProxyId` + 集合 diff 表达新增/存活/销毁 | 依赖对象在列表中的出现/消失隐式表达 | 渲染侧需要持久状态（骨骼姿态、阴影贴图槽），必须显式 create/destroy 信号 |
| ADR-11 | 剔除归渲染线程：逻辑提交完整对象集 + bounds，渲染线程按相机剔除 | 逻辑线程剔除后提交 | 剔除与逻辑 tick 解耦，便于未来遮挡/GPU-driven；bounds 随快照一次送达 |

## 13. 未决事项

- header/payload 内存布局：已落地为「连续 header 数组 + 分类 payload 数组」（本文 SoA 方案，
  `FrameBuffer<T>` 池化）；规模上来后可再按 Category/Material 重排 header 提升缓存命中；
- 剔除加速结构（BVH/八叉树）与粗剔除精度（球 vs AABB）的取舍（P2-7）——当前为球-视锥；
- 动态 buffer（骨骼皮肤矩阵、实例变换）的分配策略：ring buffer 双缓冲 vs 每对象独立 buffer；
- 光源前向 vs deferred、是否 tile-based 分桶——当前仅数据通路 + 剔除，shading 未实现；
- 快照带宽上限与增量切换阈值（v1 全量快照 → v2 dirty 增量，对应 P1-3）。
