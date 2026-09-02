# UI 系统设计（UI System Design）

> 状态：P0~P6 与 P8 编辑器核心控件已实现，P6-fix / P8 控件 / P8 审计三轮均已本机 GPU 实机验收通过（2026-08-31）；
> P7 多行/IME 组合态、P9/P10 仍是后续工作。
> P6 原有两阶段 Measure/Arrange、scissor 裁剪、Tab 焦点导航、焦点环可视化、UIGridPanel/UIWrapPanel 布局容器。
> 文字渲染问题（拉伸/错位/裁剪）已全部定位并修复（见「踩坑经验」1/2/2b/6/6b）；
> 复选框文字对齐、Demo 控件显式高度、输入框光标闪烁也已落地。
>
> **P6-fix 补丁**（本次）修复了文档标 ✅ 但实际未达标的项：UIGridPanel.Auto 尺寸传递、RowSpan/ColumnSpan、
> 附加属性实例化（修复静态字典泄漏）、HitTest 受 ClipToBounds 约束（P6.2 设计决策落地）、裁剪栈按 targetId 隔离、
> UIElement.RemoveChild/ClearChildren/重挂自动摘除旧父/环检测、TextRenderer 全墨水包围盒（含负 Left/Top）+ 原点补偿。
>
> **P8 编辑器控件轮（2026-08-26）已落地**：新增 10 个编辑器刚需控件（见下方「编辑器控件」小节）+
> **Overlay 弹出层机制**（`UICanvas.Overlays`）。冒烟测试通过（Demo 启动 8s 无崩溃），用户已逐场景验收通过。
> 验收入口：`Demo/Demo/EditorControlsVerifyOverlay.cs`（VerifyHub 第 5 个按钮进入，含 9 个子场景）。
>
> **P8 审计修复轮（2026-08-31）已落地**：审计全部 UI 控件 + 12 处缺陷修复——
> 滚动裁剪失效（scissor 空交集语义，见踩坑 18）、文本高度波动（踩坑 12）、容器 Measure/Arrange 基准不一致
> （踩坑 13/14/15）、SplitPanel/Dialog/Toolbar 交互缺陷等。编辑器控件专项 42 个测试全通过（含 13 个回归锁定）。
> 详见 `Doc/tasks/2026-08-31-editor-controls-audit-fixes-worklog.md`。
>
> **验收状态：✅ 已验收（2026-08-31）。** P6-fix / P8 控件 / P8 审计三轮经用户在本机运行
> Demo.Desktop 逐场景目视确认，未发现问题。剩余为 P7/P9/P10 打磨项。
> 本文与当前代码同步，记录**实际落地形态**与已知偏差。

> **P7 单行文本增强（当前增量）**：`UITextBox` 已支持选择、鼠标拖选、Shift/Ctrl 导航、剪贴板抽象、
> 删除/替换、Undo/Redo、Placeholder、ReadOnly、MaxLength、Password 掩码和超长文本水平滚动；
> 多行排版与 IME 组合态仍待后续实现。

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
| `UIDockPanel` | UIDockPanel.cs | 停靠布局容器（`UIDock` Top/Bottom/Left/Right/Fill + `LastChildFill` + 背景） |
| `UIPanel` | UIPanel.cs | 纯色矩形叶节点 |
| `UILabel` | UILabel.cs | 文本标签（`Text`/`TextColor`） |
| `UIButton` | UIButton.cs | 按钮（背景 + 文本 + 悬停/按下态 + `Clicked` 回调） |
| `UITextBox` | UITextBox.cs | 单行输入框（选择/剪贴板/Undo/Redo/掩码/水平滚动；多行与 IME 组合态 ⏳） |
| `UICheckbox` | UICheckbox.cs | 复选框（`IsChecked` + `CheckedChanged`） |
| `UISlider` | UISlider.cs | 滑杆（拖拽取值 0..1 + `ValueChanged`） |
| `UICanvas` | UICanvas.cs | 每窗口画布：`Update(input)`（Arrange+路由）+ `Paint(ui)` + 焦点 + **`Overlays` 弹出层** |
| `UITheme` | UITheme.cs | 默认暗色配色常量集（非样式系统；样式系统⏳ P10） |
| `TextRenderer` | TextRenderer.cs | 字符串级文本渲染（见下） |

### 编辑器控件（P8，2026-08-26 落地）

| 类型 | 文件 | 职责 |
|---|---|---|
| `UIScrollBox` | UIScrollBox.cs | 滚动容器（垂直/水平/双向 + 滚轮 + 滚动条拖拽 + `ScrollIntoView`） |
| `UIListView` / `UIListItem` | UIListView.cs | 垂直列表（单选 + 键盘导航 + 选择/激活回调） |
| `UITreeView` / `UITreeViewItem` | UITreeView.cs | 层级树（`SubItems` 逻辑子项 + 扁平化可视列表 + 展开/折叠/单选/键盘导航） |
| `UIMenuPanel` / `UIMenuItem` | UIMenu.cs | 弹出菜单（Overlay 注册 + 按 `Position` 定位 + 分隔线/快捷键） |
| `UIMenuBar` / `UIMenuBarItem` | UIMenu.cs | 菜单栏（顶级菜单项 + 点击展开/收起下拉） |
| `UIDialog` / `UIDialogButton` | UIDialog.cs | 模态对话框（遮罩铺满画布 + 居中面板 + 按钮 + Escape/Enter 处理） |
| `UITabView` / `UITabItem` | UITabView.cs | 标签页（动态标签宽度 + 关闭按钮 + 内容切换） |
| `UIComboBox` | UIComboBox.cs | 下拉选择框（点击展开/收起 + 键盘导航 + 选中回调；下拉绘制在统一 Overlay 阶段） |
| `UISplitPanel` | UISplitPanel.cs | 可拖拽分割面板（水平/垂直 + 比例 + 最小尺寸约束） |
| `UIToolbar` / `UIToolbarButton` | UIToolbar.cs | 工具栏（水平按钮组 + 分隔符） |
| `UIPropertyGrid` | UIPropertyGrid.cs | 属性网格（反射对象属性 + 标签/值编辑器行 + `PropertyChanged` 回调） |

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
- `UseEditor()`：注册 UI overlay，并在游戏内容初始化后自动把 `EditorUi` 挂到主窗口、注册逐帧刷新；可选配置回调用于定制编辑器视口。
- `IEditorSceneService`：编辑器保存/重载边界；宿主通过 `UseEditor(..., sceneService)` 或
  `EditorLayout.Build(..., sceneService)` 注入具体文件/资产库实现，编辑器负责 Dirty 与撤销历史闭环。

## 控件清单与功能规划

