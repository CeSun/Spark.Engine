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
- 列表支持单击选中、双击/回车激活；资源激活会打开对应类型的可关闭编辑器标签，不再隐式创建场景 Actor。
- Texture2D 编辑器使用原始 RGBA8 数据上传单张 UI 纹理预览，不再降采样为 64×64 色块。
- 图片、`.gltf`/`.glb` StaticMesh 导入到当前 Content 目录，不再按类型强制创建 `Textures`/`Models` 子目录。
- 模型导入只生成和登记资产，不自动实例化到场景，也不写入场景 Undo/Redo 历史。
- StaticMesh 资源支持从列表拖入场景视口；射线落点带地面/视线回退并遵循网格吸附，创建 Actor 后自动选中且可 Undo/Redo。
- Windows 桌面直接使用 Silk.NET `IWindow.FileDrop` 事件接入窗口文件拖放导入。

## 验证

- `dotnet test Spark.Engine.slnx --no-restore /p:UseSharedCompilation=false`
- 结果：`204/204` 通过。
- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false`
- 结果：0 警告，0 错误。

## 后续

- Content Browser 缩略图、资产引用关系和拖入 Viewport。
- Windows 原生文件选择器和导入任务队列。
