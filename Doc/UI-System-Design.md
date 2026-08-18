# UI 系统设计（UI System Design）

> 状态：P0~P5 已实现，`dotnet build Spark.Engine.slnx` 0 错误。文字渲染问题（拉伸/错位/裁剪）已全部定位并修复
> （见「踩坑经验」1/2/2b/6）；复选框文字对齐、Demo 控件显式高度、输入框光标闪烁也已落地。
> 画面已在本机 GPU 环境复跑确认；剩余为 P6 打磨项。本文与当前代码同步，记录**实际落地形态**与已知偏差。

## 概述

Spark.Engine 的 3D 场景走「逻辑线程 → `SceneProxy` → `SceneSnapshot` → 渲染线程」的单通道，
对象带世界变换与包围球、按相机视锥剔除。UI 是另一类形态：

| 维度 | 3D 场景管线 | UI 系统（实际） |
|---|---|---|
| 空间 | 世界空间，WorldTransform/Bounds | 屏幕空间，逻辑像素/NDC |
| 数据模型 | 稳定 ProxyId + 每帧值快照 | 持久控件树 + 每帧扁平 `UIPrimitive` |
| 剔除 | 视锥剔除 | 无（裁剪留待 P6） |
| 输入 | 无（P3-8） | 鼠标/键盘/滚轮/文本，命中测试 |
| 文本 | 无 | 字符串级纹理（SixLabors 栅格化） |
| 渲染 | 深度测试、光照 | 无深度、Alpha 混合、正交投影 |

UI **不进** `SceneProxy`/`SceneCategory` 通道，而是作为**并行子系统**，通过 RenderGraph 的
**overlay** 钩子在 3D 场景之后、写入同一 backbuffer 的最后一次绘制（每帧每视口只 acquire/present 一次）。

## 实际组件清单

### 输入系统（`Spark.Engine/Input/`，平台无关）

| 类型 | 文件 | 职责 |
|---|---|---|
| `Key` / `KeyMask` | Key.cs | 引擎键盘枚举（85 键子集）+ 128 位按下掩码（`Enumerate`/`AndNot`） |
| `MouseButton` / `MouseButtonMask` | MouseButton.cs | 鼠标按钮枚举（8 值）+ 8 位掩码 |
| `WindowInput` | WindowInput.cs | 每窗口原始输入缓冲（位置/位移/滚轮/按钮/按键/文本） |
| `InputState` | InputState.cs | 每帧只读快照（`down`/`pressed`/`released` 三态 + 文本） |
| `InputManager` | InputManager.cs | 跨窗口聚合，按上一帧算边沿，`GetState(window)`/`PrimaryState` |

平台层 `Spark.Engine.Desktop`：`DesktopWindow.Initialize` 经 `Silk.NET.Input.InputWindowExtensions.CreateInput`
建输入上下文，订阅 `IMouse`/`IKeyboard` 事件；`SilkInputMapper` 把 Silk 枚举映射为引擎枚举（P22）。

### UI 核心（`Spark.Engine/UI/`）

| 类型 | 文件 | 职责 |
|---|---|---|
| `UISize`/`UIRect`/`UIEdgeInsets` | UIGeometry.cs | 尺寸/矩形（`Contains`/`Deflate`）/边距 |
| `UIPrimitive` | UIPrimitive.cs | 屏幕空间四边形（TargetId + Rect + UV + Color + **TextureId**） |
| `UITextureUpload` | UITextureUpload.cs | 逻辑→渲染线程的纹理上传结构（RGBA8 + 尺寸） |
| `UIManager` | UIManager.cs | 基元收口 + 画布注册表 + 默认 `TextRenderer` + 纹理上传队列 |
| `UIElement` | UIElement.cs | 控件树基类：`Arrange`/`Paint`/`HitTest` + 事件钩子 + `Focusable` |
| `UIStackPanel` | UIStackPanel.cs | 盒子布局容器（`UIOrientation` 垂直/水平 + `Spacing` + 背景） |
| `UIPanel` | UIPanel.cs | 纯色矩形叶节点 |
| `UILabel` | UILabel.cs | 文本标签（`Text`/`TextColor`） |
| `UIButton` | UIButton.cs | 按钮（背景 + 文本 + 悬停/按下态 + `Clicked` 回调） |
| `UITextBox` | UITextBox.cs | 单行输入框（焦点 + 文本输入 + 编辑键 + 光标闪烁） |
| `UICheckbox` | UICheckbox.cs | 复选框（`IsChecked` + `CheckedChanged`） |
| `UISlider` | UISlider.cs | 滑杆（拖拽取值 0..1 + `ValueChanged`） |
| `UICanvas` | UICanvas.cs | 每窗口画布：`Update(input)`（Arrange+路由）+ `Paint(ui)` + 焦点 |
| `UITheme` | UITheme.cs | 默认暗色配色（编辑器骨架用） |
| `TextRenderer` | TextRenderer.cs | 字符串级文本渲染（见下） |