> 约定：✅ = 当前已实现且功能完整；🔶 = 已实现但功能不完整（标注缺失项）；⏳ = 未实现。
> 文本框交互最重，在「三」单独详列。
> 本节是「功能全集」而非「近期排期」；落地顺序见文末「分阶段计划」。

### 一、控件总览

| 分类 | 控件 | 状态 | 一句话职责 |
|---|---|---|---|
| 基础 | `UIElement`（控件树基类） | ✅ | 父子关系/布局/绘制/命中测试/事件钩子 |
| 布局 | `UIStackPanel` | ✅ | 两阶段 Measure/Arrange + 垂直/水平盒子布局（缺对齐/裁剪） |
| 布局 | `UIDockPanel` | ✅ | 两阶段 Measure/Arrange + 边缘停靠布局（缺分隔条/浮动重吸附） |
| 布局 | `UIPanel` | 🔶 | 纯色矩形叶节点（缺圆角/边框/渐变） |
| 布局 | `UIGridPanel` | ✅ | 网格布局（Fixed/Star/Auto + Row/Column 附加属性 + **RowSpan/ColumnSpan**；P6-fix：Auto 尺寸从 Measure 传到 Arrange、附加属性实例化） |
| 布局 | `UIWrapPanel` | ✅ | 自动换行布局（水平/垂直 + ItemSpacing/LineSpacing） |
| 布局 | `UIScrollBox` | ✅ | 滚动容器（双向滚动、滚轮、滚动条拖拽、`ScrollIntoView`） |
| 显示 | `UILabel` | ✅ | 文本标签 + Measure 自适应（缺对齐/换行） |
| 显示 | `UIImage` | ⏳ | 图片 / 九宫格（9-slice） |
| 显示 | `UIProgressBar` | ✅ | 确定性进度条（0..1） |
| 交互 | `UIButton` | ✅ | 按钮三态+点击 + Measure 自适应（缺禁用态/图标/快捷键/Toggle） |
| 交互 | `UITextBox` | 🔶 | 单行输入框：选择/剪贴板/Undo/Redo/Placeholder/ReadOnly/MaxLength/掩码已实现，多行与 IME 组合态待补 |
| 交互 | `UICheckbox` | ✅ | 复选框 + Measure 自适应（缺三态/键盘Space/禁用态） |
| 交互 | `UIRadioButton` + `UIRadioGroup` | ⏳ | 单选按钮（互斥分组） |
| 交互 | `UISlider` | ✅ | 水平滑杆 0..1 + Measure 自适应（缺 Min/Max/Step/垂直/键盘/范围） |
| 交互 | `UISpinner` | ⏳ | 数字步进（上下箭头） |
| 交互 | `UIComboBox` | ✅ | 下拉选择 + 键盘导航 + Overlay |
| 交互 | `UIScrollBar` | ⏳ | 独立滚动条 |
| 容器 | `UIWindow` | ⏳ | 可拖动/停靠窗口（尚未实现） |
| 容器 | `UIDialog` | ✅ | 模态对话框（Overlay 遮罩、默认/取消按钮、Escape/Enter） |
| 容器 | `UITabView` | ✅ | 标签页 + 关闭按钮 + 内容切换 |
| 容器 | `UITreeView` | ✅ | 层级树 + 展开/折叠 + 单选/键盘导航 |
| 容器 | `UIListView` | ✅ | 列表 + 单选 + 键盘导航（虚拟化待补） |
| 容器 | `UIMenuBar` / `UIMenuPanel` | ✅ | 菜单栏 / Overlay 弹出菜单 |
| 其他 | `UITooltip` | ⏳ | 悬停提示 |
| 其他 | `UICanvas` | ✅ | 两阶段布局 + 焦点导航/焦点环 + Overlay + 点击空白取消焦点（DPI 待补） |
| 其他 | `UITheme` | 🔶 | 暗色配色常量集（非样式系统；样式/换肤⏳ P10） |

### 二、现有控件功能规格

#### `UIElement`（基类）
- ✅ 父子关系、`Visible`、`Focusable`、`Padding`、`FixedSize`、`Bounds`、`Arrange`、`Paint`（先父后子）、
  `HitTest`（倒序取最上层最深）、`ContainsPoint`、鼠标 enter/leave/down/up/drag/click、键盘 keyDown/keyUp、
  文本输入、焦点变化。
- ⏳ `Enabled`（禁用）、`IsHitTestVisible`（点击穿透）、`ZIndex`（显式层级）、`Clip`（裁剪到自身）、
  `Opacity`、`Style`（样式绑定）、`ToolTip`、`Dirty` 脏标记（增量绘制）、事件捕获、双击事件、拖放事件、
  `Visibility` 三态（Visible/Collapsed/Hidden）。滚轮事件已实现为沿祖先链冒泡。

#### `UIStackPanel`
- ✅ 垂直/水平、`Spacing`、`Padding`、`BackgroundColor`、交叉轴默认拉伸、fill 均分剩余空间。
- ⏳ 主轴/交叉轴对齐（Start/Center/End/Stretch）、`Wrap` 自动换行、子元素间距覆盖。
  两阶段 Measure/Arrange 与溢出裁剪已实现；自动换行由 `UIWrapPanel` 提供。

#### `UIDockPanel`
- ✅ `UIDock`（Left/Top/Right/Bottom/Fill）+ `LastChildFill`、`BackgroundColor`、`Padding`；子元素按声明顺序
  依次停靠到边缘（Top/Bottom 占满剩余宽度、Left/Right 占满剩余高度），停靠厚度取 `FixedSize`，
  最后一个可见子元素填满剩余中央区域（对齐 WPF `DockPanel`）。
- ⏳ 可拖拽分隔条（splitter）调整各区域大小、浮动/重新吸附（完整 IDE 式停靠）。

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
- ✅ 每窗口根、`Size`、`Update(input)`（Arrange + 路由）、`Paint`、焦点、hover/pressed 状态、
  Tab/Shift+Tab 导航、`FocusedElement` 查询、点击空白取消焦点、Overlay 弹出层。
- ⏳ DPI 缩放、多窗口/多图层策略。

### 三、文本框（UITextBox）功能详解

> 输入框是交互最重的控件，这里把「光标 / 选择 / 编辑 / 剪贴板 / IME / 显示样式」逐项列全。
> 当前状态：🔶 单行编辑能力已完成；多行排版、IME 组合态等进阶能力仍待后续迭代。

#### 光标与导航
- ✅ 左/右移动、Home/End、Ctrl+←/→ 按词移动、鼠标点击定位、光标闪烁（530ms 可见/隐藏，聚焦/按键/输入时重置）。
- ⏳ 上下移动（多行）、`Ctrl+Home/End` 到文首/尾。

