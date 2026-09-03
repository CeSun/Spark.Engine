# Content Browser 创建流程梳理工作日志

日期：2026-09-03

## 本轮完成

- 将含义不准确的 `New` 按钮调整为统一 `Create` 菜单，菜单明确列出 `Folder` 与 `Material`。
- 资源列表右键菜单同步提供 `New Folder` 和 `New Material`，避免同一种操作在不同入口下行为不一致。
- 名称输入框改为创建/重命名共用；未输入名称时不执行写操作，显示提示并把焦点移回名称框。
- 新增 `EditorAssetOperationService.CreateMaterial`：生成新 AssetGuid，先写同目录临时文件并校验类型和 GUID，再原子提交并登记 Registry。
- 同名、非法名称、写入或登记失败时清理临时文件和未完成的 Registry 记录。
- 新增 `EditorUi.CreateContentMaterial` 公共入口；创建成功后 Content Browser 自动进入目标目录并选中新材质，不进入场景 Undo 栈。

## 验证

- 服务测试覆盖 Material 文件、类型、GUID、Registry 解析、同名拒绝和临时文件清理。
- 真实 `UICanvas` 测试覆盖 Create 菜单、空名称反馈，以及 Folder/Material 两个创建分支的端到端流程。
- `dotnet test Spark.Engine.slnx --no-restore /p:UseSharedCompilation=false`：`224/224` 通过。
- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false`：0 警告、0 错误。

## 后续

- E3 继续补充 Texture/Material Instance 等资源类型的创建模板，并推进缩略图缓存和资源编辑器预览交互。
