# Content Browser 资源管理闭环工作日志

日期：2026-09-03

## 本轮完成

- 新增 `EditorAssetOperationService`，所有 Content 写操作统一校验项目边界、非法路径、符号链接、同名冲突和只读文件。
- Content Browser 从真实 `Content` 目录生成目录树，空文件夹和嵌套空目录可见。
- 目录支持新建、重命名、移动、复制和可恢复删除；资产支持重命名、移动、复制和可恢复删除。
- 移动/重命名保持 AssetGuid；单资产复制生成新 GUID；目录复制为每个资产生成新 GUID，并重写组内依赖表与 Material Payload 引用。
- 删除前分析 Registry 反向依赖闭包和当前 SceneDocument 引用；直接或传递引用会通过 `EditorAssetReferencedException` 阻止删除并列出引用方。
- 删除内容移动到 `Saved/Trash/Content`，不进入场景 Undo 栈；磁盘或 Registry 提交失败时恢复原路径与索引。
- Registry 全量刷新会移除已消失的持久资产记录，同时保留无磁盘身份的当前场景引用。
- Content Browser 接入操作栏、资源列表右键菜单、`F2`/`Delete`/`Ctrl+D`；资源文件既可拖到右侧子文件夹，也可跨控件拖到左侧目录树节点，目录树节点之间同样支持拖放移动。
- `UITextBox` 增加 Enter 提交事件，`UIListView` 增加带修饰键的键盘命令与右键菜单请求。

## 验证

- 新增 8 个逻辑测试，覆盖空目录、资产 CRUD、目录移动、组复制 GUID/依赖重写、直接/传递引用保护、可恢复删除、同名失败回滚、只读失败和 Registry 重扫。
- 新增 2 个 EditorUi 端到端测试：一个覆盖“扫描 -> 新建目录 -> 重命名 -> 移动 -> 复制 -> 删除”且验证不污染场景历史，另一个通过真实 `UICanvas` 输入验证资源列表到左侧目录树的跨控件拖放。
- `dotnet test Spark.Engine.slnx --no-restore /p:UseSharedCompilation=false`：`214/214` 通过。
- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false`：0 警告、0 错误。

## 后续

- 按路线图进入 E2：Inspector 统一资源字段、Asset Picker、拖放/清空/定位和 Undo/Redo。
- 缩略图、后台导入和跨项目/版本控制操作继续按 E3/E4 处理。