#### 文本选择
- ✅ 鼠标拖拽选择、`Shift+方向键` 扩展选择、`Ctrl+A` 全选、选区高亮、
  `SelectionStart`/`SelectionLength`/`HasSelection` API、`SelectAll()`/`ClearSelection()`。
- ⏳ 双击选词、三击选行、多行选区。

#### 编辑
- ✅ `Text` 读/写、打字插入、`Backspace`/`Delete`、Ctrl+Backspace/Delete 删词、选中文本删除/替换、
  `Undo`/`Redo`（`Ctrl+Z`/`Ctrl+Y`，操作栈）。
- ⏳ `Insert` 覆写模式、多行编辑。

#### 剪贴板
- ✅ `Ctrl+C` 复制、`Ctrl+X` 剪切、`Ctrl+V` 粘贴，核心库通过 `IClipboard` 抽象接入平台剪贴板。

#### 输入法（IME）
- ✅ `OnTextInput` 接收合成后的文本。
- ⏳ IME 组合态可视化（候选下划线/高亮）、光标定位到组合区、组合提交/取消回调。

#### 显示与样式
- ✅ `TextColor`/`BackgroundColor`/`Padding`、光标闪烁、`Focusable`、占位文本、密码掩码、只读、
  最大长度、超长文本水平滚动，以及单行选区高亮。
- ⏳ 禁用态（`Enabled=false`）、多行模式（自动换行 + 垂直滚动）、输入过滤/校验、`TextAlignment`、
  字体行高精确适配。

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

6b. **文字顶部/左侧被裁掉（P6-fix）**：踩坑6 只覆盖了底部/右侧（`ceil(Right)+1` / `ceil(Bottom)+1` + `Origin=(0,0)` 不变）。
   `MeasureBounds` 的 `Left`/`Top` 可能为负（斜体左侧悬突、`Å/É` 等 ascender 超出线高、组合符上附加符号），
   此时字形像素画到纹理边界外仍被裁，且有亚像素错位。修复：纹理覆盖全包围盒 `[ceil(Right-Left)+2] × [ceil(Bottom-Top)+2]`，
   四向各留 1px 抗锯齿余量；绘制 `Origin` 平移到 `(1-Left, 1-Top)`；`DrawText` 用偏移 `(Left-1, Top-1)` 放置四边形，
   使墨水精准落到 `position`。见 `TextBoundsVerifyOverlay`。

7. **UIGridPanel.Auto 塌陷（P6-fix）**：旧版 `OnMeasure` 收集了 Auto track 尺寸，但 `OnArrange.ResolveSizes` 把 Auto
   一律置 0（源码挂着 `// TODO: 完善 Auto 尺寸的传递`），导致 Auto 单元格塌陷为 0 像素、子元素不可见，但文档却标 ✅。
   修复：Measure 把 Auto 尺寸缓存到实例字段 `_measureRowAutoSizes`/`_measureColAutoSizes`，Arrange 直接复用；
   Arrange 未先经过 Measure 时回退为 0（Auto 塌陷，文档化）。见 `GridPanelVerifyOverlay`。

8. **UIGridPanel 附加属性静态字典泄漏 + 跨画布串数据（P6-fix）**：旧版 `_rows`/`_cols` 是 `static Dictionary`，
   元素销毁后条目永不回收，多个 UIGridPanel 实例共用同一字典互相串数据。修复：改为实例字段字典；
   新增 `SetRowSpan`/`SetColumnSpan`（默认 1），Arrange 合并多轨为联合矩形。见 `GridPanelVerifyOverlay`。

9. **裁剪栈全局单例（P6-fix）**：旧版 `UIManager._clipStack` 是单例 `Stack<UIRect>`，多窗口/多 overlay pass 连续
   Paint 时前一个画布的 PushClip 残留会错误交集到后一个画布。修复：改为 `Dictionary<int, Stack<UIRect>>` 按 `targetId`
   隔离；`PushClip`/`PopClip`/`CurrentClip` 均带 `targetId` 参数。`UIElement.Paint` 改用 try/finally 保证异常时 push/pop 平衡。

10. **UIElement 树操作不完整（P6-fix）**：旧版只有 `AddChild`，无 `RemoveChild`，且不检查重复挂载/环。
    修复：新增 `RemoveChild`/`ClearChildren`；`AddChild` 自动从旧父 `_children` 摘除（避免双份布局/绘制/事件）；
    自挂自或把后代挂到祖先会抛 `InvalidOperationException`（环检测）。见 `TreeOpsVerifyOverlay`。

11. **`Measure().Y`（墨水盒高）误当行高/垂直居中基准（P8 审计）**：`TextRenderer.Measure(text)` 返回
    **该字符串的墨水包围盒高**（随字符变化：含 descender 的 "button" 更高），若用于布局高度或垂直居中，
    会导致①同字号不同文本高度不一致（按钮高度波动）、②文字基线漂移。修复：`TextRenderer` 新增
    `LineHeight`（含 line gap 的真实行高，`(三行墨水盒-单行墨水盒)/2`），布局高度与垂直居中全部改用
    `LineHeight`；水平宽度仍用墨水宽。规则：**垂直用 LineHeight，水平用 Measure 宽**。

12. **`MeasureBlock` 高度随文本波动 → 布局位移（P8 审计）**：多行文本高度曾用 `max(行数×LineHeight, 墨水盒高)`，
    墨水盒随字符变化 → 状态文字变化时（如 Toolbar 点击改 statusLabel）下方控件整体位移。
    修复：高度改回**固定** `行数 × LineHeight`。LineHeight 已含 line gap，行框足以容纳墨水
    （端到端验证墨水底部 ≤ 布局底部 + 2px，不裁剪）。

13. **容器 Measure 约束未减自身 Padding（P8 审计）**：`UIStackPanel`/`UIScrollBox`/`UIGridPanel`/`UIWrapPanel`
    给子元素的测量约束直接用 `availableSize`（未减自身 Padding），而 Arrange 用 `ContentRect`（已减）——
    基准不一致：fill 子元素（如含 Star 列的 Grid）按错误宽度测量，Arrange 时溢出内容区（右侧贴窗口边缘）。
    修复：Measure 阶段子元素可用空间 = 传入约束 - 自身 Padding（WPF 语义）。规则：**Measure 与 Arrange
    必须用同一基准（ContentRect）**。

14. **容器 FixedSize 早退跳过子测量（P8 审计）**：`UIStackPanel`/`UIDockPanel`/`UIWrapPanel`/`UIScrollBox`/
    `UIGridPanel` 的 `OnMeasure` 在 `FixedSize` 双分量>0 时直接 `return fs`，跳过子元素测量——
    而 Arrange 复用的 Auto 尺寸/内容尺寸依赖测量结果 → Auto 行塌陷、滚动范围算不出。
    修复：**先测量子元素，再处理 FixedSize 返回值**（FixedSize 只影响本控件尺寸，不影响子测量）。

