# 任务工作记录（Worklog）

> 记录「P6-fix 补丁轮」的实现会话：修复文档标 ✅ 但实际未达标的项 + 文档回刷 + 验收场景就绪。
> **验收状态：✅ 已验收**（2026-08-31 由用户运行 Demo 逐场景目视确认通过，未发现问题）。

## 背景

对照 `Doc/UI-System-Design.md` 与源码逐条核对后发现：P6 在分阶段计划里被标 ✅，但若干验收标准实际未达标，
文档「本文与当前代码同步」的声明部分失真。本轮按「P6-fix 全套」范围修复这些项，并补齐验收用 Demo Overlay。

## 修复项

### 1. UIGridPanel（`Src/Spark.Engine/UI/UIGridPanel.cs`，重写）
- **Auto 尺寸传递**：旧版 `OnMeasure` 收集了 Auto track 尺寸，但 `OnArrange.ResolveSizes` 把 Auto 一律置 0
  （源码挂着 `// TODO: 完善 Auto 尺寸的传递`），导致 Auto 单元格塌陷为 0 像素、子元素不可见，文档却标 ✅。
  修复：Measure 把 Auto 尺寸缓存到实例字段 `_measureRowAutoSizes` / `_measureColAutoSizes`，Arrange 直接复用。
- **RowSpan / ColumnSpan**：新增 `SetRowSpan`/`SetColumnSpan`（默认 1），Arrange 合并多轨为联合矩形（含中间 spacing）。
- **附加属性实例化**：`_rows`/`_cols` 由 `static Dictionary` 改为实例字段（修复元素销毁后条目永不回收 + 多 Grid 串数据）。
- **Star 扣 spacing**：Star 剩余空间扣除 `CellSpacing*(n-1)`（旧版未扣，track 总宽会超出 content rect）。
- 已知限制：span>1 的子元素不参与 Auto 轨尺寸计算；附加属性仅对直接子元素生效。

### 2. UIElement（`Src/Spark.Engine/UI/UIElement.cs`）
- **RemoveChild / ClearChildren**：新增（旧版只有 AddChild，无法干净移除子元素）。
- **重挂自动摘除**：`AddChild` 检测到 `child.Parent` 已存在且非 this 时，先从旧父 `_children` 摘除，避免双份布局/绘制/事件。
- **环检测**：自挂自抛 `InvalidOperationException`；沿祖先链上行命中 child 也抛（防 `A→B→A` 死循环）。
- **HitTest 受 ClipToBounds 约束**：P6.2 验收标准的设计决策「HitTest 也受裁剪约束」落地——
  `HitTest` 在 `ClipToBounds && !Bounds.Contains(point)` 时整棵子树返回 null，超界元素不可命中。
- **Paint try/finally**：裁剪栈 push/pop 用 try/finally 保证异常时平衡。

### 3. 裁剪栈按 targetId 隔离（`Src/Spark.Engine/UI/UIManager.cs`）
- `Stack<UIRect>` → `Dictionary<int, Stack<UIRect>>`；`PushClip`/`PopClip`/`CurrentClip` 均带 `targetId` 参数。
- 修复多窗口/多 overlay pass 连续 Paint 时前一个画布的 PushClip 拖留污染后一个画布的问题。
- `DrawRect` 取对应 target 的 clip；`UIElement.Paint` 与 `TextRenderer.DrawText` 同步改签名。

### 4. TextRenderer 全墨水包围盒（`Src/Spark.Engine/UI/TextRenderer.cs`）
- **踩坑6 只修了底/右**：旧版纹理尺寸 `ceil(Right)+1 / ceil(Bottom)+1` + `Origin=(0,0)` 不变，
  `bounds.Left`/`bounds.Top` 为负（斜体左侧悬突、`Å/É` 等 ascender 超出线高）时像素仍被裁。
- 修复：纹理覆盖全包围盒 `ceil(Right-Left)+2 × ceil(Bottom-Top)+2`，四向各 1px 抗锯齿余量；
  绘制 `Origin` 平移到 `(1-Left, 1-Top)`；新增 `_textureOffsets`，`DrawText` 用偏移 `(Left-1, Top-1)` 放置四边形，墨水精准落位。

## 验收场景（Demo，交互式）

新增 5 个文件，主窗口默认挂 `VerifyHub`，按钮切换场景（点击在 RouteInput 阶段触发，下一帧新 Root 生效）：

| 文件 | 验收点 |
|---|---|
| `Demo/Demo/VerifyHub.cs` | 入口 Hub：5 个按钮切换场景 |
| `Demo/Demo/GridPanelVerifyOverlay.cs` | Auto 单元格紧贴内容（旧版塌陷）；colSpan=2 合并两轨；Star 行拉伸 |
| `Demo/Demo/ClipHitTestVerifyOverlay.cs` | 单层裁剪：超界按钮不可点；嵌套裁剪：外∩内命中区 |
| `Demo/Demo/TreeOpsVerifyOverlay.cs` | Toggle child / Re-parent / 自挂自 / 造环（异常被捕获并显示） |
| `Demo/Demo/TextBoundsVerifyOverlay.cs` | ascender/descender 完整；两行同文水平共线 |

接入：`Demo/Demo/DemoApp.cs` 把 `uiCanvas.Root = P6VerifyOverlay.Build()` 改为 `VerifyHub.Build(switchTo)`，
`switchTo` 委托清焦点 + 换 Root。屏幕文案全部英文（系统字体无 CJK 字形）。

## 验收状态

⚠ **已验收（2026-08-31 补记）**。本轮完成：
- 代码改动落地（11 个文件：6 改 + 5 新）。
- `dotnet build D:\Spark.Engine\Spark.Engine.slnx` 全量编译通过（0 错 0 警；bin/obj 之外的源码无 warning）。
  （注：首次全量构建报 6 个 copy 错误是因为 `Demo.Desktop (PID 11932)` 正在运行锁住输出 DLL，非代码问题；
  单独编 `Demo.csproj` 干净通过。）

**已于 2026-08-31 由用户在本机运行 `dotnet run --project Demo\Demo.Desktop` 逐场景目视确认通过**：
- 4 个验收 Overlay 的交互预期（计数器增减、异常标签文案、字形完整）均经人眼核对，未发现回归。

## 文档同步

`Doc/UI-System-Design.md`：
- 状态头改写：明确区分「P6 原轮已 GPU 复跑」vs「P6-fix 仅编译验证、未验收」。
- 控件清单 `UIGridPanel` 行补 RowSpan/ColumnSpan、Auto 修复说明。
- P6.2 裁剪验收标准：HitTest 受裁剪约束从待定改为 ✅ 已落地。
- 踩坑经验新增 6b（文字顶部/左侧裁切）、7（Grid Auto 塌陷）、8（附加属性静态字典泄漏）、9（裁剪栈单例）、10（树操作不完整），每条标注对应验收 Overlay。

## 未含（明确边界）

按既定优先级，本轮只做 P6-fix 全套。未含：Slider/Checkbox setter 发变更通知（pre-P10）、
MSAA/sRGB/统一上传等 P9 渲染打磨、文本框 P7 进阶、新控件 P8。这些在文档里已有规划，需另开轮次。
