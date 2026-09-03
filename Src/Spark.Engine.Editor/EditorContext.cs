using System.Reflection;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

public enum EditorPlayState
{
    Edit,
    Play,
}

public sealed class EditorSelection
{
    private readonly List<object> _items = new();
    private readonly IReadOnlyList<object> _itemsView;
    private object? _selected;

    public EditorSelection()
    {
        _itemsView = _items.AsReadOnly();
    }

    /// <summary>当前选择集合；最后操作的主选对象由 <see cref="Selected"/> 返回。</summary>
    public IReadOnlyList<object> Items => _itemsView;

    public int Count => _items.Count;

    public object? Selected
    {
        get => _selected;
        set => Set(value == null ? Array.Empty<object>() : new[] { value }, value);
    }

    public bool Contains(object target) => IndexOfReference(_items, target) >= 0;

    public void Add(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Contains(target))
        {
            Set(_items, target);
            return;
        }
        Set(_items.Append(target), target);
    }

    public void Toggle(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var next = _items.ToList();
        var index = IndexOfReference(next, target);
        if (index >= 0)
            next.RemoveAt(index);
        else
            next.Add(target);
        Set(next, index < 0 ? target : next.LastOrDefault());
    }

    public void Set(IEnumerable<object> targets, object? primary = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var next = new List<object>();
        foreach (var target in targets)
        {
            if (target != null && IndexOfReference(next, target) < 0)
                next.Add(target);
        }

        var nextPrimary = primary != null && IndexOfReference(next, primary) >= 0
            ? primary
            : next.LastOrDefault();
        if (ReferenceEquals(_selected, nextPrimary) && SequenceEqualByReference(_items, next))
            return;

        _items.Clear();
        _items.AddRange(next);
        _selected = nextPrimary;
        Changed?.Invoke(_selected);
    }

    public event Action<object?>? Changed;

    private static int IndexOfReference(IReadOnlyList<object> items, object target)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], target))
                return i;
        }
        return -1;
    }

    private static bool SequenceEqualByReference(IReadOnlyList<object> left, IReadOnlyList<object> right)
    {
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (!ReferenceEquals(left[i], right[i]))
                return false;
        }
        return true;
    }
}

public sealed class EditorContext : IDisposable
{
    public World World { get; private set; }
    private readonly WorldContext? _worldContext;
    public World? RuntimeWorld { get; private set; }
    public World ActiveWorld => RuntimeWorld ?? World;
    public EditorPlayState PlayState { get; private set; } = EditorPlayState.Edit;
    public IAssetRegistry AssetRegistry { get; }
    public RuntimeActorFactory RuntimeActorFactory { get; }
    /// <summary>RuntimeWorld 创建后执行的宿主行为注入点，用于恢复自定义 Actor/系统。</summary>
    public Action<World>? RuntimeWorldInitializer { get; set; }
    public EditorCommandHistory History { get; } = new();
    public EditorSelection Selection { get; } = new();
    public EditorWorldOutlinerData Outliner => EditorWorldOutlinerData.For(World);
    public bool IsDirty { get; private set; }
    public event Action<bool>? DirtyChanged;
    public event Action<EditorPlayState>? PlayStateChanged;
    public event Action<World, World>? WorldChanged;