15. **复合控件未布局内部面板（P8 审计）**：`UIToolbar`/`UIMenuBar` 只 `AddChild(_itemsPanel)` 却无
    `OnArrange` → 内部面板从不布局，`Bounds` 恒 `(0,0,0,0)`，所有子项叠在左上角（文字重叠）。
    修复：复合控件必须重写 `OnMeasure`（先测内部面板）+ `OnArrange`（面板铺满 `ContentRect`）。
    同理 `UIListView`/`UITreeView`/`UIPropertyGrid` 需让内部 `_scrollBox` 先 Measure 再 Arrange。

16. **`Math.Clamp` 退化尺寸抛异常（P8）**：`Math.Clamp(value, min, max)` 在 `min > max` 时抛
    `ArgumentException`。布局退化（总尺寸 < 分割条+最小面板）时 `availableSize` 为负 → `min > max`。
    修复：先钳 `min`/`max` 到可用空间内并保证 `max ≥ min`，再 `Clamp`。

17. **按下前缺悬停移动通知（P8）**：`_hoveringSplitter` 只在 `OnMouseDrag`（按下后）更新，按下时
    `OnMouseDown` 读到旧值 → 拖拽无法启动。修复：`UIElement` 新增 `OnMouseMove(position)`（未按下也通知），
    `UICanvas.RouteInput` 每帧对 hovered 元素调用；按下前悬停态已就绪。

18. **scissor「空交集」与「无裁剪」语义混淆（P8，最重要）**：`UIManager.Intersect` 对完全越出视口的
    裁剪矩形返回 `(x,y,0,0)`，与「无裁剪」默认 `(0,0,0,0)` 无法区分。渲染层 `UIRenderer.DrawBatch`
    见 `Z≤0||W≤0` 判定「无裁剪」→ 重置全视口 → 越出项 NDC 落在 [-1,1] 内的部分被画出来
    （滚动内容越过视口可见）。修复：`Intersect` 空交集返回**负尺寸** `(x,y,-1,-1)` 标记「完全裁剪」；
    `DrawBatch` 检测负尺寸跳过该批。语义：**null=无裁剪、正尺寸=部分裁剪、负尺寸=完全裁剪**。

19. **`OnMouseClick` 空实现（P8）**：`UIDialog` 的 `_hoveredButton` 在 `OnMouseDrag` 更新了，
    `OnMouseClick` 却空实现 → 点按钮什么都不发生。修复：点击时按 `_hoveredButton` 触发回调 + `Close`。
    教训：更新了状态就必须消费它。

20. **文本截断按字符数比例（P8）**：`text[..(int)(len×maxW/width)]` 假设等宽字体，非等宽下截断后仍超宽
    （且省略号宽度未计入预算）。修复：`TextRenderer.Truncate(text, maxWidth)` 逐字符测量直到超宽。
    UIComboBox/UITabView/UIPropertyGrid/UIDialog 全部改用。

21. **树/列表扁平化破坏逻辑父子关系（P8）**：`UITreeView.RebuildFlatList` 用 `AddChild` 把树项重挂到
    面板，子项从逻辑父的 `Children` 被摘走 → 树结构丢失、键盘"跳父节点"死分支（视觉 Parent 是
    UIStackPanel）。修复：`UITreeViewItem` 维护独立 `SubItems` 逻辑子项 + `LogicalParent` 引用，
    与扁平化可视列表分离。

22. **RouteInput 期间替换 Root 导致当帧空白（P8）**：`Update` 先布局后 `RouteInput`，按钮点击在
    `RouteInput` 替换 `Root` → 本帧 `Paint` 遍历未布局新 Root（Bounds 全 0）→ UI 空白一帧（露出 3D）。
    修复：`UICanvas.Update` 在 `RouteInput` 后检测 `Root != _lastLayoutRoot`，同帧补布局。
    同理 `UIMenuPanel.Show()` 内立即 Measure/Arrange（RouteInput 期间调用时本帧已过布局）。

## 当前问题与差距分析

> 本节基于当前 P0~P8 实际代码与设计文档规划的对照，系统梳理仍存在的结构性问题、功能缺口和风险点。
> P6/P8 已落地的控件与交互不再列为缺口；历史实现计划仍保留在后文，便于追溯。

### 一、结构性问题

#### 1.1 ~~单遍布局无内容自适应~~ ✅ P6 已解决

**P6 实现**：
- `UIElement` 新增 `Measure(UISize availableSize)` + `OnMeasure()` 两阶段布局协议
- `UIStackPanel` / `UIDockPanel` 改造为 Phase 1 Measure + Phase 2 Arrange
- `UILabel` / `UIButton` / `UICheckbox` / `UISlider` / `UITextBox` 均重写 `OnMeasure` 报告内容驱动的期望尺寸
- 向后兼容：未重写 `OnMeasure` 的子元素保持原有 fill 语义
- `UICanvas.Update` 在 Arrange 前调用 `Root.Measure`，并注入 `TextRenderer` 供叶子控件测量文本

#### 1.2 ~~无裁剪机制~~ ✅ P6 已解决

**P6 实现**：
- `UIElement.ClipToBounds` 属性：启用后 `Paint` 自动 push/pop 裁剪栈
- `UIManager` 新增 `PushClip` / `PopClip` / `CurrentClip` 裁剪栈（取交集）
- `UIPrimitive.ScissorRect` 字段：逻辑线程添加基元时从裁剪栈注入
- `UIRenderer.DrawBatch` 按 (TextureId, ScissorRect) 双键分批，每批前调用 `RenderPassEncoderSetScissorRect`
- 无裁剪时重置为全视口

#### 1.3 字符串级文本纹理的性能隐患（→ P9）

**现状**：`TextRenderer` 以完整字符串为 key 缓存纹理（`Dictionary<string, int>`）。
每个唯一字符串生成一张独立 RGBA8 纹理，经 `UIManager.EnqueueTexture` 上传到 GPU。

**影响**：
- 动态文本（如 `"Clicks: {counter}"`、`"Value: {value:F2}"`）每次变化创建新纹理
- 编辑器场景下大量动态文本可能导致纹理数量达数千张
- `TextRenderer._textureIds` 和 `_textureSizes` 只增不减，无淘汰/回收机制
- `UIRenderer._textures` 和 `_textureBindGroups` 同样只增不减
- GPU 内存持续增长，长期运行有泄漏风险

