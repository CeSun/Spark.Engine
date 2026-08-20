# UIRenderView 渲染视图控件设计（Render View Control）

> 状态：已实现（编译 + 冒烟测试验证：离屏场景 pass 与 UI pass 采样依赖正确、自适应重建生效）。
> 用途：在保留模式 UI 中实时显示引擎渲染画面（离屏相机视角 / 编辑器视口 / 小地图等）。

## 概述

`UIRenderView` 是一个 `UIElement` 控件，把一个**离屏渲染目标**（`TextureRenderTarget`）的内容
显示到 UI 画布中。渲染链路：

```
相机（CameraComponent.RenderTarget = 离屏目标）
   │  每帧快照（CameraSnapshot.TargetId = 离屏目标 Id）
   ▼
BlinnPhongRenderer.BuildGraph ──写──▶ TextureRenderTarget（external 资源）
                                           │  UI pass 经 Read() 声明采样依赖
                                           ▼
UIRenderer（UI overlay pass）──采样──▶ UIRenderView 显示区域
```

要点：逻辑线程与渲染线程之间**只传 ID**（`UIPrimitive.TextureId = -renderViewId`），不跨线程传递
GPU 指针；渲染线程在 `GetBindGroup` 处从 `RenderTargetRegistry` 解析真实纹理视图。

## 组件清单

| 类型 | 文件 | 职责 |
|---|---|---|
| `UIRenderView` | `Spark.Engine/UI/UIRenderView.cs` | 控件：显示区域 + 自适应分辨率 + 宽高比保持 |
| `UIManager.RegisterRenderView` 等 | `Spark.Engine/UI/UIManager.cs` | 渲染视图注册表（逻辑线程布局用） |
| `UIManager.DrawRenderView` | 同上 | 发出 `TextureId = -renderViewId` 的基元 |
| `EngineApplication.CreateRenderView` / `DestroyRenderView` | `Spark.Engine/EngineApplication.cs` | 离屏目标创建/销毁便捷方法 |
| `UIRenderer`（负 ID 分支） | `Spark.Engine/Render/UI/UIRenderer.cs` | 渲染线程侧：解析渲染视图 → bind group 缓存 + 图依赖声明 |

## 关键设计

### 1. ID 间接引用（跨线程安全）

- `TextureRenderTarget` 是渲染线程对象；`UIRenderView` 只持有 `RenderViewId`（= 目标的 `TargetId`）。
- 基元用**负 TextureId** 编码渲染视图（正 ID = 已上传 UI 纹理，0 = 内置白纹理），互不冲突。
- 渲染线程 `GetBindGroup(textureId < 0)` 时从 `RenderTargetRegistry` 查 `TextureRenderTarget`，
  取其 `View` 建 bind group（`_renderViewBindGroups` 缓存）；目标被移除后自动释放缓存回退白纹理。

### 2. 渲染依赖（RenderGraph Read 声明）

UI overlay pass 声明 `builder.Read(renderViewResource, ResourceAccess.Sample)`，编译时建边
「写该离屏目标的场景 pass → UI pass」，确保采样发生在写入之后。依赖**按目标精确声明**：
只有实际引用了该渲染视图的窗口 UI pass 才声明读取（`_targetRenderViews` 按 TargetId 分组）。

### 3. 自适应分辨率（消除放大模糊）

离屏目标分辨率固定时，若显示区域大于目标分辨率会产生放大模糊（线性上采样）。`UIRenderView`
默认开启 `AutoResize`：

- 每帧 `OnPaint` 计算期望分辨率 = `Bounds × ResolutionScale`（超采样倍率，默认 1；>1 更锐利）。
- 与当前实际目标尺寸差异超过 `ResizeThreshold`（默认 8px，防抖）时，调用
  `RenderViewResizeRequested(oldId, w, h)` 回调。
- 回调由使用者实现（Demo 中）：`app.CreateRenderView` 建新目标 → 更新相机 `RenderTarget` →
  `app.DestroyRenderView` 延迟释放旧目标（ADR-7）→ 返回新 Id。
- 时序：`OnPaint` 在 `FillFrameData` 的相机收集之前，重建后**当帧**即生效。

### 4. 宽高比保持

`MaintainAspectRatio`（默认 true）：按离屏目标宽高比在显示区域内居中 letterbox，避免拉伸变形。
自适应分辨率开启后，离屏目标宽高比 ≈ 显示区域宽高比，居中偏移趋近于零。

### 5. 生命周期

`CreateRenderView` = 分配 Id + 建 `TextureRenderTarget`（RGBA8，可作颜色附件 + 纹理采样）+
注册 `RenderTargetRegistry` + 注册 `UIManager`。
`DestroyRenderView` = 注销 `UIManager` + `RenderTargets.Remove`（入延迟删除队列，渲染线程帧末释放）。
重建（Resize 回调）即「创建新 + 延迟销毁旧」，复用同一套机制。

## 使用方式

```csharp
// 1. 创建离屏渲染视图（已注册）
var renderView = app.CreateRenderView(320, 240);

// 2. 相机渲染到离屏目标
var camera = AddCamera(world, renderView, eye: ..., lookAt: ...); // CameraComponent.RenderTarget = renderView

// 3. 控件（可开启自适应分辨率）
var view = new UIRenderView { RenderViewId = renderView.Id, ResolutionScale = 1.5f };
view.RenderViewResizeRequested = (oldId, w, h) =>
{
    var next = app.CreateRenderView(w, h);
    camera.RenderTarget = next;
    app.DestroyRenderView(oldId);
    return next.Id;
};

// 4. 挂到 UI 树
canvas.Root = someDockPanel; someDockPanel.AddChild(view);
```

## 验证

- 冒烟测试（本地 GPU）：程序运行稳定；首帧 RenderGraph dump 确认
  `BlinnPhong(Target=3)`（离屏场景 pass）→ `UIOverlay(Target=4)` 读 `res_3`（采样依赖正确）；
  自适应重建后目标 Id 更新为新 `TextureRenderTarget`，UI pass 读取新目标。
- 已知限制：UI 文本渲染器依赖系统字体（Arial/Segoe UI 等），**不支持中文**，UI 文本一律用英文。

## 未决事项

- 采样器当前共享 UI 全局 Linear 采样器；超采样缩小显示时无 mipmap，可考虑为渲染视图建独立采样器。
- `RenderTargetRegistry` 目标重建（同 Id 原地 resize）可省去每次重建的 Id 抖动，留待后续。
