# 任务工作记录（Worklog）

> 记录「编辑器控件 P8 + 布局全面审计与修复」实现与调试会话（2026-08-31）。
> 范围：10 个编辑器控件落地 + Overlay 弹出层 + 布局全面审计（4 路并行子代理）+ 12 处布局/文本/滚动缺陷修复。
> 验证：42 个单元测试全通过（含回归锁定）、全解决方案 0 警告 0 错误、Demo 冒烟运行正常。

## 概述

延续 P8 编辑器控件轮（2026-08-26 已落地控件实现 + Overlay 机制 + 验收页面），本轮聚焦：

1. **验收驱动的问题修复**：用户逐场景目视验收时发现的一系列布局/交互缺陷，逐个定位根因并修复。
2. **布局全面审计**：启动 4 个并行子代理审计全部 UI 控件文件（基础/容器/列表树菜单/复合），
   结合自查，系统性修复 Measure/Arrange/Paint 不一致、文本溢出、滚动裁剪失效等问题。
3. **回归测试锁定**：新增 13 个单元测试（共 42 个），把每个修过的根因用测试永久锁定。

## 一、编辑器控件轮遗留问题修复（验收驱动）

### 1. SplitPanel 退化尺寸抛异常
- **现象**：面板尺寸小于分割条+最小面板时抛 `ArgumentException: '50' cannot be greater than -55`。
- **根因**：`Math.Clamp(value, MinFirstSize, availableSize - MinSecondSize)` 在 `availableSize` 为负时
  `min > max` 抛异常（`OnArrange`/`OnPaint`/`GetSplitterRect`/`OnMouseDrag` 四处）。
- **修复**：`ComputeFirstSize` 统一钳位——`availableSize ≤ 0` 返回 0；最小尺寸钳到可用空间内
  （`min = Min(MinFirstSize, availableSize)`、`max = availableSize - Min(MinSecondSize, availableSize)`，
  且保证 `max ≥ min`）。拖拽同样防护（`availableSize ≤ 0` 直接 return）。
- **测试**：`SplitPanel_TinyBounds_NoThrow` / `SplitPanel_Vertical_TinyHeight_NoThrow`。

### 2. SplitPanel 分割条拖不动
- **现象**：悬停变色正常，按住左键拖动无反应（无异常）。
- **根因**（时序缺陷）：`_hoveringSplitter` 只在 `OnMouseDrag` 里更新，而 `OnMouseDrag` **只在按下后**调用；
  按下时 `OnMouseDown` 检查 `_hoveringSplitter`（此时还是 false）→ `_dragging` 永远置不上。
- **修复**：`UIElement` 新增 `OnMouseMove(position)` 虚方法（未按下时也通知 hover 移动）；
  `UICanvas.RouteInput` 每帧对 hovered 元素调用；`UISplitPanel.OnMouseMove` 更新悬停态，按下时已有正确值。

### 3. Dialog 无法关闭
- **现象**：点按钮/按 Escape/Enter 都不关闭。
- **根因**：三条关闭路径全断——`OnMouseClick()` 是空实现（`_hoveredButton` 更新了但没用）；
  dialog 非 `Focusable`（键盘事件路由不到）；`_hoveredButton` 只在按下后更新（无 hover 反馈）。
- **修复**：`OnMouseClick` 按 `_hoveredButton` 触发按钮回调 + `Close`；构造设 `Focusable = true`；
  `OnMouseMove` 同步更新 hover。

### 4. Toolbar/Menu 左上角文字重叠
- **现象**：Toolbar 和 Dialog 页左上角多个字母叠在一起。
- **根因**：`UIToolbar` 和 `UIMenuBar` **没有重写 `OnArrange`**——内部 `_itemsPanel` 从不布局，
  `Bounds` 保持 `(0,0,0,0)`，所有按钮/菜单项叠在左上角。
- **修复**：两个复合控件补 `OnMeasure`（先测内部面板）+ `OnArrange`（面板铺满 `ContentRect`）；
  顺带修 `UIListView`/`UITreeView`/`UIPropertyGrid`——`OnArrange` 用 `Bounds` 而非 `ContentRect`，
  且 `OnMeasure` 未让内部 `_scrollBox` 测量（滚动范围算不出）。

### 5. Toolbar 按钮太窄文字溢出
- **现象**：按钮固定宽 28px，文字（如 "Open"）溢出相邻按钮重叠。
- **根因**：`UIToolbarButton.OnMeasure` 返回固定 `ButtonSize`，完全忽略文本宽度。
- **修复**：宽度 = `文本宽 + Padding`（下限 `ButtonSize`）；加默认水平 Padding 6px。

