# Inspector 资源引用编辑工作日志

日期：2026-09-03

## 本轮完成

- 新增 Inspector 专用统一资源字段，仅展示所有选中对象共有、可读写且带 `[SceneProperty]` 的 `SceneResource` 属性。
- 支持 None、同值和 `<Multiple Values>` 状态；覆盖 StaticMesh、Material、Texture2D 及派生资源类型。
- 点击值区打开按属性类型过滤的 Asset Picker；字段提供 `L` 定位、`O` 打开和 `X` 清空操作。
- Content Browser 的真实资源项可跨控件拖到 Inspector 字段。类型不兼容的拖放保留原值并显示字段级错误。
- 缺失 Registry 记录、资源解析失败和失败导入状态都在对应字段附近展示，不中断其它 Inspector 行。
- 新增 `PropertyBatchChangeCommand`，把多对象资源赋值合并为一次原子 Undo/Redo，执行或撤销中途失败会回滚已修改目标。
- 资源赋值后重新登记世界资产时保留磁盘 Content 路径、内容哈希和传递依赖，避免定位和 Cook 闭包退化。
- Content Browser 增加按 AssetGuid 定位能力；定位时清空搜索和类型过滤并进入资源所在目录。
- Play 状态统一拒绝资源属性编辑，避免在运行时预览期间意外改写 EditorWorld。

## 验证

- 新增 8 个测试，覆盖单选字段、Texture2D 属性、多选混合值、批量赋值/清空及 Undo/Redo、失败原子回滚、Asset Picker 类型过滤、真实画布拖放、字段级错误、定位/打开，以及保存/重载、Play/Stop 和 Cook 传递依赖闭包。
- `dotnet test Tests/Spark.Engine.Tests/Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false`：`222/222` 通过。
- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false`：0 警告、0 错误。

## 已知边界

- 首轮资源字段只作用于可持久化的 `[SceneProperty]`，不处理任意表达式绑定、数组/嵌套对象和跨项目资源引用。
- Material 资产自身的纹理参数仍由资源编辑器工作流管理，不混入场景 Undo 栈；资源编辑器预览与深化按 E3 继续实施。

## 后续

- 按路线图进入 E3：Texture/StaticMesh/Material 缩略图、预览缓存与失效更新，以及资源编辑器交互深化。
