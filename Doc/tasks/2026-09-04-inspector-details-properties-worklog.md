# Inspector Details 属性展示工作日志

日期：2026-09-04

## 问题

选择 Actor 时，Inspector 只把 Actor 自身交给 `UIPropertyGrid`。由于 Actor 可编辑属性很少，Details 面板
通常只有 `Name` 和编辑器临时可见性两个属性，Transform、Camera/Light/Mesh 等组件参数及资源引用不可见。

## 本轮完成

- 按 UE Details 的基本分组习惯拆分 Actor、Transform / Root Component、Asset References 区域。
- Actor 选择时保留 Actor 基础属性，并将根 `SceneComponent` 的 `RelativeLocation`、`RelativeRotation`、
  `RelativeScale` 及其业务属性（例如 Camera 的视场角/裁剪面）显示在第二个属性网格中。
- Actor 的 Mesh/Material/Texture 等 `[SceneProperty]` 资源字段改为绑定根组件真实对象，拖放、Asset Picker、
  批量赋值和 Undo/Redo 不改变原有语义。
- 直接选择 Component 仍使用原有单网格和资源字段路径；空选择不会残留上一次分组内容。
- 为 `UIPropertyGrid` 增加只读 `PropertyNames` 诊断接口，并加入 Actor + Camera Details 回归测试。

## 验证

- `dotnet test Tests/Spark.Engine.Tests/Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false`：`285/285` 通过。
- `dotnet build Demo/Demo.Desktop/Demo.Desktop.csproj --no-restore /p:UseSharedCompilation=false /p:OutDir="$env:TEMP/SparkEngine-DemoDesktop-Verify-20260904b/"`：0 警告、0 错误。

## 已知边界

- 当前组件分组以 Actor 的根 `SceneComponent` 为主；非根组件折叠树、分类元数据和数组/嵌套对象编辑留给后续 Inspector 扩展。