### 渲染侧（`Spark.Engine/Render/`）

| 类型 | 文件 | 职责 |
|---|---|---|
| `IGraphOverlay` | Pipeline/IGraphOverlay.cs | 覆盖层接口：`AppendToGraph(graph, snapshot)` + `IDisposable` |
| `UIRenderer` | UI/UIRenderer.cs | UI overlay：多纹理分批绘制 + 动态顶点/索引缓冲 + 白纹理 + 纹理注册表 |
| `UI.wgsl` | UI/UI.wgsl | 顶点已转 NDC，片元 `textureSample(tex, samp, uv) * color` |
| `UseUI()` | UI/UIRendererExtensions.cs | 注册 `IGraphOverlay → UIRenderer` |
| `SceneSnapshot.UIPrimitives` | Render/SceneSnapshot.cs | UI 基元挂在场景快照（值快照，与场景对象解耦） |

### 编辑器（`Spark.Engine.Editor/`）

- `EditorLayout.Build()`：编辑器骨架（菜单栏 + 层级面板 + 透明视口区 + 检查器 + 状态栏），用 `UITheme`。
- `UseEditor()`：注册 `UseUI()`（不再空壳）。

## 控件清单与功能规划

> 约定：✅ = 当前已实现；⏳ = 规划待实现。文本框交互最重，在「三」单独详列。
> 本节是「功能全集」而非「近期排期」；落地顺序见文末「分阶段计划」。

### 一、控件总览

| 分类 | 控件 | 状态 | 一句话职责 |
|---|---|---|---|
| 基础 | `UIElement`（控件树基类） | ✅ | 父子关系/布局/绘制/命中测试/事件钩子 |
| 布局 | `UIStackPanel` | ✅ | 垂直/水平盒子布局 |
| 布局 | `UIPanel` | ✅ | 纯色矩形叶节点 |
| 布局 | `UIGridPanel` / `UIWrapPanel` / `UIDockPanel` | ⏳ | 网格 / 自动换行 / 停靠布局 |
| 布局 | `UIScrollBox` | ⏳ | 滚动容器（scissor 裁剪 + 滚动条） |
| 显示 | `UILabel` | ✅ | 文本标签 |
| 显示 | `UIImage` | ⏳ | 图片 / 九宫格（9-slice） |
| 显示 | `UIProgressBar` | ⏳ | 进度条 |
| 交互 | `UIButton` | ✅ | 按钮（三态 + 点击回调） |
| 交互 | `UITextBox` | ✅ | 单行输入框（v1） |
| 交互 | `UITextBox` 多行 / 富文本 | ⏳ | 多行 / 富文本输入 |
| 交互 | `UICheckbox` | ✅ | 复选框 |
| 交互 | `UIRadioButton` + `UIRadioGroup` | ⏳ | 单选按钮（互斥分组） |
| 交互 | `UISlider` | ✅ | 水平滑杆（0..1） |
| 交互 | `UISlider` 范围双滑块 / 垂直 / 步进 | ⏳ | 区间选择 / 垂直 / 吸附 |
| 交互 | `UISpinner` | ⏳ | 数字步进（上下箭头） |
| 交互 | `UIComboBox` | ⏳ | 下拉选择 |
| 交互 | `UIScrollBar` | ⏳ | 独立滚动条 |
| 容器 | `UIWindow` / `UIDialog` | ⏳ | 可拖动窗口 / 模态对话框 |
| 容器 | `UITabView` | ⏳ | 标签页 |
| 容器 | `UITreeView` | ⏳ | 树形列表（展开/折叠） |
| 容器 | `UIListView` | ⏳ | 列表（虚拟化） |
| 容器 | `UIMenuBar` / `UIContextMenu` | ⏳ | 菜单栏 / 右键上下文菜单 |
| 其他 | `UITooltip` | ⏳ | 悬停提示 |
| 其他 | `UICanvas` | ✅ | 每窗口根画布（布局 + 路由 + 焦点） |
| 其他 | `UITheme` | ✅ | 默认暗色配色（向样式系统演进） |