    public EditorContext(World world, WorldContext? worldContext = null,
        IAssetRegistry? assetRegistry = null, RuntimeActorFactory? runtimeActorFactory = null)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        _worldContext = worldContext;
        AssetRegistry = assetRegistry ?? new AssetRegistry();
        RuntimeActorFactory = runtimeActorFactory ?? new RuntimeActorFactory();
    }

    public void Execute(IEditorCommand command)
    {
        History.Execute(command);
        SetDirty(true);
    }

    public bool Undo()
    {
        var result = History.Undo();
        if (result) SetDirty(true);
        return result;
    }

    public bool Redo()
    {
        var result = History.Redo();
        if (result) SetDirty(true);
        return result;
    }

    public void MarkSaved() => SetDirty(false);

    /// <summary>标记外部重载完成并丢弃重载前的撤销/重做命令。</summary>
    public void MarkReloaded()
    {
        History.Clear();
        SetDirty(false);
    }

    /// <summary>从已校验文档构建新 EditorWorld，并在成功后原子替换当前场景。</summary>
    public void Reload(SceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (PlayState != EditorPlayState.Edit)
            throw new InvalidOperationException("Stop Play before reloading the editor scene.");

        RegisterWorldAssets();
        var previous = World;
        var previousOutliner = EditorWorldOutlinerData.For(previous);
        if (_worldContext != null && !ReferenceEquals(_worldContext.CurrentWorld, previous))
            throw new InvalidOperationException("EditorContext no longer owns WorldContext.CurrentWorld.");
        var next = document.InstantiateEditorWorld(
            previous.Scene.ResourceManager, AssetRegistry, RuntimeActorFactory);
        try
        {
            EditorWorldOutlinerData.For(next).RestoreSessionStateFrom(
                previousOutliner, next.EnumerateActors(includePendingActors: true));
            BindCameraTargets(previous, next);
            next.Update(0f, tickActors: false);
        }
        catch
        {
            next.Dispose();
            throw;
        }

        if (_worldContext != null)
        {
            var exchanged = _worldContext.ExchangeCurrentWorld(next);
            if (!ReferenceEquals(exchanged, previous))
                throw new InvalidOperationException("WorldContext returned an unexpected previous World.");
        }
        World = next;
        previous.Dispose();
        Selection.Selected = null;
        MarkReloaded();
        WorldChanged?.Invoke(previous, next);
    }

    /// <summary>从当前编辑文档创建独立 RuntimeWorld；编辑 World 不进入运行时生命周期。</summary>
    public bool Play()
    {
        if (PlayState != EditorPlayState.Edit)
            return false;

        var document = SceneDocument.Capture(World);
        RegisterWorldAssets();
        var runtime = document.InstantiateWorld(World.Scene.ResourceManager, AssetRegistry, RuntimeActorFactory);
        try
        {
            RuntimeWorldInitializer?.Invoke(runtime);
            BindCameraTargets(World, runtime);
            _worldContext?.SetRuntimeWorld(runtime);
        }
        catch
        {
            runtime.Dispose();
            throw;
        }
        RuntimeWorld = runtime;
        PlayState = EditorPlayState.Play;
        PlayStateChanged?.Invoke(PlayState);
        return true;
    }

    /// <summary>停止运行时 World，释放其代理和生命周期状态，不回写编辑 World。</summary>
    public bool Stop()
    {
        if (PlayState != EditorPlayState.Play || RuntimeWorld == null)
            return false;

        var runtime = RuntimeWorld;
        RuntimeWorld = null;
        try
        {
            if (_worldContext?.RuntimeWorld == runtime)
                _worldContext.SetRuntimeWorld(null);
            else
                runtime.Dispose();
        }
        finally
        {
            PlayState = EditorPlayState.Edit;
            PlayStateChanged?.Invoke(PlayState);
        }
        return true;
    }

    public void Dispose() => Stop();

    /// <summary>
    /// 同步场景 CameraComponent 的 RenderTarget 到运行时副本。编辑器视口由 EditorViewportSession
    /// 独立持有目标，不需要调用此方法。
    /// </summary>
    public void SyncRuntimeCameraTargets()
    {
        if (RuntimeWorld != null)
            BindCameraTargets(World, RuntimeWorld);
    }

    /// <summary>注册一个只作用于新建 RuntimeWorld 的行为扩展。</summary>
    public void RegisterRuntimeBehavior(Action<World, SceneDocument> behavior)
        => RuntimeActorFactory.RegisterWorldBehavior(behavior);

    /// <summary>按场景持久化边界深复制 Actor；返回值尚未加入 World。</summary>
    public IReadOnlyList<ActorCloneResult> CloneActors(IEnumerable<Actor> actors)
    {
        RegisterWorldAssets();
        return EditorActorCloner.Clone(World, actors, AssetRegistry, RuntimeActorFactory);
    }

    private static void BindCameraTargets(World source, World destination)
    {
        var editorCameras = new List<CameraComponent>();
        var runtimeCameras = new List<CameraComponent>();
        source.CollectCameraComponents(editorCameras);
        destination.CollectCameraComponents(runtimeCameras);

        // RenderTarget 是窗口/贴图资源，不属于场景文档；只绑定同一稳定组件身份的相机。
        var editorCamerasByGuid = editorCameras.ToDictionary(camera => camera.ComponentGuid);
        foreach (var runtimeCamera in runtimeCameras)
        {
            if (editorCamerasByGuid.TryGetValue(runtimeCamera.ComponentGuid, out var editorCamera))
                runtimeCamera.RenderTarget = editorCamera.RenderTarget;
        }
    }

    /// <summary>登记当前 EditorWorld 引用的资产，供内容浏览器和导入/保存流程刷新索引。</summary>
    public void RegisterWorldAssets()
    {
        foreach (var actor in World.EnumerateActors(includePendingActors: true))
        {
            foreach (var component in actor.Components)
            {
                if (component is StaticMeshComponent staticMesh)
                {
                    RegisterAsset(staticMesh.Mesh);
                    RegisterAsset(staticMesh.Material);
                }
                else if (component is SkeletalMeshComponent skeletalMesh)
                {
                    RegisterAsset(skeletalMesh.Mesh);
                    RegisterAsset(skeletalMesh.Material);
                }
            }
        }

        void RegisterAsset(SceneResource? asset)
        {
            if (asset == null)
                return;
            var existing = AssetRegistry.Records.FirstOrDefault(record => record.AssetGuid == asset.AssetGuid);
            if (existing == null)
            {
                AssetRegistry.Register(asset);
                return;
            }
            if (ReferenceEquals(existing.Resource, asset))
                return;

            // 世界可能先于磁盘资源完成实例化。把实例附加回已有身份时保留扫描得到的
            // Content 路径和传递依赖，避免一次 Inspector 赋值破坏定位与 Cook 闭包。
            AssetRegistry.Register(asset,
                sourcePath: existing.SourcePath,
                cookedPath: existing.CookedPath,
                dependencies: existing.Dependencies,
                contentHash: existing.ContentHash,
                contentPath: existing.ContentPath);
        }
    }

    private void SetDirty(bool value)
    {
        if (IsDirty == value) return;
        IsDirty = value;
        DirtyChanged?.Invoke(value);
    }
}

public sealed class PropertyChangeCommand(object target, PropertyInfo property, object? oldValue, object? newValue) : IEditorCommand
{
    public string Description { get; } = $"Change {property?.Name ?? throw new ArgumentNullException(nameof(property))}";
    public void Execute() => property.SetValue(target, newValue);
    public void Undo() => property.SetValue(target, oldValue);
}
