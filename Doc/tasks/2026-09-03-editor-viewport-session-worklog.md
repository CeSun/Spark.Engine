# 编辑器视口 Session 解耦工作日志

日期：2026-09-03

## 背景

编辑器视口原先通过 `[SceneTransient]` 的 `EditorViewportCameraActor` 加入 EditorWorld，并在 Reload/Play 时扫描、复制到新 World。虽然该 Actor 默认从 Outliner 隐藏，但视口生命周期仍耦合场景对象，Resize 也需要反复扫描 World 并同步 RenderTarget。

## 本轮决策

- Engine 层新增 `ICameraSnapshotSource` 与 `CameraSnapshotSourceRegistry`，允许宿主工具直接向帧数据追加相机快照。
- Editor 层新增 `EditorViewportSession`。Session 内部复用现有 `CameraComponent` 的相机数学，但组件没有 Owner，也不注册到任何 World。
- `EditorUi.CreateViewportSession` 负责创建和登记会话；`SetPictureInPicture` 明确绑定 `UIRenderView` 与 Session。
- Play/Stop 只改变 `WorldContext.ActiveWorld`，Session 继续观察活跃世界；Reload 不再复制编辑器相机。
- Resize 直接替换 `EditorViewportSession.RenderTarget`，不扫描 EditorWorld/RuntimeWorld。
- 普通场景 Camera 仍按 ComponentGuid 在 EditorWorld 与 RuntimeWorld 之间恢复 RenderTarget，不改变场景相机语义。

## 落地结果

- 删除 `EditorViewportCameraActor` 以及 `CloneEditorViewportCameras`。
- Demo 编辑器视口不再向 `World.Actors` 添加相机宿主，Outliner 无需为编辑器相机做隐藏补偿。
- 相机飞行、轨道、平移、聚焦、书签、拾取和 Gizmo 继续使用 Session 内的 `CameraComponent`，交互行为保持不变。
- 外部相机源注册表只在注册关系变化时更新快照数组，逐帧收集不产生列表副本。
- 多个 Session 可同时输出到不同 RenderTarget，并可独立停用、换目标和释放，为后续多视口布局保留稳定边界。

## 验收与边界

- EditorWorld 和 RuntimeWorld 均不包含编辑器视口 Camera Actor。
- Play/Reload 后 Session 保持同一相机实例、姿态和 RenderTarget。
- 多 Session、目标替换、停用和注销均有回归测试。
- 本轮只完成多视口的数据与生命周期边界；多面板布局、各视口输入焦点和视图类型切换留给 E6。

## 验证

- `dotnet test Tests/Spark.Engine.Tests/Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false`
- 结果：`241/241` 通过。
- Demo Desktop 使用独立临时输出目录构建，结果：0 警告、0 错误。