### 二、现有控件功能规格

#### `UIElement`（基类）
- ✅ 父子关系、`Visible`、`Focusable`、`Padding`、`FixedSize`、`Bounds`、`Arrange`、`Paint`（先父后子）、
  `HitTest`（倒序取最上层最深）、`ContainsPoint`、鼠标 enter/leave/down/up/drag/click、键盘 keyDown/keyUp、
  文本输入、焦点变化。
- ⏳ `Enabled`（禁用）、`IsHitTestVisible`（点击穿透）、`ZIndex`（显式层级）、`Clip`（裁剪到自身）、
  `Opacity`、`Style`（样式绑定）、`ToolTip`、`Dirty` 脏标记（增量绘制）、事件冒泡/捕获、双击事件、
  滚轮事件、拖放事件、`Visibility` 三态（Visible/Collapsed/Hidden）。

#### `UIStackPanel`
- ✅ 垂直/水平、`Spacing`、`Padding`、`BackgroundColor`、交叉轴默认拉伸、fill 均分剩余空间。
- ⏳ 主轴/交叉轴对齐（Start/Center/End/Stretch）、`Wrap` 自动换行、内容自适应（两阶段 Measure/Arrange，
  不再把无 `FixedSize` 的子元素当 fill 撑满）、子元素间距覆盖、溢出裁剪。

#### `UIPanel`
- ✅ 纯色矩形。
- ⏳ 圆角、边框、背景纹理/九宫格、线性/径向渐变。

#### `UILabel`
- ✅ `Text`、`TextColor`、左上角绘制。
- ⏳ 水平/垂直对齐、自动换行（Wrap）、省略号（Ellipsis）、多行、字体/字号/字重/字距/行距、
  富文本 span（局部颜色/样式）、描边/阴影、`AutoSize` 按内容自适应。

#### `UIButton`
- ✅ `Text`、背景/悬停/按下三态色、`Clicked` 回调、`Padding`。
- ⏳ 禁用态（`Enabled=false` + 样式）、图标（`UIImage`）、长按自动重复（`Repeat`）、快捷键绑定、
  `ToolTip`、`Toggle` 开关态、焦点可视化。

#### `UICheckbox`
- ✅ `IsChecked`、方框 + 勾选标记 + 文字、点击切换、`CheckedChanged`、文字与方框垂直居中。
- ⏳ 三态（`Indeterminate`）、键盘切换（Space）、禁用态、`UIRadioGroup` 组联动。

#### `UISlider`
- ✅ 水平、`Value`(0..1)、轨道/填充/拇指、拖拽取值、`ValueChanged`。
- ⏳ `Min`/`Max`/`Step` + `SnapToStep`、垂直方向、键盘调节（方向键/PageUp/Down）、刻度与数值标签、
  范围双滑块（`RangeSlider`）、禁用态。

#### `UICanvas`
- ✅ 每窗口根、`Size`、`Update(input)`（Arrange + 路由）、`Paint`、焦点、hover/pressed 状态。
- ⏳ 焦点导航（Tab/Shift+Tab）、`FocusedElement` 查询、弹出层（下拉/菜单/对话框）、DPI 缩放、多图层。

### 三、文本框（UITextBox）功能详解

> 输入框是交互最重的控件，这里把「光标 / 选择 / 编辑 / 剪贴板 / IME / 显示样式」逐项列全。

#### 光标与导航
- ✅ 左/右移动、Home/End、光标闪烁（530ms 可见/隐藏，聚焦/按键/输入时重置）。
- ⏳ 上下移动（多行）、`Ctrl+←/→` 按词移动、`Ctrl+Home/End` 到文首/尾、鼠标点击在文本内定位光标。

#### 文本选择
- ✅（无）
- ⏳ 鼠标拖拽选择、`Shift+方向键` 扩展选择、双击选词、三击选行、`Ctrl+A` 全选、选择高亮
  （选区背景色/文字色）、`SelectionStart`/`SelectionLength` API、`SelectAll()`/`ClearSelection()`。

