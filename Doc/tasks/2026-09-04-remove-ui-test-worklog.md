# 移除独立 UI Test 工作日志

日期：2026-09-04

## 变更

- 删除 Demo 中独立的 UI Test 窗口、根面板、输入控件测试面板、集合控件测试面板和 EditorControls 验收场景。
- 从编辑器工具栏移除 `UI Tests` 按钮及其宿主回调；`EditorUi` 不再暴露 UI Test 窗口启动入口。
- 从 VerifyHub 移除 Editor Controls 测试场景入口。
- 保留 `EditorControlTests` 单元测试，因为它们是控件的无 GPU 回归测试，不属于被删除的运行时 UI Test 窗口。
- 更新 UI 设计文档和 README，避免引用已删除 Demo 文件。

## 验证

- Demo Desktop 可正常构建。
- 全量单元测试继续通过。
