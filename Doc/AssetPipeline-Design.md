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
- 导入 BaseColor、Normal、Metallic-Roughness 纹理引用。
- 将 glTF 材质参数映射到引擎 Material；当前仍由 Blinn-Phong 管线消费。
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
Material 当前仍是可变资源，因此 Play 时按 AssetGuid 为每个 RuntimeWorld 创建一份瞬态副本：保留 AssetGuid、分配独立 ResourceId，
同一 RuntimeWorld 内复用该副本，并由 World 在 Stop/Dispose 时释放。MaterialInstance 会展平为等价的有效参数，纹理引用继续共享。

## 8. 后续扩展

当前已落地：`GltfStaticMeshImporter`（`.gltf` JSON、内嵌/外部 buffer、StaticMesh、节点层级）、
`.asset` 首版编解码（StaticMesh 和基础 Material）、`AssetRegistry`（AssetGuid、来源/依赖/状态、目录扫描和懒加载）、
`RuntimeActorFactory`（自定义组件和运行时行为注册）和
`WindowsCookBackend`（版本化 `PAK0` 包、AssetGuid/依赖索引、确定性排序、原子写入）。`.scene` 格式当前为
版本 4，StaticMesh/SkeletalMesh/Material 组件会保存 AssetGuid 引用，LightComponent 状态和 Camera 视图参数会随组件保存；
RuntimeWorld 与 EditorWorld 使用不同的全局 ProxyId，避免渲染实例状态串用。编辑器 Play 使用同一个
`ResourceManager` 共享不可变 Mesh/Texture 资产，避免同一资源被两个管理器重复接管；可变 Material 使用 World 持有的运行时副本。
GLB、材质/纹理导入、完整纹理依赖加载以及 ResourceManager 从 `.pak` 的运行时加载仍待实现。

- SkeletalMesh、Skeleton、Animation Clip。
- 材质实例和 PBR 参数完整导入。
- 纹理压缩、MipMap、平台格式选择。
- 增量 Cook、热重载、自动恢复。
- Linux/macOS/Web 等 CookTargetPlatform。
