# Spark.Engine 资产格式与 Cook 设计

> 状态：设计基线（Windows first，可扩展其他平台）
> 目标：使用引擎自定义资产格式，支持 glTF 静态网格导入，并把编辑器资产转换为运行时包。

## 1. 文件类型

第一阶段使用不包含产品名的后缀：

| 后缀 | 用途 | 运行时是否直接读取 |
|---|---|---|
| `.scene` | 可编辑场景文档 | 编辑器读取；Cook 后运行时读取 `.pak` 内部数据 |
| `.asset` | 引擎内部单项资产 | 编辑器读取；Cook 后转换 |
| `.pak` | Cook 后运行时资产包 | 是 |

源文件不要求人类可读，优先采用版本化二进制。二进制格式仍需保留 Magic、FormatVersion、AssetType 和依赖信息。
当前不做自动迁移；遇到不支持的版本应明确报错，而不是按旧布局猜测解析。

## 1.1 项目目录结构

项目根由 `<ProjectName>.project` 标记（当前示例为 `Demo/Demo.project`）。编辑器启动时把进程工作目录切换到该根目录，
因此相对路径、日志、导入输出和 Cook 输出都以项目为基准；宿主也可以通过
`UseEditor(..., projectDirectory: ...)` 显式指定根目录。代码和项目配置中不硬编码本机
绝对目录；项目目录按启动目录直接解析，不向父目录推断；显式配置时使用 `.` 或
`../MyProject` 这类相对于进程启动目录的路径。一个项目根目录必须且只能包含一个
`*.project` 文件。

```text
<RepositoryRoot>/
├── Demo/               # Demo 项目根目录
│   ├── Demo.project    # 项目描述和目录约定
│   ├── Content/        # 持久化 .asset、导入后的模型/贴图
│   │   ├── Models/
│   │   ├── Textures/
│   │   └── Materials/
│   ├── Config/         # 编辑器/项目配置
│   ├── Saved/          # 最近文件、自动恢复、日志等运行期编辑器数据
│   ├── Intermediate/   # 导入中间文件、缓存和临时构建数据
│   ├── Build/          # Cook 后的 .pak 与平台产物
│   ├── Demo/           # Demo C# 项目源码
│   └── Demo.Desktop/   # Demo 桌面启动项目
├── Src/                # Spark.Engine 引擎源码
├── Tests/              # 引擎测试
└── Doc/                # 设计和实现文档
```

`Content Browser` 只索引 `Content` 下的引擎专有资源；首版目录扫描器只注册 `.asset`，
`.scene` 由场景服务管理。它不会扫描或注册 glTF、PNG/JPG 等原始资源格式。原始文件只作为
导入输入，可以放在项目外部或 `Source` 目录；导入器负责生成引擎自己的 `.asset` 文件，
运行时也只依赖 Cook 后的内部资产。

资源导入使用 Content Browser 当前选中的目录作为目标，不按资源类型强制创建
`Textures` 或 `Models` 子目录；当前目录可以是任意多级相对路径。
Content Browser 首次打开且搜索为空、类型为 `All Assets` 时，若存在 `Textures` 目录则默认选中它；
用户手动切换目录后保持手动选择。

### 1.1 首版编码选择

首版采用引擎自定义的分块二进制编码，不新增第三方序列化依赖。每个文件由固定头和长度前缀块组成：

```text
Magic(4) | FormatVersion(u16) | AssetType(u8) | Flags(u8)
| AssetGuid(16) | DependencyCount(u32) | PayloadLength(u64) | Payload
```

Payload 内部按 `ChunkType + ChunkVersion + ChunkLength + Data` 排列，未知 Chunk 可以跳过，未知文件版本直接拒绝加载。
编辑器保存采用确定性字段顺序，便于内容哈希、问题复现和 Cook；调试工具另行提供导出 JSON 的只读诊断功能，不把 JSON 作为运行时格式。

## 2. 持久化身份

```text
AssetGuid   磁盘和编辑器稳定身份，Guid
ResourceId  进程内运行时 ID，int
ProxyId     场景代理生命周期 ID，int
```

AssetGuid 是资产引用和场景序列化的唯一持久身份。Cook 后可以建立 `AssetGuid -> PackageOffset` 索引，但不改变源资产身份。

## 3. 资产流水线

```text
Source File / glTF
    -> Importer
    -> Intermediate Asset
    -> Asset Registry
    -> Windows Cook Backend
    -> .pak Runtime Package
    -> ResourceManager
    -> GPU Upload
```

平台无关的部分包括资产模型、依赖图、AssetGuid、校验和和序列化描述；平台相关的部分包括纹理压缩、着色器产物、包布局和运行时加载器。
因此第一阶段只实现 Windows Cook，但接口必须允许增加其他 `CookTargetPlatform`。

