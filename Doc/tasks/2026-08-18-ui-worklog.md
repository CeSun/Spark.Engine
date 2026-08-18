# 任务工作记录（Worklog）

> 记录「保留模式 UI 系统」的实现与调试会话（P0~P5 落地 + 文字渲染问题定位）。
> 提交：`f8639e9`（实现）+ `c44a993`/`d50243d`/`93f4fb1`/`f185ff2`（修复与打磨）。

## 概述

从零实现一套与 3D 场景解耦的保留模式 UI 系统：输入抽象 → 控件树布局/命中测试 → 每帧扁平
`UIPrimitive` → 经 `IGraphOverlay` 挂到 RenderGraph，在场景 pass 之后叠加绘制到同一 backbuffer。
文本采用「字符串级纹理」v1（SixLabors 栅格化整段文本），不是字形图集。

期间文字渲染在本地观察到「拉伸/错位」，最终定位为**渲染线程顶点的双重偏移叠加**（见阶段 4），
连同 bytesPerRow 对齐、批次覆盖、文本裁剪，累计修复四处渲染/栅格化问题。

## 阶段 1：输入系统（P0）

- `Key`/`KeyMask`（85 键子集 + 128 位掩码）、`MouseButton`/`MouseButtonMask`
- `WindowInput`（每窗口原始缓冲）、`InputState`（down/pressed/released 三态 + 文本）、`InputManager`（边沿计算）
- Desktop 层：`SilkInputMapper` 把 Silk 枚举映射为引擎枚举

## 阶段 2：UI 渲染核心 + 控件树（P1~P2）

- `UIPrimitive`（屏幕空间四边形：TargetId/Rect/UV/Color/TextureId）+ `UITextureUpload`
- `UIManager`（基元收口 + 画布注册表 + 纹理上传队列）+ `UICanvas`（Arrange + 路由 + Paint + 焦点）
- `UIElement`/`UIStackPanel`/`UIPanel`；单遍布局：`FixedSize ≤ 0` = 拉伸填充
- 渲染侧：`UIRenderer`（多纹理分批 + 动态顶点/索引缓冲 + 白纹理）+ `UI.wgsl` + `UseUI()`

## 阶段 3：文本 + 交互 + 完整控件（P3~P5）

- `TextRenderer`：字符串级纹理（`MeasureSize` → `DrawText` 栅格化白字透明底 → 上传）
- `UILabel`/`UIButton`/`UITextBox`/`UICheckbox`/`UISlider`/`UITheme`
- 命中测试 + 事件路由（hover/click/drag/key/text/focus）

## 阶段 4：文字拉伸/错位定位（本次核心）

现象：文字被横向拉伸到窗口宽度、多个元素错位/消失；纯色块（蓝条/复选框/滑轨）正常。

定位过程：

1. **先排除逻辑层**：写独立测试 dump 逻辑层产出的 `UIPrimitive`，确认每个文本四边形都是字面大小
   （如 "Spark.Engine UI" = 115×16、位置在 header 内），逻辑层正确。
2. **加临时日志 dump 渲染线程顶点**：在 `UIRenderer.DrawBatch` 打 `sizeof(UIVertex)`、每批
   vertexOffset/byteOffset、每个顶点的 NDC 坐标。发现 CPU 侧顶点数据 100% 正确，问题在「取顶点」。
3. **根因**：`DrawBatch` 同时用了 `SetVertexBuffer(offset=byteOffset)` 与
   `DrawIndexed(baseVertex=vertexOffset)`。WebGPU 取址为 `offset + (index + baseVertex) × stride`，
   两者叠加后每批读到 `2×vertexOffset` 处：第一批（offset=0）碰巧对，之后全错位。
   修复：只用 `SetVertexBuffer(offset)`，`baseVertex` 恒为 0（`c44a993`）。

期间还修复/确认：

- `BytesPerRow` 未 256 对齐 → 文字错乱（`f8639e9` 内）
- 多纹理批次顶点互相覆盖 → 只剩最后一批（`f8639e9` 内，累积 offset）
- 文本纹理用 `MeasureSize`（不含 descender/悬突）→ 底部/右侧被裁（`d50243d`，改 `MeasureBounds`）

## 阶段 5：交互与布局打磨

- 复选框文字由「上对齐」改为与方框垂直居中对齐（`93f4fb1`）
- Demo 给 label/checkbox 显式 `FixedSize` 高度，避免被当 fill 均分撑满（`93f4fb1`）
- 输入框光标增加 530ms 可见/隐藏闪烁，聚焦/按键/输入时重置（`f185ff2`）

## 遗留待办

- **内容自适应尺寸**：两阶段 `Measure`/`Arrange`（当前无 `FixedSize` 的子元素被当 fill 均分，需调用方
  显式指定高度）
- 字形图集（替代字符串级纹理）；嵌入默认字体（跨平台一致）
- scissor 裁剪、滚动容器、停靠布局、多窗口 UI、脏标记增量绘制
- `UITextBox` 光标高度用 `Measure(" ").Y`（约 10px），比实际行高略短，可改为按字体行高