**缓解措施（临时）**：可在 `TextRenderer` 中加入 LRU 淘汰策略，限制最大缓存数。

**根本解决**：字形图集（Glyph Atlas）——共享一张大纹理，按字形 UV 引用，详见 P9 规格。

### 二、功能缺口

#### 2.1 文本框剩余缺口（→ P7）

当前 `UITextBox` 已满足编辑器单行属性输入和搜索框的基本需求，剩余差距集中在多行排版、IME 组合态和输入校验：

| 功能类别 | 已实现 | 缺失 | 优先级 |
|---------|--------|------|--------|
| 光标导航 | 单行左右/Home/End、按词移动、鼠标定位、闪烁 | 多行上下、Ctrl+Home/End | P7-Medium |
| 文本选择 | 拖选、Shift 导航、Ctrl+A、选区高亮、Selection API | 双击/三击选择、多行选区 | P7-Medium |
| 编辑 | 插入、删除、选区替换、Ctrl+Backspace/Delete、Undo/Redo | Insert 覆写、多行编辑 | P7-Medium |
| 剪贴板 | Ctrl+C/X/V、`IClipboard` 平台抽象 | 无 | - |
| 显示 | Placeholder、Password、ReadOnly、MaxLength、水平滚动 | 禁用态、输入过滤/校验、对齐 | P7-Medium |
| 多行 | ❌ | 自动换行 + 垂直滚动 | P7-High |
| IME | 接收文本提交 | 组合态可视化、候选下划线、提交/取消回调 | P7-High |

**影响**：编辑器属性输入和搜索框可用；脚本编辑、多行文本和中文 IME 仍需后续迭代。

#### 2.2 控件覆盖率与剩余控件（→ P8/P9）

P8 编辑器刚需控件已落地，当前覆盖率约 70%。剩余缺口主要是资源显示、窗口化交互和高级输入控件：

| 缺失控件 | 阻塞的编辑器功能 |
|---------|----------------|
| `UIImage` | 资源预览、图标显示、工具栏按钮图标 |
| `UIWindow` | 设置窗口、多面板浮动与停靠 |
| `UIRadioButton` / `UISpinner` | 属性编辑器的互斥选择与数字步进 |
| `UITooltip` | 编辑器工具提示 |
| `UIListView`/`UITreeView` 虚拟化 | 大型资产库/场景树性能 |

#### 2.3 焦点与交互体验（已落地基础能力）

**P6 已实现**：
- ✅ Tab/Shift+Tab 焦点导航（深度优先收集 Focusable 元素，循环切换）
- ✅ 焦点环可视化（`UICanvas.Paint` 末尾绘制 2px 高亮边框）
- ✅ 点击空白区域取消焦点（`ClearFocus()`）
- ✅ `FocusedElement` 查询属性

**仍待后续**：
- ⏳ 双击事件、拖放事件、事件捕获
- ⏳ 更完整的全局快捷键和命令路由

### 三、渲染质量差距（→ P9）

| 特性 | 现状 | 目标 |
|------|------|------|
| 圆角 | ❌ 纯直角矩形 | 可配置 CornerRadius |
| 边框 | ❌ 无 | BorderThickness + BorderColor |
| 阴影 | ❌ 无 | BoxShadow（模糊/偏移/颜色） |
| 渐变 | ❌ 纯色 | LinearGradient / RadialGradient |
| 九宫格 | ❌ 无 | 9-slice 拉伸不变形 |
| DPI 缩放 | ❌ 无 | 高分屏适配 |
| 增量绘制 | ❌ 每帧全量重绘 | Dirty 脏标记跳过未变化子树 |
| 字体嵌入 | ❌ 系统字体回退 | 嵌入字体保证跨平台一致 |

### 四、架构层面差距（→ P10）

#### 4.1 无样式系统

当前 `UITheme` 仅是静态颜色常量集合（7个属性），控件颜色硬编码在各自类中：
- `UIButton.BackgroundColor` / `HoverColor` / `PressedColor` 各自定义
- `UITextBox.BackgroundColor` / `TextColor` 各自定义
- `UICheckbox.BoxColor` / `CheckColor` 各自定义
- `UISlider.TrackColor` / `FillColor` / `ThumbColor` 各自定义

无法统一换肤、无样式继承/覆盖、无状态样式绑定。

#### 4.2 无数据绑定

UI 与逻辑强耦合：`UIDemoOverlay` 中按钮点击直接修改 label.Text，
无法实现 MVVM 风格的 Model → View 自动同步。

### 五、代码质量问题

#### 5.1 命名空间冲突

`Spark.Engine.Math` 遮蔽 `System.Math`，所有 UI 代码需写全限定 `System.Math`（踩坑经验5）。
增加代码冗余和出错风险。建议：UI 文件顶部加 `using Math = System.Math;` 别名或调整命名空间结构。

#### 5.2 资源管理隐患

- `TextRenderer._textureIds` / `_textureSizes`：只增不减，无上限、无淘汰
- `UIRenderer._textures` / `_textureBindGroups`：只增不减，窗口关闭后未清理
- `UIManager._canvases`：窗口销毁后对应 Canvas 未移除

#### 5.3 错误处理不足

- `UIRenderer.AppendToGraph`：目标不存在时仅 Debug 日志，无明确反馈
- `UIManager.LoadSystemFont`：所有系统字体都找不到时抛 `InvalidOperationException`，无优雅降级
- 纹理上传失败无异常处理/重试机制

## P6~P10 详细规格

> 本节将设计文档中 ⏳ 项的粗略描述扩展为可执行的功能规格、接口设计和验收标准。

### P6：内容自适应布局 + 裁剪 + 焦点增强

#### 6.1 两阶段 Measure/Arrange

**目标**：子元素能根据自身内容报告期望尺寸，容器据此分配空间。

**接口变更**：

```csharp
// UIElement 新增
public virtual UISize Measure(UISize availableSize)
{
    // 默认实现：有 FixedSize 则返回 FixedSize，否则返回 (0,0) 表示 fill
    if (FixedSize is { } fs)
        return new UISize(
            fs.Width > 0 ? fs.Width : availableSize.Width,
            fs.Height > 0 ? fs.Height : availableSize.Height);
    return new UISize(0f, 0f);
}
```

**容器改造**（以 `UIStackPanel` 为例）：

