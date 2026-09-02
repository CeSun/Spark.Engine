using System.Reflection;
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
    private object? _selected;
    public object? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            Changed?.Invoke(value);
        }
    }

    public event Action<object?>? Changed;
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
        if (_worldContext != null && !ReferenceEquals(_worldContext.CurrentWorld, previous))
            throw new InvalidOperationException("EditorContext no longer owns WorldContext.CurrentWorld.");
        var next = document.InstantiateEditorWorld(
            previous.Scene.ResourceManager, AssetRegistry, RuntimeActorFactory);
        try
        {
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

    /// <summary>同步编辑相机的 RenderTarget 到运行时相机，用于 UIRenderView resize 等目标替换场景。</summary>
    public void SyncRuntimeCameraTargets()
    {
        if (RuntimeWorld != null)
            BindCameraTargets(World, RuntimeWorld);
    }

    /// <summary>注册一个只作用于新建 RuntimeWorld 的行为扩展。</summary>
    public void RegisterRuntimeBehavior(Action<World, SceneDocument> behavior)
        => RuntimeActorFactory.RegisterWorldBehavior(behavior);

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

    private void RegisterWorldAssets()
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
            AssetRegistry.Register(asset);
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
