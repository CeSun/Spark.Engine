# Details 面板浮动窗口工作日志

日期：2026-09-04

## 目标

让 Details 面板遵循 UE 编辑器习惯：标题栏拖动到窗口外后，可脱离主布局成为独立原生窗口；关闭浮动窗口后自动回到主布局。

## 本轮完成

- 新增 `UIDragHandle`，标题栏拖动超过 8px 才触发抽离，普通点击不会误触发。
- Inspector 标题栏接入拖拽句柄；编辑器通过 `WindowManager` 创建独立 Details 原生窗口，并为其分配独立 `UICanvas`。
- 浮动窗口定位使用主窗口屏幕坐标加拖拽鼠标位置（鼠标落在窗口标题附近），不再依赖系统默认随机位置。
- 主布局在抽离后将 `UISplitPanel` 切换为单面板模式，让 Viewport 自动铺满空出的区域；浮动窗口关闭后恢复原分割布局。
- 复用现有输入、渲染目标和 UI 生命周期，不修改 World/Selection 数据模型。
- 新增拖拽阈值回归测试；Outliner 与 Content Browser 的抽离入口预留给后续 Dock/Layout 阶段统一接入。
- `UISplitPanel.SetPanels` 支持第二面板为空，单面板时隐藏分割条并填满可用空间。
- Outliner 和 Content Browser 标题栏也接入相同拖拽句柄，可分别抽离为独立窗口；关闭后恢复到原停靠区域。
- 浮动窗口位置由主窗口屏幕坐标与拖拽鼠标坐标计算，鼠标保持在新窗口标题附近。

## 已知边界

- 当前已接入 Details、World Outliner 和 Content Browser；任意面板停靠、跨窗口拖动预览和布局持久化仍将在 E6 工作区阶段继续实现。

## 验证

- `dotnet test Tests/Spark.Engine.Tests/Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false`：`288/288` 通过。
- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false /p:OutDir="$env:TEMP/SparkEngine-DemoDesktop-Verify-20260904e/"`：0 警告、0 错误。
