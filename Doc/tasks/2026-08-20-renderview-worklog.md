# 任务工作记录（Worklog）

> 记录「UIRenderView 引擎画面显示控件」的实现会话。
> 功能：在保留模式 UI 中实时显示离屏相机渲染画面（TextureRenderTarget → UI 采样）。
> 详设见 [UIRenderView-Design.md](../UIRenderView-Design.md)。

## 概述

在保留模式 UI 系统中实现一个可显示引擎画面的控件：相机渲染到离屏 `TextureRenderTarget`，
UI 控件经负 TextureId 间接引用该目标，渲染线程 UI overlay pass 采样其纹理视图显示。
核心解决三个问题：跨线程 ID 引用、RenderGraph 采样依赖声明、自适应分辨率消除放大模糊。

## 阶段 1：控件 + 逻辑侧支持

- `UIRenderView`（`Spark.Engine/UI/UIRenderView.cs`）：`RenderViewId` + 背景 + 宽高比保持 +
  布局填充语义（`ClipToBounds` 默认开启）。
- `UIManager`：渲染视图注册表（`RegisterRenderView`/`UnregisterRenderView`/`GetRenderViewSize`，
  ConcurrentDictionary）+ `DrawRenderView`（发出 `TextureId = -renderViewId` 基元，负值编码）。
- `EngineApplication.CreateRenderView`/`DestroyRenderView`：离屏目标创建/注册/延迟销毁便捷方法。

## 阶段 2：渲染线程侧

- `UIRenderer.GetBindGroup` 负 ID 分支：从 `RenderTargetRegistry` 取 `TextureRenderTarget` 建 bind
  group（缓存 + 目标移除时释放缓存回退白纹理）。
- `AppendToGraph`：收集本帧引用的渲染视图 → `graph.ImportTexture` 导入 external 资源 →
  各 UI pass 对**实际引用**的渲染视图声明 `builder.Read(resource, Sample)`，保证在写该离屏目标的
  场景 pass 之后执行（初版对所有 UI pass 声明读取，后改为按 TargetId 精确声明，见阶段 4）。
- `Dispose` 清理渲染视图 bind group 缓存。

## 阶段 3：画面模糊定位与修复（自适应分辨率）

现象：离屏 320×240 被放大显示到约 464×352 的 UI 区域（1.45× 上采样）+ 线性过滤 → 画面糊。

修复：`UIRenderView` 增加 `AutoResize`（默认开）+ `ResolutionScale`（超采样）+ `ResizeThreshold`
（防抖）——每帧 `OnPaint` 对比期望分辨率与实际目标尺寸，差异超阈值时经
`RenderViewResizeRequested` 回调重建（新目标 + 更新相机 + 延迟销毁旧目标）。
时序利用 `FillFrameData` 中 canvas.Paint 先于相机收集，重建**当帧生效**。

## 阶段 4：依赖声明细化

初版让所有 UI pass 读取所有渲染视图（冗余依赖）；改为 `_targetRenderViews` 按 TargetId 分组，
仅实际引用该渲染视图的 UI pass 声明读取（冒烟测试 dump 确认：只有第三窗口 UI pass 读离屏目标）。

## 阶段 5：UI 文本英文化

UI 文本渲染器依赖系统字体（Arial/Segoe UI 等），不支持中文（显示为方块）；将演示与窗口标题的
中文改为英文（控件/引擎核心无用户可见中文文本）。

## 验证

- `dotnet build` 全量编译通过（0 错）。
- 冒烟测试（本地 GPU，`dotnet run --project Demo/Demo.Desktop` 运行 10s 无崩溃）：
  - 首帧 RenderGraph dump：`BlinnPhong(Target=3)` 写离屏目标 → `UIOverlay(Target=4)` 读 `res_3`，
    采样依赖边正确。
  - 自适应重建后目标 Id 更新（`TextureRenderTarget(5)`），UI pass 读取新目标，链路无断裂。

## 遗留待办

- 渲染视图独立采样器（超采样缩小显示时无 mipmap，可建专用采样器）。
- `RenderTargetRegistry` 原地 resize（同 Id 重建）省去 Id 抖动。