### 6. 同字号按钮高度不一致 / 文字基线不对齐
- **现象**：导航按钮高度不一样；Toolbar 按钮文字垂直未对齐。
- **根因**：多处用 `TextRenderer.Measure(text).Y`（**墨水盒高**，随文本字符变化）当行高/垂直居中基准。
- **修复**：`TextRenderer` 新增 `LineHeight`（含 line gap 的真实行高）；布局高度与垂直居中全部改用
  `LineHeight`（水平宽度仍用墨水宽）。

### 7. Grid 行缝里有文字
- **现象**：Grid 验收页行 0/行 1 的缝隙里出现文本。
- **根因**（两个叠加）：
  a) `UIGridPanel.OnMeasure` 在 FixedSize 双分量>0 时提前返回，跳过 Auto 轨尺寸收集 → Auto 行高塌陷为 0；
  b) `UILabel`/`UIButton` 多行文本（`\n`）高度只算单行，绘制却画两行。
- **修复**：Grid 无论是否有 FixedSize 都先收集 Auto 尺寸；`TextRenderer` 新增 `MeasureBlock(text)`
  （宽=最宽行、高=行数×行高），`UILabel`/`UIButton` 改用。

### 8. 三级页面返回直接回首页
- **现象**：EditorControls 子场景点 "Back" 直接回 VerifyHub，而不是回二级列表页。
- **根因**：`EditorControlsVerifyOverlay.BackBar` 的返回按钮写死 `switchTo(VerifyHub.Build(...))`。
- **修复**：`BackBar` 加可选 `backTo` 参数，三级场景传 `() => EditorControlsVerifyOverlay.Build(switchTo)`。

### 9. 切换页面闪烁露出 3D
- **现象**：切换页面时闪一帧，能看到底部 3D 场景。
- **根因**（帧时序）：`Update` 先布局后 `RouteInput`；按钮点击在 `RouteInput` 里替换 `Root`，
  本帧 `Paint` 遍历未布局的新 Root（Bounds 全 0）→ UI 空白一帧。
- **修复**：`UICanvas.Update` 在 `RouteInput` 后检查 `Root != _lastLayoutRoot`，同帧立即补布局。

### 10. Grid 右侧贴窗口边缘
- **现象**：Grid 页面右侧与窗口无间距。
- **根因**：`UIStackPanel.OnMeasure` 给子元素的约束未减自身 Padding → fill 子元素（含 Star 列 Grid）
  按未减 padding 宽度测量 → Arrange 时溢出内容区。
- **修复**：`UIStackPanel`（以及 `UIMenu`/`UIToolbar` 内部面板）Measure 约束减 Padding。

## 二、布局全面审计（4 路并行子代理 + 自查）

启动 4 个后台子代理并行审计全部 UI 控件（基础/容器/列表树菜单/复合），汇总后按根因分类修复：

### 基础控件组
- `MeasureBlock` 多行宽度曾把含 `\n` 整串当单行测量 → 改逐行取 Max。
- `UITextBox` 光标高度用 `Measure(" ")`（空格无墨水 ≈2px）→ 改 `LineHeight`。
- `UICheckbox` 文本垂直居中用墨水高 → 改 `LineHeight`；方框居中忽略 Padding → 相对内容区。

### 容器组
- **`UISplitPanel` 无 `OnMeasure`** → 整棵子树从不测量、DesiredSize 恒 0 → 补 OnMeasure。
- **`UIGridPanel` Star 剩余未扣 Padding** → Measure 与 Arrange 基准不一致、DesiredSize 超约束 → 修正。
- **`UIScrollBox` 交叉轴约束未扣 Padding + FixedSize 早退未测内容** → 修正。
- **`UIWrapPanel` 换行阈值未扣 Padding + fill 尺寸 0 vs 20 不一致 + FixedSize 早退** → 修正。
- **`UIStackPanel`/`UIDockPanel` FixedSize 早退跳过子测量** → 先测子元素再返回。
- **`UIDockPanel` Measure 忽略 Fill 子元素交叉轴期望** → 补。

### 列表树菜单组
- `UIMenuItem.OnMeasure` 恒返回 0 宽 → 面板宽度恒 MinWidth → 改上报文本宽（含快捷键）。
- `UIScrollBox` 滚动条画在内容之下（被不透明项盖住）→ `UIElement` 新增 `OnPaintOverlay` 后置钩子。
- `UIMenuPanel` 的 `_itemsPanel` 死布局 → 同步 Arrange。
- `UIToolbar`/`UIMenuBarItem` 高度不随容器拉伸 → 改 fill（交叉轴拉伸）。
- `UITreeView` 深缩进负宽 + 左方向键死分支（视觉 Parent ≠ 逻辑父）→ 钳位 + `LogicalParent`。
- `UIMenuPanel` 首次 Show 一帧错位 → Show 内立即 Measure/Arrange。

