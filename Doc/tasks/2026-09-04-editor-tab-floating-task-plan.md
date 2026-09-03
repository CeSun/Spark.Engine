# 编辑器 Tab 面板浮动任务梳理

日期：2026-09-04  
关联提交：`68264c3 feat(editor): detach outliner and viewport tabs`

## 目标

按照 UE 编辑器的 Tab 面板习惯，允许用户从 Outliner 和 Viewport 的具体标签发起拖动：
指针移动超过 8px 后，将当前标签对应的面板脱离为独立原生窗口；关闭浮动窗口后自动恢复原停靠布局。

## 任务清单与状态

### 本次已落地

- [x] `UITabView` 记录按下的具体标签，并以 8px 阈值触发 `TabDragStarted`。
- [x] Outliner 按标签粒度抽离；多标签时保留其他标签，避免整个宿主被移出。
- [x] Outliner 只有标签可拖出，面板标题仅作标识，避免重复拖动入口。
- [x] Viewport/资源编辑器支持 Scene、StaticMesh、Material、Texture 等文档标签独立浮动。
- [x] 最后一个 Outliner/Viewport 标签被抽离时，主布局切换为单面板并由剩余控件铺满。
- [x] 浮动窗口使用独立 `UICanvas`，窗口位置以主窗口坐标加拖动鼠标位置计算。
- [x] 浮动窗口关闭后恢复原 Tab、Panel 和分割布局；失败时执行回滚。
- [x] 增加具体 Tab 拖动回归测试，并完成全量测试与 Desktop 构建验证。
- [x] 更新 UI 系统设计文档和浮动面板工作日志。

### 验收标准

- [x] 点击标签但移动距离小于 8px 时，不创建浮动窗口。
- [x] 拖动标签超过 8px 时，抽离的是被拖动的标签，而不是当前选中标签或整个宿主。
- [x] 多标签宿主在抽离后仍可切换、关闭和创建其他标签。
- [x] 抽离最后一个标签时，主区域没有空白，剩余面板填满可用空间。
- [x] 关闭浮动窗口后，标签回到原宿主并可继续交互。
- [x] `289/289` 自动化测试通过；Demo Desktop 构建 0 警告、0 错误。

### 后续任务（E6 工作区停靠）

- [ ] 支持浮动面板拖回主窗口并显示停靠预览。
- [ ] 支持任意 Dock 面板在左/右/上/下区域重新吸附，而不局限于当前固定布局。
- [ ] 持久化 Tab 顺序、选中状态、面板尺寸、浮动窗口位置与大小。
- [ ] 支持多显示器、DPI 缩放和窗口越界修正。
- [ ] 增加面板关闭、恢复、重复拖动和跨窗口生命周期的集成测试。

## 实现入口

| 模块 | 文件 | 职责 |
| --- | --- | --- |
| Tab 交互 | `Src/Spark.Engine/UI/UITabView.cs` | 命中标签、8px 阈值、拖动事件 |
| Outliner 宿主 | `Src/Spark.Engine.Editor/EditorOutlinerHost.cs` | 单个 Outliner Tab 的移除与恢复 |
| Viewport 宿主 | `Src/Spark.Engine.Editor/EditorAssetEditors.cs` | Scene/资源文档 Tab 的移除与恢复 |
| 编辑器布局 | `Src/Spark.Engine.Editor/EditorUi.cs` | 原生窗口、Canvas、布局切换与关闭恢复 |
| 回归测试 | `Tests/Spark.Engine.Tests/EditorControlTests.cs` | 具体标签拖动行为验证 |

## 验证命令

```powershell
dotnet test Tests\Spark.Engine.Tests\Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false
$verifyOut = Join-Path $env:TEMP 'SparkEngine-DemoDesktop-Verify-20260904k\'
dotnet build Demo\Demo.Desktop\Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false /p:OutDir="$verifyOut"
```