```csharp
protected override void OnArrange()
{
    var content = ContentRect;
    bool vertical = Orientation == UIOrientation.Vertical;

    // Phase 1: Measure
    var measured = new List<(UIElement child, UISize desired)>();
    foreach (var child in Children)
    {
        if (!child.Visible) continue;
        var avail = vertical
            ? new UISize(content.Width, float.PositiveInfinity)
            : new UISize(float.PositiveInfinity, content.Height);
        measured.Add((child, child.Measure(avail)));
    }

    // Phase 2: 计算 fill 份额 + Arrange
    float fixedSum = 0f;
    int fillCount = 0;
    foreach (var (_, desired) in measured)
    {
        float main = vertical ? desired.Height : desired.Width;
        if (main > 0f) fixedSum += main; else fillCount++;
    }
    float spacingTotal = Spacing * Math.Max(0, measured.Count - 1);
    float leftover = Math.Max(0f, (vertical ? content.Height : content.Width) - fixedSum - spacingTotal);
    float fillShare = fillCount > 0 ? leftover / fillCount : 0f;

    float offset = vertical ? content.Y : content.X;
    foreach (var (child, desired) in measured)
    {
        float main = vertical ? desired.Height : desired.Width;
        float cross = vertical ? desired.Width : desired.Height;
        if (main <= 0f) main = fillShare;
        if (cross <= 0f) cross = vertical ? content.Width : content.Height;

        var rect = vertical
            ? new UIRect(content.X, offset, cross, main)
            : new UIRect(offset, content.Y, main, cross);
        child.Arrange(rect);
        offset += main + Spacing;
    }
}
```

**叶子控件 Measure 重写**：

```csharp
// UILabel
public override UISize Measure(UISize availableSize)
{
    if (FixedSize is { } fs) return base.Measure(availableSize);
    var size = /* ui.Text.Measure(Text) */;
    return new UISize(size.X + Padding.Left + Padding.Right, size.Y + Padding.Top + Padding.Bottom);
}
```

**向后兼容**：未重写 `Measure` 的子元素保持原有 fill 语义，已有代码无需修改。

**验收标准**：
- [x] `UILabel` 不设 `FixedSize` 时按文字内容自适应宽高
- [x] `UIButton` 不设 `FixedSize` 时按文字+Padding 自适应
- [x] `UIStackPanel` 混合固定/自适应子元素正确分配空间
- [x] `UIDemoOverlay` 去掉所有显式 `FixedSize` 后布局正确
- [x] 现有 Demo 不修改代码仍能正常运行（向后兼容）

#### 6.2 Scissor 裁剪

**接口**：

```csharp
// UIElement 新增
public bool ClipToBounds { get; set; }

// UIManager 新增裁剪栈
private readonly Stack<UIRect> _clipStack = new();
public void PushClip(UIRect rect);
public void PopClip();
public UIRect CurrentClip { get; }
```

**渲染侧**：`UIRenderer` 在绘制每个基元前，将当前裁剪区转为屏幕像素并调用
`RenderPassEncoderSetScissorRect`。裁剪区取 `_clipStack` 栈顶与基元所在 target 尺寸的交集。

**验收标准**：
- [x] `ClipToBounds = true` 的容器，子元素超出部分不可见
- [x] 嵌套裁剪正确（子裁剪区 ⊆ 父裁剪区）
- [x] ~~裁剪不影响 HitTest（HitTest 仍需检查裁剪区外的元素？→ 设计决策：HitTest 也受裁剪约束）~~ → P6-fix 已落地：`UIElement.HitTest` 在 `ClipToBounds && !Bounds.Contains(point)` 时整棵子树返回 null，超界元素不可命中（见 `ClipHitTestVerifyOverlay`）

#### 6.3 焦点导航增强

- Tab/Shift+Tab 按控件树顺序切换焦点（跳过 `Focusable=false` / `Visible=false`）
- 焦点环：`UICanvas` 在 `Paint` 末尾为 `_focused` 元素绘制 2px 高亮边框
- 点击空白区域取消焦点（`_focused?.OnFocusChanged(false); _focused = null;`）

**验收标准**：
- [x] Tab 循环切换所有 Focusable 控件
- [x] 焦点元素有可见高亮边框
- [x] 点击空白处焦点清除

#### 6.4 新增布局容器

| 控件 | 规格 |
|------|------|
| `UIGridPanel` | `Rows`/`Columns` 定义（固定/比例/Auto），子元素按 `(Row, Column)` 定位，支持 RowSpan/ColumnSpan |
| `UIWrapPanel` | 沿主轴排列，超出时自动换行到下一行/列；交叉轴尺寸取该行/列最大子元素 |

**验收标准**：
- [x] GridPanel 支持固定+比例+Auto 混合行列定义
- [x] WrapPanel 子元素自动换行，交叉轴正确包裹

### P7：文本框进阶

#### 7.1 文本选择

**当前实现字段/API**：

```csharp
private int _selectionAnchor;
private int _cursor;
```

**API**：

```csharp
public int SelectionStart { get; }
public int SelectionLength { get; }
public bool HasSelection { get; }
public void SelectAll();
public void ClearSelection();
```

**交互**：
- 鼠标拖拽：`OnMouseDown` 记录起点，`OnMouseDrag` 更新终点，`OnMouseUp` 确认选择
- Shift+方向键：从当前光标位置扩展选择
- Ctrl+A：全选
- 双击选词、三击选行（基于 `TextRenderer.Measure` 逐词/逐行定位）
- 选区高亮：在 `OnPaint` 中先画选区背景矩形，再画文字

**验收标准**：
- [x] 鼠标拖拽可选择任意范围文本
- [x] Shift+←/→ 扩展/收缩选择
- [x] Ctrl+A 全选
- [x] 选区有高亮背景色
- [x] 打字替换选中文本

#### 7.2 剪贴板

**核心库抽象**：

```csharp
// Spark.Engine/UI/IClipboard.cs
public interface IClipboard
{
    string? GetText();
    void SetText(string text);
}
```

**平台实现**：`Spark.Engine.Desktop` 中通过 Silk.NET 或平台 API 实现。

**快捷键**：
- Ctrl+C：复制选中文本
- Ctrl+X：剪切选中文本
- Ctrl+V：在光标处粘贴（替换选中文本）

**验收标准**：
- [x] Ctrl+C/X/V 与系统剪贴板互通
- [x] 无选中时 Ctrl+C/X 不操作
- [x] 粘贴替换选中文本

#### 7.3 Undo/Redo

**数据结构**：操作栈 `Stack<TextOperation>`，连续打字合并为一组。

```csharp
private enum TextOpType { Insert, Delete, Replace }
private record TextOperation(TextOpType Type, int Position, string OldText, string NewText);
```

**快捷键**：Ctrl+Z 撤销、Ctrl+Y 重做。

**验收标准**：
- [x] 单次打字/删除可撤销
- [x] 连续打字合并为一个撤销单元
- [x] 撤销后可重做
- [x] 新操作清空重做栈

#### 7.4 其他进阶功能