#### 编辑
- ✅ `Text` 读/写、打字插入、`Backspace`/`Delete` 删单字符。
- ⏳ `Ctrl+Backspace` 删前一个词、`Ctrl+Delete` 删后一个词、选中文本删除/替换、`Insert` 覆写模式、
  `Undo`/`Redo`（`Ctrl+Z`/`Ctrl+Y`，操作栈 + 连续输入分组）。

#### 剪贴板
- ✅（无）
- ⏳ `Ctrl+C` 复制、`Ctrl+X` 剪切、`Ctrl+V` 粘贴、与系统剪贴板交互（核心库定义 `IClipboard` 抽象，
  平台层实现）、选中复制。

#### 输入法（IME）
- ✅ `OnTextInput` 接收合成后的文本。
- ⏳ IME 组合态可视化（候选下划线/高亮）、光标定位到组合区、组合提交/取消回调。

#### 显示与样式
- ✅ `TextColor`/`BackgroundColor`/`Padding`、光标闪烁、`Focusable`。
- ⏳ 占位文本（`Placeholder`，空内容时灰字）、密码/掩码（`MaskChar`）、只读（`ReadOnly`）、
  禁用（`Enabled=false`）、最大长度（`MaxLength`）、超长文本水平滚动（跟随光标）、
  多行模式（`Multiline` + 自动换行 + 垂直滚动）、输入过滤/校验（`InputFilter`/`Validator`，
  如只数字/正则/自定义）、`TextAlignment`、光标高度按字体行高（当前用 `Measure(" ").Y` 略短）。

### 四、通用能力规划

#### 输入 / 焦点
- Tab/Shift+Tab 焦点导航、焦点环可视化、全局快捷键系统、双击、滚轮、拖放（drag & drop）、
  触摸/触控笔、手柄导航。

#### 渲染
- scissor 裁剪（滚动容器/下拉/弹窗）、圆角/边框/阴影/渐变、九宫格（9-slice）、
  字形图集（替代字符串级纹理）、嵌入字体（跨平台一致）、字体样式（粗体/斜体/字重/字距）、
  文字抗锯齿、多窗口 UI、DPI 缩放、脏标记增量绘制、按纹理批处理优化。

#### 主题 / 样式
- `UITheme` 扩展为样式系统（颜色/字体/圆角/间距/各状态色）、样式表（Style/Class）、
  暗/亮主题切换、控件外观定制。

#### 布局
- 两阶段 Measure/Arrange（内容自适应）、`UIGridPanel`/`UIWrapPanel`/`UIDockPanel`、锚点/相对定位。

#### 数据 / 无障碍 / 本地化
- 数据绑定（Model → 控件，MVVM 风格）、列表虚拟化（长列表性能）、
  无障碍（Accessibility/读屏）、本地化（L10n/I18n）、文本方向（LTR/RTL）。

## 数据流

```
逻辑线程（EngineApplication 每帧）：
  WindowManager.UpdateWindow()          // PollEvents → DesktopWindow 填 WindowInput
  InputManager.Update(windows)          // 算 pressed/released 边沿 → InputState
  OnUpdate(dt)                          // World 更新（游戏逻辑）
  FillFrameData(snapshot)：
    对每窗口：canvas.Size = window.Size
              canvas.Update(input)      // Arrange（布局）+ 输入路由（hover/点击/键盘/文本）
              canvas.Paint(ui)          // 控件树 → ui.Primitives（UIPrimitive）
    拷贝 ui.Primitives → snapshot.UIPrimitives；ui.Clear()
  DualFrameBuffer.SubmitReady()

渲染线程（SceneRenderPipeline.Render）：
  ProcessUploads / SyncProxyStates
  BuildGraph(graph, snapshot)           // 场景 pass（ShadowDepth → BlinnPhong …）
  遍历 IGraphOverlay → AppendToGraph    // UIRenderer：排空纹理上传队列 → 追加 UIOverlay pass
  graph.Compile() / Execute()           // 每帧每视口 acquire/present 一次
```

## 文本渲染（P3，字符串级 v1）

`TextRenderer`（逻辑线程）按**整段字符串**渲染：`TextMeasurer.MeasureBounds` 取含 descender/悬突的实际包围盒定纹理尺寸 →
`Image<Rgba32>` + `RichTextOptions{Dpi=72, Origin=(0,0)}` 把白字画到透明底 → RGBA8 入队（`UIManager.EnqueueTexture`）→
渲染线程 `UIRenderer.ProcessTextureUploads` 上传 → `UILabel` 以带 `TextureId` 的四边形绘制（着色经 `Color` tint）。

