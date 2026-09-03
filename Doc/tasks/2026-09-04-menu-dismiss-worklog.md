# 弹出菜单外部点击关闭工作日志

日期：2026-09-04

## 问题

`UIMenuPanel` 作为 Overlay 只在弹出矩形内参与命中测试。点击其它区域时事件会落到 Root，
但没有统一的菜单收起步骤，导致菜单一直保持打开状态。

## 本轮完成

- 在 `UICanvas.RouteInput` 的左键按下阶段检测所有可见 `UIMenuPanel`，点击其弹出矩形外时调用 `Close()`。
- 关闭后同一帧继续对 Root 做命中和点击路由，因此点击另一个按钮/菜单不会丢失操作。
- `UIMenuPanel.Close()` 会清理菜单项焦点，避免弹层移除后画布仍持有已不可见的焦点元素。
- 新增菜单外部点击回归测试，覆盖 Overlay 移除和 `Visible` 状态。

## 验证

- 菜单外部点击测试通过。
- `dotnet test Tests/Spark.Engine.Tests/Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false`：`286/286` 通过。
- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false /p:OutDir="$env:TEMP/SparkEngine-DemoDesktop-Verify-20260904c/"`：0 警告、0 错误。
