# Content Browser 与项目布局工作日志

日期：2026-09-03

## 本轮完成

- 项目描述文件从仓库顶层移动到 `Demo/Demo.project`；编辑器按项目目录直接解析，不再向父目录推断。
- 一个项目根目录必须且只能包含一个 `*.project` 文件。
- `UseEditor` 只保留 `projectDirectory`，资源目录固定为项目根下的 `Content`。
- Content Browser 启动时只扫描引擎专有 `.asset` 文件，不扫描 PNG、JPG、glTF 原始资源。
- 默认无筛选时优先定位 `Textures` 目录；`All Assets` 显示 `Content` 根目录直接资源和直接子文件夹。
- 当前目录默认只显示直接资源；搜索或类型筛选启用后递归匹配当前目录及子目录。
- 右侧资源列表支持显示直接子文件夹，双击文件夹可进入并同步左侧目录树。
- 图片、glTF StaticMesh 导入到当前 Content 目录，不再按类型强制创建 `Textures`/`Models` 子目录。
- Windows 桌面直接使用 Silk.NET `IWindow.FileDrop` 事件接入窗口文件拖放导入。

## 验证

- `dotnet test Spark.Engine.slnx --no-restore /p:UseSharedCompilation=false`
- 结果：`193/193` 通过。
- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false`
- 结果：0 警告，0 错误。

## 后续

- Content Browser 缩略图、资产引用关系和拖入 Viewport。
- Windows 原生文件选择器和导入任务队列。