- 按字符串缓存纹理（`Dictionary<string,int>`）；`Measure(text)` 供 `UITextBox` 光标定位。
- **非字形图集**：每段文本一张纹理，纹理数随字符串数增长（编辑器动态文本多时需换字形图集）。

## 设计原则（延续 P1~P17，新增 UI 专用）

- **P18 UI 与场景解耦**：UI 不占 `SceneCategory`，无世界变换/包围球。
- **P19 保留模式控件树 + 每帧扁平基元**：状态持久在控件树，每帧 paint 出 `UIPrimitive` 值快照。
- **P20 输入是显式前置**：交互依赖输入系统（P0 先行）。
- **P21 UI 渲染是 overlay pass**：`IGraphOverlay` 在 `BuildGraph` 后追加，共享帧级 acquire/present。
- **P22 平台类型不出核心库**：核心用引擎 `Key`/`MouseButton`，Silk 枚举在 Desktop 映射。
- **P23 文本走字符串级纹理（v1）**：SixLabors 栅格化整段文本为一张纹理；字形图集是后续优化。

## 决策记录（ADR 续）

| ID | 决策 |
|---|---|
| ADR-22 | UI 采用**保留模式控件树**（对齐 Slate/WPF/UGUI），非即时模式 |
| ADR-23 | UI 基元 `UIPrimitive` 作为 `SceneSnapshot` 的 `UIPrimitives` 字段（`FrameBuffer<UIPrimitive>`），复用双缓冲与值快照 |
| ADR-24 | UI 渲染为 `IGraphOverlay`（DI 注册），`SceneRenderPipeline.Render` 在 `BuildGraph` 后追加 |
| ADR-25 | 输入抽象在核心库（引擎枚举 + `InputState`），Silk.NET 事件在 Desktop 映射 |
| ADR-26 | 文本栅格化用 SixLabors.Fonts + ImageSharp.Drawing，**字符串级纹理**（v1），系统字体（Arial→Segoe UI→DejaVu Sans 回退） |
| ADR-27 | UI 多纹理按 `TextureId` 分批绘制，顶点写入**累积 offset**（`DrawIndexed` 的 `baseVertex`），避免批次覆盖 |
| ADR-28 | RGBA8 纹理上传的 `BytesPerRow` 必须 **256 对齐**（WebGPU `COPY_BYTES_PER_ROW_ALIGNMENT`），紧密数据重排为对齐 stride |

## 踩坑经验（本次实现中实际踩到）

1. **纹理上传 `BytesPerRow` 未 256 对齐 → 文字错乱**：多行 RGBA8 纹理的 `BytesPerRow = width*4` 若不是 256 的倍数，
   wgpu 会产生错位/未定义内容（表现为文字拉伸、错位）。单行（`height=1`）纹理豁免（所以 1×1 白纹理/纯色块正常）。
   修复：把紧密 RGBA 重排为 256 对齐 stride（行尾补零），`BytesPerRow` 传对齐值。见 ADR-28。

2. **多纹理批次顶点互相覆盖 → 只剩最后一批**：`UIRenderer` 按 `TextureId` 分批，最初每批都写顶点缓冲 offset 0，
   后批覆盖前批，导致只有最后画的控件显示。修复：累积 offset + `baseVertex`。见 ADR-27。

2b. **（本次根因）`SetVertexBuffer(offset)` 与 `DrawIndexed(baseVertex)` 双重偏移**：上一项修复时，顶点写入用
   `QueueWriteBuffer` 累积 byte offset，绘制时**同时**传了 `SetVertexBuffer(offset=byteOffset)` 和
   `DrawIndexed(baseVertex=vertexOffset)`。WebGPU 里两者是「二选一」的同一偏移——最终取址为
   `offset + (index + baseVertex) × stride`，叠加后每批实际读到 `2×vertexOffset` 处的顶点：第一批（offset=0）碰巧正确，
   之后每批都错位，表现为文字被拉伸到别的控件的矩形、部分元素消失。修复：只保留 `SetVertexBuffer(offset)`，
   `baseVertex` 恒为 0。

3. **字符串级文本的代价**：比字形图集实现简单，但「一段文本一张纹理」，编辑器里动态/大量文本会占纹理，
   后续需替换为字形图集（共享一张图集 + 按字形 UV 引用）。

