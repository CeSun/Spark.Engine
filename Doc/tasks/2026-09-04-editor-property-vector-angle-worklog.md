# 编辑器属性输入：偏航角与三维向量工作日志

日期：2026-09-04

## 目标

让 Inspector 属性输入框能够直接编辑 UE 常见的变换数据：三维向量，以及以度为单位的
`Pitch, Yaw, Roll` 欧拉角；单值角度输入同时接受 `°` 和 `deg` 后缀。

## 已完成

- `UIPropertyGrid` 的 Vector2/Vector3/Vector4 改为每个分量一个 `UITextBox`，按 UE Details 风格横向排列。
- `Quaternion` 在编辑器中显示为三个独立的欧拉角输入框（`Pitch, Yaw, Roll`，单位：度）。
- 三个欧拉角输入提交时转换为 `Quaternion.CreateFromYawPitchRoll`。
- 浮点输入支持当前区域设置与不变文化格式，并接受 `45°` / `45 deg` 角度后缀。
- 保留现有 `UITextBox` 的选择、撤销、回车提交、Escape 取消和失焦提交行为。
- 新增 Vector3、Yaw 角度和 Quaternion 欧拉角的端到端输入测试。

## 验收标准

- [x] 分别在 X/Y/Z 输入框填写 `1`、`2`、`3` 后，Vector3 属性得到对应三个分量。
- [x] 输入 `10, 45, 20` 后，旋转得到 Pitch=10°、Yaw=45°、Roll=20°。
- [x] 输入 `45°` 或 `45 deg` 后，单值角度得到 45。
- [x] 旋转输入统一采用三轴欧拉角，底层仍保存为 Quaternion。
- [x] 全量自动化测试通过。

## 验证

```powershell
dotnet test Tests\Spark.Engine.Tests\Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false
```

结果：`290/290` 通过。
