# Spark.Engine 场景层级与组件挂载设计

> 状态：基础运行时能力已实现，编辑器集成与资产 Socket 仍在开发（UE 风格，Windows 编辑器优先）
> 目标：补齐 `World -> Actor -> SceneComponent` 的空间层级，支持 RootComponent、Socket、挂载规则和编辑器层级编辑。
> 参考：UE `USceneComponent`、`AActor::AttachToComponent`、`FAttachmentTransformRules`。

## 1. 设计目标

Spark.Engine 的场景对象继续采用 UE 风格：

```text
World
└── Actor
    └── RootComponent
        ├── SceneComponent
        ├── StaticMeshComponent
        ├── CameraComponent
        └── LightComponent
```

空间关系只由 `SceneComponent` 树表达。普通 `ActorComponent` 没有空间变换，不能作为挂载节点。
一个 SceneComponent 最多有一个父节点，可以有多个子节点，场景层级不允许形成环。

UE 参考：

- [Components in Unreal Engine](https://dev.epicgames.com/documentation/en-us/unreal-engine/components-in-unreal-engine)
- [USceneComponent](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/USceneComponent)

## 2. RootComponent

每个 Actor 可以指定一个 `RootComponent`。Actor 的空间位置、旋转和缩放由 RootComponent 代表。
Actor 挂载到另一个组件时，实际挂载的是该 Actor 的 RootComponent。

约束：

1. RootComponent 必须属于当前 Actor。
2. RootComponent 不能挂载到自身或自己的后代。
3. 设置新 RootComponent 时必须处理旧根节点的父子关系，并根据规则保持或重算世界变换。
4. 没有 RootComponent 的 Actor 可以存在，但不能作为完整的空间层级参与渲染和挂载。

建议 API：

```csharp
SceneComponent? RootComponent { get; }
void SetRootComponent(SceneComponent component);
```

## 3. AttachParent / AttachChildren

SceneComponent 保存一组相对变换和一组层级关系：

```csharp
SceneComponent? AttachParent { get; }
IReadOnlyList<SceneComponent> AttachChildren { get; }
Matrix4x4 RelativeTransform { get; set; }
Matrix4x4 WorldTransform { get; }
```

世界变换按以下关系计算。由于 `System.Numerics.Matrix4x4` 使用行向量约定，代码中的乘法顺序为右侧父级：

```text
ChildRelativeTransform
  * ParentSocketTransform
  * ParentWorld
  = ChildWorld
```

没有 Socket 时 `ParentSocketTransform` 为单位变换。父节点或自身变换变化时，当前节点和整个后代子树标记为 dirty，按父到子的顺序更新。

挂载和分离必须：

- 检查父子是否属于允许的 Actor/World 范围。
- 允许跨 Actor 挂载，但父子 Actor 必须位于同一个 World；跨 World 挂载直接拒绝。
- 检查是否会形成环。
- 同时维护父节点的 `AttachChildren` 和子节点的 `AttachParent`。
- 失败时保持原层级不变，并返回 `false` 或抛出明确异常。

## 4. Socket

Socket 是父组件提供的命名局部变换。子组件保存 `AttachSocketName`，世界变换计算时先取父组件的 Socket 变换。

```csharp
string? AttachSocketName { get; }

bool DoesSocketExist(string socketName);
Transform GetSocketTransform(
    string socketName,
    TransformSpace space = TransformSpace.World);
```

Socket 提供者采用接口隔离：

```csharp
public interface ISceneSocketProvider
{
    bool DoesSocketExist(string socketName);
    Transform GetSocketTransform(string socketName, TransformSpace space);
}
```

初始实现：

- `SceneComponent`：普通命名 Socket 字典。
- `StaticMeshComponent`：后续从 StaticMesh 资产读取 Socket。
- `SkeletalMeshComponent`：后续从骨骼或骨骼 Socket 读取。

指定的 Socket 不存在时，挂载操作应失败且不改变原关系；查询 API 可以明确记录 warning 并回退到组件变换，但不能静默改变挂载语义。

UE 参考：[GetSocketTransform](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/Runtime/Engine/Components/USceneComponent/GetSocketTransform)。

## 5. AttachmentTransformRules

挂载规则分别控制位置、旋转和缩放：

```csharp
public enum AttachmentRule
{
    KeepRelative,
    KeepWorld,
    SnapToTarget
}

public readonly struct AttachmentTransformRules
{
    public AttachmentRule LocationRule { get; init; }
    public AttachmentRule RotationRule { get; init; }
    public AttachmentRule ScaleRule { get; init; }
    public bool WeldSimulatedBodies { get; init; }
}
```

语义：

| 规则 | 结果 |
|---|---|
| `KeepRelative` | 保持子节点相对变换，父节点移动时子节点跟随 |
| `KeepWorld` | 保持当前世界变换，反解新的相对变换 |
| `SnapToTarget` | 对齐到父节点或 Socket，位置/旋转/缩放分别按对应规则处理 |

常用预设：`KeepRelativeTransform`、`KeepWorldTransform`、`SnapToTargetIncludingScale`、`SnapToTargetNotIncludingScale`。
`WeldSimulatedBodies` 先保留字段，物理系统实现后再启用。

规则只描述一次挂载操作，不作为持久化状态。场景文件持久化父节点、Socket 名和最终相对变换。

## 6. API 与生命周期

```csharp
void SetupAttachment(SceneComponent parent, string? socketName = null);

bool AttachToComponent(
    SceneComponent parent,
    AttachmentTransformRules rules,
    string? socketName = null);

void DetachFromComponent(DetachmentTransformRules rules);
```

`SetupAttachment` 用于构造和场景加载阶段，记录待注册的父节点和 Socket；`AttachToComponent` 用于编辑器操作和运行时动态挂载。
注册/BeginPlay 后的挂载必须更新代理、包围盒和渲染线程同步数据。组件销毁或 Actor 移出 World 时，先解除层级关系，再注销 SceneProxy。

Actor 移出 World 时仅解除跨 Actor 挂载，Actor 自己拥有的组件树保持不变。挂在被移除 Actor 下的其他 Actor
使用 `KeepWorldTransform` 脱离，不级联删除；若待移除操作在生命周期提交前被撤销，则恢复原父组件、Socket 和局部变换。

## 7. 编辑器与运行时 World 隔离

编辑器必须保持编辑对象静止，游戏运行时使用独立 World：

```text
Edit Mode
  EditorWorld
  - 不执行游戏 Actor Tick
  - 只执行编辑器刷新、命令和属性修改

Play Mode
  RuntimeWorld
  - 从 SceneDocument 实例化
  - 执行 BeginPlay / Update / EndPlay
  - Stop 后完整销毁
```

两个 World 不共享可变 Actor/Component，只共享不可变资产和资源描述。编辑器中的挂载、变换和层级修改全部通过可撤销命令完成。

### 7.1 RuntimeWorld 实例化决策

Play 不直接深拷贝 `EditorWorld`，也不在编辑对象上切换生命周期，而是以当前 `SceneDocument` 为输入重新实例化 `RuntimeWorld`：

| 方案 | 优点 | 代价/风险 |
|---|---|---|
| 深拷贝 EditorWorld | 启动路径直观，可保留部分内存态 | 需要为每类 Actor/Component 编写复制规则，容易遗漏运行时字段或共享可变引用 |
| SceneDocument 重新实例化 | 以持久化边界隔离状态，生命周期确定，Stop 可完整销毁；与 Cook 输入一致 | 首次 Play 需要构造对象并解析资产，启动成本略高 |

首版采用 **SceneDocument 重新实例化**。Play 前将编辑器当前命令结果反映到内存中的 `SceneDocument`，再按父节点拓扑创建 Runtime Actor/Component，恢复 Root、Socket、相对变换和资产引用。
运行时新增对象、Tick 状态、临时组件和物理状态只存在于 RuntimeWorld；Stop 释放整个 RuntimeWorld，不回写 EditorWorld。

## 8. 场景持久化

场景文件至少记录：

```text
ActorGuid
ComponentGuid
ComponentType
RootComponentGuid
ParentComponentGuid
AttachSocketName
RelativeTransform
EditableProperties
AssetGuid references
```

AttachmentTransformRules 不写入场景文件。加载时按父节点拓扑顺序创建组件、设置 RootComponent、恢复挂载和相对变换，最后再启动运行时生命周期。

## 9. 实施状态与顺序

1. ✅ 相对变换、RootComponent、AttachParent/AttachChildren、环检测和递归世界变换。
2. ✅ `KeepRelative` / `KeepWorld` / `SnapToTarget`，以及 Detach 保持世界变换。
3. ✅ `ISceneSocketProvider` 与普通组件 Socket；跨 Actor 挂载限制在同一 World。
4. ✅ SceneProxy 读取层级计算后的 WorldTransform，变换 setter 向后代传播 dirty。
5. ✅ `AttachComponentCommand` 已支持可撤销挂载；⏳ 编辑器层级树拖拽接线；Viewport 变换命令已支持可撤销局部 TRS。
6. ✅ SceneDocument 二进制序列化、加载、RuntimeWorld 实例化、AssetGuid 解析和 EditorContext Play/Stop；⏳ `.pak` Runtime Loader。
7. ⏳ StaticMesh Socket、跨资产 Socket 和后续骨骼 Socket。
