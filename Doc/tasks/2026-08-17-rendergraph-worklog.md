# 任务工作记录（Worklog）

> 记录「RenderGraph 声明式多 pass 编排」的实现与调试会话（ShadowDepth → Forward 两 pass 落地）。
> 未提交（工作区变更）：`ForwardRenderer` 重构 + `Src/Spark.Engine/Render/RenderGraph/` 新增。

## 概述

把 `ForwardRenderer` 从「命令式按顺序执行」（手写 `RenderShadowMap` / `DrawView`）升级为「声明式
依赖图」（`RenderGraph`），阴影贴图与完整着色收敛为两个声明式 pass：`ShadowDepthPass`（写 transient
深度贴图）→ `ForwardPass`（采样阴影贴图 → 写 backbuffer）。图的编译负责依赖边、拓扑排序、环检测与
简化剔除；执行负责 transient 资源的帧内分配/释放。

期间排查并修复了 wgpu 的 draw-time validation 崩溃（`Incompatible bind group at index 2`），
涉及显式 PipelineLayout 的 bind group 完整性与生命周期。

## 阶段 1：RenderGraph 核心

- 新增 `Render/RenderGraph/RenderGraph.cs`：`RegisterTexture`/`ImportTexture`/`AddPass`/`Compile`/`Execute`/
  `Reset`/`Dispose`；Compile 建依赖边 → Kahn 拓扑排序 → 环检测 → 简化剔除；Execute 分配 transient →
  按拓扑序执行 → 帧末释放
- 新增 `RenderPass.cs`（name + setup/execute 委托，收集读写声明）、`RenderPassBuilder.cs`、
  `RenderGraphContext.cs`（句柄 → 真实 GPU 对象）、`RenderGraphResource.cs`、`TextureResource.cs`、
  `TextureResourceDesc.cs`、`ResourceAccess.cs`、`TransientResourcePool.cs`（Phase B：每帧新建/释放，无别名）
- `ForwardRenderer` 重构：删除手写的 `RenderShadowMap`/`DrawView`/`EnsureDepthTarget` 等，改为每帧建图 +
  `ShadowDepthPass`/`ForwardPass` 两个复用 pass 实例

## 阶段 2：ShadowDepthPass / ForwardPass 拆分

- `ShadowDepthPass`：`FrameUniforms.view_proj = 光源 VP`，深度-only pass（无颜色附件），写入 transient 深度贴图；
  自身 group0 用 1×1 占位深度纹理（避免同 pass 边写边采样）
- `ForwardPass`：挂视口尺寸深度缓冲 + 采样阴影贴图（`textureSampleCompare`）→ 完整着色 → 写 backbuffer

## 阶段 3：wgpu bind group 崩溃定位与修复

报错（报错点在 `RenderPassEncoderEnd`，实为延迟校验）：

```text
Incompatible bind group at index 2 in the current render pipeline
Assigned bind group layout not found (internal error)
```

修复要点：

1. **显式 PipelineLayout 的 bind group 完整性**（阴影 pass）：draw 前为管线实际用到的每个组都 set 一个
   兼容 bind group；材质缺失时回退引擎默认材质，并始终 set group1/2/3——masked 变体仍会经 group3 采样
   遮罩纹理，统一绑定四组才能对所有变体与显式布局兼容（详见 [ShadowMapping-Design.md §4.1](../ShadowMapping-Design.md)）。
2. **窗口 backbuffer 是 external 资源，须走 `BeginRenderSession()`**（acquire/present），不能走
   `GetTextureView()`（后者只对离屏 `TextureRenderTarget` 有效，对 `Viewport` 抛异常）。
3. **transient 资源 + 缓存的 bind group = 悬垂视图**：阴影贴图每帧新建，forward pass 的 group0 bind group
   必须随阴影贴图每帧重建，否则引用已释放的旧视图。
4. **bind group 类型正确**：无阴影时 group0 的 depth 槽绑 1×1 占位深度纹理、comparison 槽绑比较采样器，
   不能用颜色纹理/过滤采样器顶替。
5. **group0 按有无阴影分流**：有阴影用含阴影贴图的 group0，无阴影用占位 group0，避免 set null bind group。

## 阶段 4：Demo 资源路径修正

- 砖墙贴图从 `Downloads` 绝对路径移入 `Demo/Demo.Desktop/Assets/`，csproj 增加 `Content` + 复制到输出目录
- `Program.cs` 改为 `Path.Combine(AppContext.BaseDirectory, "Assets", fileName)` 加载

## 遗留待办

- **运行时验证**：需本地 GPU 环境（本会话在沙箱内无法跑通 `dotnet build`——命名管道被禁，MSBuild/Roslyn 报
  `MSB3883 Access to \\.\pipe\LOCAL\dotnet_... denied`，非代码问题）
- **多相机 / 多视口**：每个 forward pass 各自 acquire/present，多相机写同一 backbuffer 需收口为每帧 acquire 一次
- **别名复用（Phase C）** 与 **barrier / pass 级剔除（Phase D）**：当前剔除是简化版