| 功能 | 规格 | 优先级 |
|------|------|--------|
| Ctrl+←/→ 按词移动 | 基于空格/标点分词 | High |
| Ctrl+Backspace/Delete | 删除前/后一个词 | High |
| Placeholder | `PlaceholderText` + `PlaceholderColor`，内容为空时显示 | Medium |
| MaxLength | 超出截断 | Medium |
| ReadOnly | 禁止编辑但可选中/复制 | Medium |
| Password 掩码 | `MaskChar`（默认 '●'），显示掩码但存储原文 | Medium |
| 超长文本滚动 | 内容宽度 > 控件宽度时水平偏移跟随光标 | Medium |
| 多行模式 | `Multiline=true` + Enter 换行 + 垂直滚动 | Low |
| IME 组合态 | 组合文本下划线高亮 | Low |

### P8：更多控件

#### 8.1 优先级排序

> 以下保留 P8 初始排期作为追踪记录；`UIScrollBox`、菜单、树、列表、下拉、对话框、标签页等已在 P8 落地，
> 当前剩余项见「当前问题与差距分析 §2.2」及分阶段计划。

| 优先级 | 控件 | 理由 |
|--------|------|------|
| High | `UIScrollBox` | 依赖 P6 裁剪；长列表/溢出内容的基础设施 |
| High | `UIImage` | 编辑器资源预览、图标显示 |
| High | `UIMenuBar` / `UIContextMenu` | 编辑器菜单系统 |
| Medium | `UITreeView` | 场景层级面板 |
| Medium | `UIListView` | 资产列表、搜索结果 |
| Medium | `UIComboBox` | 枚举属性选择 |
| Medium | `UIWindow` / `UIDialog` | 多窗口/模态交互 |
| Low | `UITabView` | 属性面板标签页 |
| Low | `UIProgressBar` | 加载/编译进度 |
| Low | `UIRadioButton` | 互斥选项 |
| Low | `UISpinner` | 数字步进 |
| Low | `UITooltip` | 悬停提示 |

#### 8.2 关键控件规格

**`UIScrollBox`**：
- 内容区 + 垂直/水平滚动条
- 依赖 P6 scissor 裁剪
- 滚轮事件驱动滚动
- 滚动条可拖拽
- `ScrollToTop()`/`ScrollToBottom()`/`ScrollIntoView(element)` API

**`UIImage`**：
- `Source`（纹理路径或 TextureId）
- `Stretch` 模式：None/Fill/Uniform/UniformToFill
- 九宫格（9-slice）：`SliceInsets` 定义四边切片宽度
- `TintColor` 着色

**`UIMenuBar` / `UIContextMenu`**：
- `MenuItem`：Text + Shortcut + Icon + SubItems + Action
- 分隔线（Separator）
- 弹出层（依赖 P6 裁剪 + 层级管理）
- 键盘导航（↑↓ 选择、←→ 展开/收起子菜单、Enter 执行、Esc 关闭）

**`UITreeView`**：
- `TreeNode`：Text + Icon + Children + IsExpanded
- 展开/折叠动画（可选）
- 单选/多选模式
- 虚拟化（仅渲染可见节点，依赖 `UIScrollBox`）

**`UIListView`**：
- 虚拟化渲染（仅创建可见项的 UI 元素）
- 单选/多选
- `ItemTemplate` 自定义项外观
- 排序/过滤（数据层）

### P9：渲染打磨

#### 9.1 字形图集（Glyph Atlas）

**目标**：替代字符串级纹理，共享一张大纹理，按字形 UV 引用。

**设计**：
- 图集纹理尺寸：1024×1024（可配置），R8 或 RGBA8
- 字形缓存：`Dictionary<(char, fontSize), GlyphInfo>`，`GlyphInfo` 含 UV rect + advance + bearing
- 排版：逻辑线程按字形查图集 → 拼装四边形序列（每字形一个 quad）
- 图集满时：扩容或分页（多张图集）
- 嵌入字体：打包 `.ttf`/`.otf` 到程序集资源，保证跨平台一致

**验收标准**：
- [ ] 动态文本不再创建新纹理
- [ ] 相同字号相同字符复用同一字形
- [ ] 编辑器场景纹理数量恒定（≤ 图集数）
- [ ] 跨平台字体渲染一致

#### 9.2 视觉效果增强

| 特性 | 实现方式 | 验收标准 |
|------|---------|---------|
| 圆角 | Shader 中 SDF 圆角或预渲染圆角纹理 | 四角平滑过渡，无锯齿 |
| 边框 | 额外四边形或 Shader 描边 | 内外边界清晰 |
| 阴影 | 预渲染模糊纹理或 Shader box-shadow | 偏移/模糊/颜色可配 |
| 渐变 | Shader 线性/径向插值 | 起止色/方向/中心可配 |
| 九宫格 | 9 个四边形拼合，四角固定尺寸 | 拉伸时四角不变形 |

#### 9.3 增量绘制

- `UIElement` 新增 `bool IsDirty` 属性
- 属性变更时标记自身及祖先为 dirty
- `Paint` 时跳过非 dirty 子树（缓存上帧基元）
- 布局变更（Arrange 结果不同）自动标记 dirty

**验收标准**：
- [ ] 静态 UI 帧间零基元产出
- [ ] 单控件变更仅重绘该子树
- [ ] 布局变更后全子树重绘

#### 9.4 DPI 缩放

- `UICanvas` 新增 `float DpiScale` 属性
- 布局/绘制坐标乘以 DpiScale
- 字体大小按 DPI 缩放
- 窗口 DPI 变更时重新布局

**验收标准**：
- [ ] 150%/200% DPI 下 UI 大小正确
- [ ] 文字清晰不模糊
- [ ] 运行时 DPI 切换即时生效

### P10：主题样式系统 + 数据绑定

#### 10.1 样式系统

**设计**：

```csharp
// 样式定义
public class UIStyle
{
    public Vector4? BackgroundColor { get; set; }
    public Vector4? TextColor { get; set; }
    public float? CornerRadius { get; set; }
    public float? BorderThickness { get; set; }
    public Vector4? BorderColor { get; set; }
    public Font? Font { get; set; }
    public float? FontSize { get; set; }
    // ... 可扩展
}

// 样式应用
public class UIElement
{
    public UIStyle? Style { get; set; }       // 直接样式
    public string? StyleClass { get; set; }   // 样式类名
    // 解析优先级：直接样式 > StyleClass > 控件默认样式 > UITheme
}

// 样式表
public class UIStyleSheet
{
    public UIStyle? this[string className] { get; set; }
    public UIStyle? this[Type controlType] { get; set; }
}
```