4. **单遍布局无内容自适应**：`FixedSize`（null 或分量 ≤0 = 填充）只支持「固定 or 填充」，fill 子元素均分剩余空间，
   无「按内容包裹」；表现为复选框方块在其 fill 区域内垂直居中、与文字错开。后续需两阶段 `Measure`/`Arrange`。
   当前缓解：(a) `UICheckbox` 的文字已改为与方框垂直居中对齐（不再上对齐）；(b) Demo 里 label/checkbox 显式
   指定 `FixedSize` 高度，避免被当 fill 撑满。

5. **`Math` 命名空间遮蔽**：`Spark.Engine.Math` 命名空间会遮蔽 `System.Math`，UI 代码需写全限定 `System.Math`。

6. **文字底部/右侧被裁掉几像素**：`TextRenderer.CreateTexture` 用 `MeasureSize`（前向宽度 × 行高）当纹理尺寸，
   但该值不含下伸部（descender，如 `g/p/y`）与右侧悬突，导致底部和右侧的像素被裁。修复：改用
   `MeasureBounds`（实际墨水包围盒）取 `ceil(Right)+1` / `ceil(Bottom)+1` 作为纹理尺寸，并复用同一 `RichTextOptions`
   （`Origin=(0,0)`）做测量与绘制，保证测量和光栅化一致。

## 已知限制 / 后续（P6+）

> 逐控件的功能全集（含文本框复制/粘贴/选择/词删除、剪贴板、IME、Undo/Redo 等）见上文
> 「控件清单与功能规划」，此处只列结构性限制。

- **文字渲染**：四处 bug 已修复（见踩坑 1/2/2b/6），画面已复跑确认。
- **单遍布局无内容自适应**：当前 `UIStackPanel` 无 `FixedSize` 的子元素会被当 fill 均分剩余空间，
  需两阶段 Measure/Arrange（这是「空白/复选框错位」的结构性根因，Demo 目前用显式 `FixedSize` 缓解）。
- **字符串级文本纹理**：每段文本一张纹理，纹理数随字符串数增长，需换字形图集 + 嵌入字体（跨平台一致）。
- **无裁剪/滚动/弹出层**：scissor 裁剪、`UIScrollBox`、下拉/菜单/对话框的弹出层尚未实现。

## 分阶段计划（现状）

| 阶段 | 内容 | 状态 |
|---|---|---|
| P0 | 输入系统（Key/MouseButton/WindowInput/InputState/InputManager） | ✅ |
| P1 | UI 渲染核心（UIPrimitive/UIRenderer overlay/UI.wgsl/UseUI） | ✅ |
| P2 | 控件树 + 布局（UIElement/UICanvas/UIStackPanel/UIPanel） | ✅ |
| P3 | 字体/文本（TextRenderer 字符串级 + UILabel + 多纹理渲染器） | ✅ |
| P4 | 交互（HitTest + 事件路由 + UIButton/UITextBox） | ✅ |
| P5 | 完整控件 + 主题 + 编辑器接入（UICheckbox/UISlider/UITheme/EditorLayout） | ✅ |
| P6 | 内容自适应布局（两阶段 Measure/Arrange）+ 布局控件（Grid/Wrap/Dock） | ⏳ |
| P7 | 文本框进阶（选择/复制粘贴/词删除/Undo/剪贴板/IME/多行/掩码） | ⏳ |
| P8 | 更多控件（UIImage/ProgressBar/Radio/Combo/Spinner/Scroll/Tree/List/Tab/Menu/Window/Tooltip） | ⏳ |
| P9 | 渲染打磨（字形图集/嵌入字体/scissor 裁剪/圆角边框阴影/九宫格/增量绘制/DPI） | ⏳ |
| P10 | 主题样式系统 + 数据绑定 + 无障碍/本地化 | ⏳ |

> 说明：P0~P5 已实现并通过本机 GPU 复跑；P6~P10 对应上文「控件清单与功能规划」的 ⏳ 项，
> 具体功能逐条见该节。

## 关联文档

- [RenderPipeline-Design.md](./RenderPipeline-Design.md) — 渲染管线（RenderGraph/pass/overlay 集成点）
- [SceneSync-Design.md](./SceneSync-Design.md) — 场景同步（SceneSnapshot 值快照通道）
- [RenderGraph-Design.md](./RenderGraph-Design.md) — 帧图（overlay pass 挂接）
