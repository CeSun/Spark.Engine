# Spark.Engine 阴影贴图设计

> 状态：已实现（前向渲染器内的单阴影贴图）。本文记录实现结构与**本次调试踩坑经验**，
> 作为后续多阴影/软阴影/级联阴影的基础。
> 关联代码：`Src/Spark.Engine/Render/Pipeline/BlinnPhong/BlinnPhongRenderer.cs`、`MaterialShaderCache.cs`、
> `ShaderPass.cs`、`Shaders/ForwardShadeLit.wgsl`、`Shaders/ForwardDepthFragment.wgsl`、
> `Src/Spark.Engine/Render/Common/TextureRenderTarget.cs`、
> `Src/Spark.Engine/Render/Pipeline/BlinnPhong/Stages/ShadowDepthStage.cs`。

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
  ├─ ShadowDepthStage：向 RenderGraph 声明并执行深度-only pass
  │    └─ 把 CastShadow 网格渲进 1024×1024 Depth24Plus 贴图
  └─ 前向 pass：挂深度缓冲（视口尺寸）+ 采样阴影贴图（textureSampleCompare）
```

关键点：

| 组件 | 职责 |
|---|---|
| `TextureRenderTarget`（isDepth=true） | 离屏深度目标（阴影贴图 + 前向深度缓冲共用此抽象） |
| `ShadowDepthStage` | 阴影 stage：`FrameUniforms.view_proj = 光源 VP`，`ShaderPass.ShadowDepth`，深度附件 Clear 1.0 |
| `FrameUniforms` | 增 `shadow_view_proj`（光源 VP）+ `shadow_light`（lights 数组下标，0xFFFFFFFF=无阴影） |
| group0 布局 | binding0 帧 uniform + binding1 阴影贴图（`texture_depth_2d`）+ binding2 比较采样器（`Compare=Less`） |
| group1/2/3 | 每次阴影 draw 绑定对象、材质参数、材质纹理；无材质时使用引擎默认材质 |
| `shade_lit` | 对 `i == shadow_light` 的光源采样阴影：`shadow = textureSampleCompare(...)`，乘进光照 |
| `StaticMeshComponent.CastShadow` | 写进 header 的 `Visibility.CastShadow`，阴影 pass 据此收集 caster |
| 前向深度缓冲 | `EnsureDepthTarget` 按视口尺寸懒建/重建，深度测试 `Less`（保证近处三角形盖住远处墙） |

## 3. 深度约定与阴影比较（已定位）

- **深度是标准约定（near→0, far→1，非 reverse-Z）**。用实际矩阵验证（`view×proj` 作用后）：
  世界 z=-2 → ndc.z≈0.955、z=-4 → ndc.z≈0.980（近处更小、远处更大）。
- **比较方向踩坑（已定位）**：WGSL 规范对 `textureSampleCompare` 的比较方向曾定义不清
  （[gpuweb/gpuweb#5285](https://github.com/gpuweb/gpuweb/issues/5285)）。实际 wgpu 的 `Compare=Less`
  返回 `depth_ref < sampled_depth`，与常见教材的 `sampled_depth < depth_ref` **相反**。
- **因此本实现的正确阴影公式是**（不能照抄教材的 `1 - textureSampleCompare`）：
  `shadow = textureSampleCompare(shadow_map, shadow_samp, suv, ndc.z - bias)`，
  bias 取**负**（减）——在 `depth_ref < sampled` 语义下，减 bias 让「自身表面」被判为受光、防止自阴影（acne）。
  该公式里 `shadow` 直接就是光照因子（1=受光、0=被挡），所以后续换后端/改深度约定时需重新验证此方向。

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
### 4.1 显式 PipelineLayout 的 bind group 完整性

本管线固定使用四组布局：group0 帧、group1 对象、group2 材质参数、group3 材质纹理。
`ShadowDepthStage` 曾只设置 group0/1，在 `wgpuRenderPassEncoderEnd` 触发：

```text
Incompatible bind group at index 2 in the current render pipeline
Assigned bind group layout not found (internal error)
```

`RenderPassEncoderEnd` 只是延迟校验的报错点，真正缺陷在此前的 draw 状态组装；随后出现的
`panic in a function that cannot unwind` 是 Rust panic 穿过 C ABI 后的次生 abort，不是另一个根因。
修复规则是：阴影绘制解析实际 `MaterialGPUResource`，缺失时回退默认材质，并在 draw 前始终设置
group1/2/3。即使普通深度 shader 没有读取材质参数，masked 变体仍会通过 group3 采样遮罩纹理；
统一绑定四组可以保持所有材质变体与显式布局兼容。

排查同类错误时按以下顺序进行：先把日志中的 `index N` 映射到固定组职责，再比较
`DeviceCreatePipelineLayout`、`DeviceCreateBindGroup` 和 draw 前 `SetBindGroup` 使用的布局对象及索引，
最后检查 BindGroup 是否被提前释放。修复后至少执行一次包含投影光源和 caster 的运行验证；仅 build
通过不能覆盖 WebGPU 的 draw-time validation。

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