**验收标准**：
- [ ] 控件可通过 Style 属性覆盖外观
- [ ] StyleClass 匹配样式表中的类定义
- [ ] 暗/亮主题切换一键生效
- [ ] 样式变更即时反映

#### 10.2 数据绑定

**设计**：

```csharp
// 简易绑定
public class UIBinding<TSource>
{
    public Func<TSource, object> Getter { get; }
    public Action<TSource, object>? Setter { get; }
    public string PropertyPath { get; }
}

// 控件绑定
button.Bind(nameof(UIButton.Text), viewModel, vm => vm.Label);
slider.Bind(nameof(UISlider.Value), viewModel, vm => vm.Volume, (vm, v) => vm.Volume = v);
```

**验收标准**：
- [ ] Model 属性变更自动更新 UI
- [ ] 双向绑定：UI 变更回写 Model
- [ ] 绑定解除后无内存泄漏

## 已知限制 / 后续（P6+）

> 逐控件的功能全集（含文本框复制/粘贴/选择/词删除、剪贴板、IME、Undo/Redo 等）见上文
> 「控件清单与功能规划」，此处只列结构性限制。

- **文字渲染**：四处 bug 已修复（见踩坑 1/2/2b/6），画面已复跑确认。
- ~~**单遍布局无内容自适应**~~：✅ P6 已解决（两阶段 Measure/Arrange）。
- **字符串级文本纹理**：每段文本一张纹理，纹理数随字符串数增长，需换字形图集 + 嵌入字体（跨平台一致）。
  → 详见「当前问题与差距分析 §1.3」和「P9 §9.1」。
- ~~**无裁剪**~~：✅ P6 scissor 裁剪已实现；✅ P8 `UIScrollBox` 与 Overlay 下拉/菜单/对话框已实现。
- **文本框仍为单行实现**：选择、剪贴板、Undo/Redo、掩码等已实现，多行和 IME 组合态仍待 P7。
  → 详见「当前问题与差距分析 §2.1」和「P7」。
- **控件覆盖率约 70%**：编辑器核心 Tree/List/Menu/Dialog/Tab/Combo/PropertyGrid 已具备，
  `UIWindow`、`UIImage`、Radio/Spinner/Tooltip 和虚拟化仍待补。
  → 详见「当前问题与差距分析 §2.2」和「P8」。
- **无样式系统/数据绑定**：外观硬编码，UI 与逻辑强耦合。
  → 详见「当前问题与差距分析 §四」和「P10」。

## Overlay 弹出层机制（P8，2026-08-26）

菜单下拉、对话框遮罩等需要**覆盖在兄弟元素之上**且**不参与布局流**的控件，通过
`UICanvas.Overlays` 弹出层实现：

- **注册**：`UIDialog.Show()` / `UIMenuPanel.Show(position)` 自动把自身加入 `canvas.Overlays`
  （经 `UIElement.Canvas`/`FindCanvas()` 定位画布）；`Close()` 移除。
- **布局**：Overlay 每帧由 `UICanvas.Update` 直接铺满画布（`Measure(canvasSize)` + `Arrange(fullRect)`），
  元素内部自行定位（菜单按 `Position` 弹出于指定坐标，对话框居中）。
- **绘制**：`UICanvas.Paint` 在 Root 之后绘制可见 Overlay（后注册的在上层）。
- **命中**：`UICanvas.RouteInput` 的 `HitTestTop` 先测 Overlay（倒序），再测 Root——
  对话框遮罩拦截底层点击（模态），菜单只有弹出矩形内可点。
- **TextRenderer/Canvas 注入**：`UICanvas.Update` 对每个可见 Overlay 同样注入
  `LayoutTextRenderer` 与 `Canvas`，保证弹出层内文本可测量/绘制。

**滚轮事件**：`UIElement.OnMouseWheel(float delta)` 虚方法 + `UICanvas.RouteInput` 把滚轮
沿 hovered 元素祖先链向上冒泡（`ScrollDelta` 来自 `InputState`，Windows 标准 ±120）。

`UIScrollBox` 滚动条拖拽在当前命中体系下需点击滚动条本体（拖拽由 `OnMouseDrag` 驱动）。

## 分阶段计划（现状）

| 阶段 | 内容 | 状态 | 关键交付物 |
|---|---|---|---|
| P0 | 输入系统（Key/MouseButton/WindowInput/InputState/InputManager） | ✅ | 平台无关输入抽象 |
| P1 | UI 渲染核心（UIPrimitive/UIRenderer overlay/UI.wgsl/UseUI） | ✅ | 多纹理分批 overlay pass |
| P2 | 控件树 + 布局（UIElement/UICanvas/UIPanel/Stack/Dock） | ✅ | 保留模式控件树 + Measure/Arrange |
| P3 | 字体/文本（TextRenderer 字符串级 + UILabel + 多纹理渲染器） | ✅ | 字符串级文本渲染 |
| P4 | 交互（HitTest + 事件路由 + UIButton/UITextBox v1） | ✅ | 鼠标/键盘事件路由 |
| P5 | 完整控件 + 主题 + 编辑器接入（UICheckbox/UISlider/UITheme/EditorLayout） | ✅ | 编辑器骨架 |
| P6 | 内容自适应布局 + 裁剪 + 焦点增强 + Grid/Wrap | ✅ | Measure/Arrange、scissor、Tab导航、GridPanel、WrapPanel |
| P7 | 文本框进阶（多行、IME 组合态、输入校验） | 🔶 部分 | 单行选择/剪贴板/Undo/Redo/掩码已落地；多行与 IME 组合态待补 |
| P8 | 编辑器控件与 Overlay（ScrollBox/Menu/Tree/List/Combo/Dialog/Tab 等） | 🔶 部分 | 编辑器刚需控件已落地；Image/Radio/Spinner/Tooltip/Window/虚拟化待补 |
| P9 | 渲染打磨（字形图集/嵌入字体/圆角边框阴影/九宫格/增量绘制/DPI） | ⏳ | 高质量渲染 + 性能优化 |
| P10 | 主题样式系统 + 数据绑定 + 无障碍/本地化 | ⏳ | 可定制外观 + MVVM 支持 |

> 说明：P0~P6 已实现并通过本机 GPU 复跑；P7/P8 为部分落地，P9/P10 为后续扩展。
> 具体功能逐条见「控件清单与功能规划」，实施规格见「P6~P10 详细规格」。

## 关联文档

- [RenderPipeline-Design.md](./RenderPipeline-Design.md) — 渲染管线（RenderGraph/pass/overlay 集成点）
- [SceneSync-Design.md](./SceneSync-Design.md) — 场景同步（SceneSnapshot 值快照通道）
- [RenderGraph-Design.md](./RenderGraph-Design.md) — 帧图（overlay pass 挂接）
