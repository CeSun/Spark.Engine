# Spark.Engine 阴影贴图设计

> 状态：已实现（前向渲染器内的单阴影贴图）。本文记录实现结构与**本次调试踩坑经验**，
> 作为后续多阴影/软阴影/级联阴影的基础。
> 关联代码：`Src/Spark.Engine/Render/Pipeline/Forward/ForwardRenderer.cs`、`MaterialShaderCache.cs`、
> `ShaderPass.cs`、`Shaders/ForwardShadeLit.wgsl`、`Shaders/ForwardDepthFragment.wgsl`、
> `Src/Spark.Engine/Render/Pipeline/TextureRenderTarget.cs`。

## 1. 目标与范围

- 前向渲染器内实现**单阴影贴图**：每帧找第一个 `CastShadow` 的聚光/平行光，渲一张深度贴图，
  前向 pass 里采样它做阴影遮挡。
- 多 pass 能力承接 [MaterialSystem-Design.md §7.1](./MaterialSystem-Design.md) 的 `ShaderPass`
  （Forward/ShadowDepth/DepthOnly，ADR-22）。
- 当前限制：单阴影贴图、硬编码 1024×1024、硬编码 bias、点光不投影（需 cube map）。

## 2. 实现结构

```
Render(SceneSnapshot)
  ├─ ComputeShadowInfo：找第一个 CastShadow 的聚光/平行光，算 light view-proj
  ├─ RenderShadowMap：深度-only pass，把 CastShadow 网格渲进 1024×1024 Depth24Plus 贴图
  └─ 前向 pass：挂深度缓冲（视口尺寸）+ 采样阴影贴图（textureSampleCompare）
```

关键点：

| 组件 | 职责 |
|---|---|
| `TextureRenderTarget`（isDepth=true） | 离屏深度目标（阴影贴图 + 前向深度缓冲共用此抽象） |
| `RenderShadowMap` | 阴影 pass：`FrameUniforms.view_proj = 光源 VP`，`ShaderPass.ShadowDepth`，深度附件 Clear 1.0 |
| `FrameUniforms` | 增 `shadow_view_proj`（光源 VP）+ `shadow_light`（lights 数组下标，0xFFFFFFFF=无阴影） |
| group0 布局 | binding0 帧 uniform + binding1 阴影贴图（`texture_depth_2d`）+ binding2 比较采样器（`Compare=Less`） |
| `shade_lit` | 对 `i == shadow_light` 的光源采样阴影：`shadow = textureSampleCompare(...)`，乘进光照 |
| `StaticMeshComponent.CastShadow` | 写进 header 的 `Visibility.CastShadow`，阴影 pass 据此收集 caster |
| 前向深度缓冲 | `EnsureDepthTarget` 按视口尺寸懒建/重建，深度测试 `Less`（保证近处三角形盖住远处墙） |

## 3. 深度约定与阴影比较

- **System.Numerics `CreatePerspectiveFieldOfView` 是标准深度（near→0, far→1，非 reverse-Z）**。
  已验证：near=0.1/far=20 时，z=-2 → ndc.z≈0.955、z=-4 → ndc.z≈0.980（近处更小）。
- WGSL `textureSampleCompare(t, s, coords, depth_ref)` 返回 `compare(sampled_depth, depth_ref)`。
- **标准前向阴影公式**应为 `light = 1.0 - textureSampleCompare(map, samp, uv, fragDepth - bias)`（`Compare=Less`）。
- **本实现踩坑**：标准公式在本实现里得到**反相**（阴影区亮、非阴影区暗）。经验修正为
  `light = textureSampleCompare(map, samp, uv, fragDepth + bias)`（翻转比较结果与 bias 符号）后阴影区变暗。
  该方向与标准公式相反，说明本实现里比较结果的实际语义与预期相反（比较函数方向或深度取值需注意）；
  后续若换后端/改深度约定，需重新验证此方向。

## 4. 踩坑记录 / 经验教训（本次调试，按发现顺序）

1. **前向 pass 必须挂深度缓冲**。没有深度缓冲时，可见性完全由 draw order 决定，后画的墙（z=-4）
   把前面的三角形（z=-2）盖住——这是「颜色不对 / 看不到三角形」的根因，不是材质/纹理的问题。
2. **深度附件尺寸必须等于颜色附件尺寸**。WebGPU 校验：`Attachments have differing sizes`。
   深度缓冲要按视口尺寸（`RenderTarget.Width/Height`）懒建，尺寸变化时重建，不能写死。
3. **`DepthStencilState` 的 stencil 比较函数不能是 `Undefined`**。wgpu panic：
   `invalid compare function for front stencil face state`。禁用 stencil 也要给有效值
   （`Compare=Always` + `FailOp/DepthFailOp/PassOp=Keep`，读写掩码默认 0）。
4. **同一纹理不能在一个 pass 里既作深度附件写、又在 bind group 里采样**。WebGPU 校验：
   `conflicting usages: RESOURCE vs DEPTH_STENCIL_WRITE`。阴影 pass 写阴影贴图时，group0 的
   binding1 不能绑阴影贴图本身 → 用 1×1 占位深度纹理建一个**独立的阴影 pass group0**。
5. **Silk.NET `TextureFormat` 深度格式命名**：`Depth32float`（小写 f）/ `Depth24Plus`，不是
   `Depth32Float`（大写 F）。查枚举成员别靠猜，用反射或直接 build 报错定位。
6. **光源与相机同位置 → 阴影投在 caster 正后方被挡住**。demo 构图要把光源错开相机（本例光源移到
   x=0.5），否则阴影永远看不见、误以为是「没有阴影」。
7. **调试方法**：WebGPU 校验错误逐条修（每条都精确指向问题）；`ParamDebug` 日志打材质参数/ShadingModel/
   TextureFlags 确认材质正确；`ShaderDump` 直接 dump 生成的 WGSL 确认纹理采样与 shade_lit 都在。
   这三步能快速把「视觉不对」收敛到「哪一层不对」（材质参数 / shader 生成 / 深度 / 阴影比较）。

## 5. 现状与后续

| 项目 | 现状 |
|---|---|
| 阴影贴图 | ✅ 单张 1024×1024 Depth24Plus |
| 阴影 pass + 前向采样 | ✅ |
| 前向深度缓冲 | ✅（按视口尺寸懒建） |
| bias | 硬编码 +0.002 |
| PCF 软阴影 | ⏳（比较采样器已用 Linear 过滤，硬件 PCF 视设备） |
| 多阴影贴图 / 点光 cube map | ⏳ |
| 方向光紧致正交包围盒 | ⏳（当前固定 40×40 正交） |
| 阴影精度 / 级联 | ⏳ |

---

### 与现有文档的关系

- 本文是 [MaterialSystem-Design.md §7.1](./MaterialSystem-Design.md) 多 pass（`ShaderPass`，ADR-22）的
  渲染侧落地：阴影 pass 消费 `ShaderPass.ShadowDepth`，前向 pass 消费 `ShaderPass.Forward`。
- `TextureRenderTarget` 对应 [RenderGraph-Design.md](./RenderGraph-Design.md) 阶段 A 的前置（离屏目标），
  阴影的两 pass（ShadowDepth → Forward 采样）正是 RenderGraph 文档 §4.1 的最小例子。
