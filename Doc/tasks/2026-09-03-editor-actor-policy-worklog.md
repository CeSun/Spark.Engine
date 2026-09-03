# 编辑器内部 Actor 策略工作日志

日期：2026-09-03

## 背景

`EditorViewportCameraActor` 已通过 `[SceneTransient]` 排除场景保存，但 World Outliner 和状态栏仍直接遍历全部 Actor，导致编辑器相机以及 `WallSwinger`、`SkeletalAnimator` 等宿主内部行为出现在用户场景结构中。

## 本轮决策

- `[SceneTransient]` 继续只表达“不进入 SceneDocument”，不复用为 UI 隐藏规则。
- 新增 `EditorActorFlags`/`EditorActorAttribute`，分别描述 Outliner 可见性、选择、编辑、用户删除、复制和关卡统计能力。
- 新增集中式 `EditorActorPolicy`，UI 和编辑器服务不得自行判断具体 Actor 类型。
- `EditorViewportCameraActor`、`WallSwinger` 和 `SkeletalAnimator` 标记为 `Internal`。
- 用户主动创建、带 `CameraComponent` 的普通场景 Actor 仍保持可见和可编辑。

## 落地范围

- World Outliner 的结构签名和重建使用同一可见 Actor 集合。
- 视口拾取跳过不可选择 Actor，避免内部几何遮挡正常选择，同时保持 Outliner 可见性与拾取能力彼此独立。
- 公共选择入口、Inspector 属性修改、资源引用修改、Transform、Attach、Duplicate、Rename 和 Delete 统一遵守 Actor 能力。
- 状态栏 Actor/Component 计数排除内部 Actor。
- SceneDocument、Reload、Play/Stop 和 RenderTarget 绑定逻辑保持不变。

## 后续

- ✅ World Outliner 已增加 Actor/类型/组件搜索、`Show Internal Actors`、`Show Components` 和 `Only Selected`。
- E6 工作区阶段将 Outliner 过滤状态纳入布局持久化。
- ✅ 已引入 `EditorViewportSession`，编辑器视口相机已脱离 `World.Actors`。

## 验证

- 定向测试覆盖内部 Actor 隐藏、普通场景 Camera 可见、选择和编辑保护、状态统计以及隐藏几何拾取穿透。
- `dotnet test Tests/Spark.Engine.Tests/Spark.Engine.Tests.csproj --no-restore /p:UseSharedCompilation=false`
- 结果：`237/237` 通过。
- Demo Desktop 使用独立临时输出目录构建，结果：0 警告、0 错误。