## 4. glTF 第一阶段范围

第一阶段只导入 StaticMesh：

- 保留 glTF 节点层级和节点局部变换。
- 每个可渲染节点生成一个静态网格实例或对应场景组件。
- 导入位置、法线、UV、顶点颜色、索引。
- 首版不导入 glTF Material 和 Texture，只生成引擎 StaticMesh 资产。
- 暂不导入 Skeleton、Animation、Morph Target。

默认保留节点层级，便于编辑器层级树查看和修改；后续可以提供“合并网格”导入选项。

## 5. Asset Registry

编辑器维护资产索引，至少包含：

```text
AssetGuid
AssetType
SourcePath
CookedPath
Dependencies
ContentHash
ImportSettings
ImportStatus
```

第一阶段不做增量 Cook，但保留 `ContentHash` 和依赖字段，后续可以在不改变格式的情况下增加增量构建。

## 6. Cook 约束

- Cook 不运行在渲染线程。
- Import、校验、依赖扫描和包写入由编辑器后台任务执行。
- Runtime 只依赖 `.pak` 中的内部资产和平台产物，不依赖 glTF 源文件。
- Cook 失败必须保留错误信息和失败资产身份，不能生成半可用包。
- `.pak` 包应有 manifest、版本、目标平台和依赖索引。

## 7. 与 ResourceManager 的边界

`ResourceManager` 负责运行时资源对象和 GPU 上传，不负责解析 glTF、不负责扫描目录、不负责写 Cook 包。

```text
Editor Importer / Cooker
    -> Runtime Asset Loader
    -> ResourceManager.EnsureUploaded
    -> RenderThread GPU resources
```

不可变 Mesh/Texture 资产可以被 EditorWorld 和 RuntimeWorld 共享；Actor、Component、SceneProxy 和运行时状态不能共享。
Mesh 的顶点、索引、逆绑定姿势以及 Texture 的像素数据会在构造时复制，公共 API 仅暴露 `ReadOnlyMemory<T>`；
因此调用方既不能通过构造参数保留的数组，也不能通过资源属性原地修改共享资产。
Material 当前仍是可变资源，因此 Play 时按 AssetGuid 为每个 RuntimeWorld 创建一份瞬态副本：保留 AssetGuid、分配独立 ResourceId，
同一 RuntimeWorld 内复用该副本，并由 World 在 Stop/Dispose 时释放。MaterialInstance 会展平为等价的有效参数，纹理引用继续共享。

## 8. 后续扩展

当前已落地：基于 SharpGLTF 的 `GltfStaticMeshImporter`（`.gltf`/`.glb`、内嵌/外部 buffer、StaticMesh、节点层级）和
`GltfImportService`（按“规范化源路径 + mesh index”生成稳定 AssetGuid、写入 `.asset`、登记 Registry，导入时不修改场景）、
`.asset` 首版编解码（StaticMesh、Texture2D 和基础 Material，含材质纹理依赖）、`AssetRegistry`（AssetGuid、来源/依赖/状态、目录扫描和懒加载）、
`RuntimeActorFactory`（自定义组件和运行时行为注册）和
`WindowsCookBackend`（版本化 `PAK0` 包、AssetGuid/依赖索引、确定性排序、原子写入）。`.scene` 格式当前为
版本 5：结构字段显式保存 Actor/Component GUID、Actor/Component 类型、Root/Attach/Socket 和相对变换；组件可编辑状态由
`[SceneProperty]` 驱动的类型化属性块保存，资产属性编码为 AssetGuid。`[SceneTransient]` Actor 不进入编辑场景文档；
旧场景版本会明确拒绝，当前不做自动迁移。
RuntimeWorld 与 EditorWorld 使用不同的全局 ProxyId，避免渲染实例状态串用。编辑器 Play 使用同一个
`ResourceManager` 共享不可变 Mesh/Texture 资产，避免同一资源被两个管理器重复接管；可变 Material 使用 World 持有的运行时副本。
`SceneCookService` 已能从场景资产引用构建传递依赖闭包并生成完整 Windows `.pak`；`CookedPackageRuntimeLoader`
从包内 Registry 解码 StaticMesh/Material/Texture2D，实例化 RuntimeWorld，并把资源生命周期交给 World。
材质/纹理导入和平台纹理产物仍待实现。

- SkeletalMesh、Skeleton、Animation Clip。
- 材质实例和 PBR 参数完整导入。
- 纹理压缩、MipMap、平台格式选择。
- 增量 Cook、热重载、自动恢复。
- Linux/macOS/Web 等 CookTargetPlatform。