### 复合控件组
- 文本截断从「字符数比例」改为 `TextRenderer.Truncate`（逐字符测量，非等宽字体不超宽）。
- `UIComboBox` 下拉项超 `MaxDropDownHeight` 仍绘制/命中 → 只处理可见数量。
- `UITabView` 标签总宽超栏时静默裁剪 → 回退均分。
- `UIDialog` 长消息横向溢出 → `Truncate`。

### 滚动裁剪（重点，见下节）
- 滚动容器内容越出视口可见的根因与修复单独记录。

## 三、滚动裁剪失效（本会话最重要的发现）

### 现象
用户明确：**滚动逻辑没问题，但内容越出 ListView/ScrollBox 边框可见**（本应被裁掉）。

### 定位过程
1. 逻辑层测试证明滚动数学正确（滚动到顶/底内容边界对齐视口）。
2. 端到端 Paint 测试发现：**完全滚出视口的内容项（如文本 Y=-187 或 Y=695，视口 [8,308]）的
   scissor 与视口交集为空**。
3. **根因**：`UIManager.Intersect` 对空交集返回 `(x, y, 0, 0)`，与「无裁剪」默认值 `(0,0,0,0)`
   **无法区分**。渲染层 `UIRenderer.DrawBatch` 见 `Z≤0||W≤0` 判定「无裁剪」→ 重置为全视口。
   而这些项的 NDC 坐标**部分落在 [-1,1] 内**（如 `Y=-7` 项 bottom=0.965）→ GPU 只裁 [-1,1] 外部分 →
   **视口上方/下方那几像素内容被画出来**。
4. **修复**：`Intersect` 对空交集返回**负尺寸** `(x, y, -1, -1)` 作为「完全裁剪」标记；
   `DrawBatch` 检测 `Z<0||W<0` → 跳过该批。语义从此清晰：**null=无裁剪、正尺寸=部分裁剪、负尺寸=完全裁剪**。

### 测试
- `ScrollBox_Scrolled_PrimitivesHaveScissorClip` / `ListView_Scrolled_PrimitivesHaveScissorClip` /
  `ScrollBox_DemoScenario_AllPrimitivesClippedToViewport`（断言没有任何内容基元「无裁剪」）。

## 四、文本高度稳定性（用户验收驱动）

### 现象
Toolbar 点击按钮后状态文字变化，导致 Toolbar 整体下移。

### 根因
`TextRenderer.MeasureBlock` 高度曾用 `max(行数×LineHeight, Measure(text).Y)`——`Measure(text).Y` 是
**该文本的墨水盒高**（随字符变化：含 descender 的 "button" 更高），导致同字号不同文本布局高度波动。

### 修复
`MeasureBlock` 高度改回**固定** `行数 × LineHeight`（与文本内容无关）。LineHeight 已含 line gap，
行框足以容纳墨水（端到端测试验证墨水底部 ≤ 布局底部 + 2px 余量，不裁剪）。

### 潜在风险验证（用户问「会不会裁底部」）
- 端到端验证：单行含 descender（"agyp"）、两行（"line1\nline2"）的文本基元底部 ≤ Label 底部 + 2px。
- 公式验证：`LineHeight = (三行墨水盒 - 单行墨水盒)/2` 在 12/16/24/32 字号下都满足
  `LineHeight ≥ 单行墨水`、`n×LineHeight ≥ n 行墨水`。
- 结论：**布局高度固定不波动，同时不裁剪墨水**。

## 五、遗留待办

- P8 缺控件：Image / ProgressBar / RadioButton / Spinner / Tooltip / Window。
- 样式系统初版（P10）、文本框进阶（P7）、字形图集（P9）。
- `UITextBox` 超长文本水平滚动（当前 ClipToBounds 截断）。
- EditorControls 验收 9 个子场景 + 原 4 个验收场景仍待用户 GPU 逐场景确认。

## 验证状态

| 项 | 状态 |
|---|---|
| 单元测试 | ✅ 42/42（含 13 个本轮新增回归） |
| 全解决方案构建 | ✅ 0 警告 0 错误 |
| Demo 编译 | ✅ |
| Demo 冒烟 | ✅ 运行 8s 无崩溃 |
| 用户 GPU 验收 | ⏳ 待用户逐场景确认 |
