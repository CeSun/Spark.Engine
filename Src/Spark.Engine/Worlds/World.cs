using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Render;
using Spark.Engine.Resources;
using System.Runtime.ExceptionServices;

namespace Spark.Engine.Worlds;

public class World : IDisposable
{
    private List<Actor> _actors = [];
    private List<Actor> _pendingAddActors = [];
    private List<Actor> _pendingRemoveActors = [];
    private readonly HashSet<SceneResource> _ownedResources = [];
    private int _disposed;

    /// <summary>当前已进入 World 注册阶段的 Actor；编辑器预览中可能尚未 BeginPlay。</summary>
    public IReadOnlyList<Actor> Actors => _actors;

    /// <summary>
    /// 枚举 Actor。包含 pending 状态时返回已接受的逻辑结构：包含待添加 Actor，排除待移除 Actor。
    /// 该视图供编辑器保存和 Play 快照使用，不要求先推进一帧生命周期。
    /// </summary>
    public IEnumerable<Actor> EnumerateActors(bool includePendingActors = false)
    {
        ThrowIfDisposed();
        foreach (var actor in _actors)
        {
            if (includePendingActors && _pendingRemoveActors.Contains(actor))
                continue;
            yield return actor;
        }
        if (includePendingActors)
        {
            foreach (var actor in _pendingAddActors)
                yield return actor;
        }
    }

    /// <summary>渲染场景注册表：持有所有场景代理（网格/光源/…），每帧捕获为快照。</summary>
    public Scene Scene { get; }

    public World(ResourceManager resourceManager)
    {
        Scene = new Scene(resourceManager);
    }

    /// <summary>登记由当前 World 创建并独占的瞬态资源；World Dispose 时统一释放。</summary>
    public T OwnResource<T>(T resource) where T : SceneResource
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(resource);
        _ownedResources.Add(resource);
        return resource;
    }

    public void AddActor(Actor actor)
    {
        ThrowIfDisposed();
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (actor.World != null && !ReferenceEquals(actor.World, this))
            throw new InvalidOperationException("An Actor cannot belong to multiple Worlds.");
        if (_pendingRemoveActors.Remove(actor))
            return;
        if (_actors.Contains(actor) || _pendingAddActors.Contains(actor))
            return;

        actor.SetWorld(this);
        _pendingAddActors.Add(actor);
    }

    public void RemoveActor(Actor actor)
    {
        ThrowIfDisposed();
        if (actor == null) throw new ArgumentNullException(nameof(actor));

        // 同帧 Add 后 Remove：取消添加，actor 从不进入世界，代理不泄漏（中2）
        if (_pendingAddActors.Remove(actor))
        {
            actor.SetWorld(null);
            return;
        }

        if (!_actors.Contains(actor) || _pendingRemoveActors.Contains(actor))
            return;

        _pendingRemoveActors.Add(actor);
    }

    public void Update(float deltaTime) => Update(deltaTime, tickActors: true);

    /// <summary>
    /// 推进 World 生命周期；编辑器预览可传入 <c>false</c>，让 Actor/Component 注册渲染代理但不执行 gameplay Tick。
    /// </summary>
    public void Update(float deltaTime, bool tickActors)
    {
        ThrowIfDisposed();
        // 待添加：先进入编辑器/运行时共有的注册阶段；gameplay BeginPlay 在 tickActors 分支执行。
        foreach (var actor in _pendingAddActors.ToArray())
        {
            if (!_pendingAddActors.Contains(actor))
                continue;   // 已被同帧 RemoveActor 取消（中2）

            _actors.Add(actor);
            try
            {
                actor.RegisterComponents();
            }
            catch
            {
                // add 侧异常回滚：不留下半注册的 actor（中5）
                _actors.Remove(actor);
                _pendingAddActors.Remove(actor);
                actor.SetWorld(null);
                throw;
            }
            _pendingAddActors.Remove(actor);
        }

        // 待移除：对副本迭代 + try/finally 保证列表与 world 一致（中3/中5）
        foreach (var actor in _pendingRemoveActors.ToArray())
        {
            if (!_pendingRemoveActors.Contains(actor))
                continue;   // 已被取消

            try
            {
                DeactivateActor(actor);
            }
            finally
            {
                _actors.Remove(actor);
                actor.SetWorld(null);
                _pendingRemoveActors.Remove(actor);
            }
        }

        if (!tickActors)
        {
            // 编辑器预览不执行 gameplay Tick，但 Inspector/Gizmo 仍可能修改变换或资源；
            // 只同步代理，保证下一帧渲染看到最新状态。
            foreach (var actor in _actors.ToArray())
            {
                foreach (var component in actor.Components.ToArray())
                    component.RefreshSceneProxy();
            }
            return;
        }

        // 编辑器 World 可长期保持“已注册但未 BeginPlay”；只有可 Tick 的 World 才进入 gameplay 生命周期。
        foreach (var actor in _actors.ToArray())
        {
            if (actor.HasBegunPlay)
                continue;
            try
            {
                actor.BeginPlay();
            }
            catch
            {
                try { DeactivateActor(actor); } catch { /* 保留 BeginPlay 根因 */ }
                _actors.Remove(actor);
                actor.SetWorld(null);
                throw;
            }
        }

        // 更新：副本迭代，回调重入增删不影响本帧集合（中3）
        foreach (var actor in _actors.ToArray())
        {
            actor.Update(deltaTime);
        }
    }

    /// <summary>收集本帧活跃相机（绑定了渲染目标的 CameraComponent，即"视图"）。</summary>
    public void CollectCameras(List<CameraComponent> result)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(result);
        foreach (var actor in _actors)
        {
            foreach (var component in actor.Components)
            {
                if (component is CameraComponent camera && camera.RenderTarget != null)
                    result.Add(camera);
            }
        }
    }

    /// <summary>
    /// 收集所有相机组件，包括尚未进入生命周期的 pending Actor 和未绑定渲染目标的相机。
    /// 该入口用于编辑器在 Play 实例化后恢复窗口/离屏目标；正常渲染仍使用 <see cref="CollectCameras"/>。
    /// </summary>
    public void CollectCameraComponents(List<CameraComponent> result, bool includePendingActors = true)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(result);

        static void Collect(IEnumerable<Actor> actors, List<CameraComponent> destination)
        {
            foreach (var actor in actors.OrderBy(actor => actor.ActorGuid))
            {
                foreach (var component in actor.Components.OrderBy(component => component.ComponentGuid))
                {
                    if (component is CameraComponent camera)
                        destination.Add(camera);
                }
            }
        }

        Collect(_actors, result);
        if (includePendingActors)
            Collect(_pendingAddActors, result);
    }

    /// <summary>结束所有 Actor 生命周期并释放场景代理。可重复调用。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        foreach (var actor in _pendingAddActors)
            actor.SetWorld(null);
        _pendingAddActors.Clear();
        _pendingRemoveActors.Clear();

        Exception? firstException = null;
        foreach (var actor in _actors.ToArray())
        {
            try
            {
                DeactivateActor(actor);
            }
            catch (Exception ex)
            {
                firstException ??= ex;
            }
            finally
            {
                actor.SetWorld(null);
            }
        }

        _actors.Clear();

        try
        {
            Scene.Dispose();
        }
        catch (Exception ex)
        {
            firstException ??= ex;
        }

        foreach (var resource in _ownedResources)
        {
            try { resource.Dispose(); }
            catch (Exception ex) { firstException ??= ex; }
        }
        _ownedResources.Clear();

        if (firstException != null)
            ExceptionDispatchInfo.Capture(firstException).Throw();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(World));
    }

    private static void DeactivateActor(Actor actor)
    {
        Exception? firstException = null;
        if (actor.HasBegunPlay)
        {
            try { actor.EndPlay(); }
            catch (Exception exception) { firstException = exception; }
        }
        try { actor.UnregisterComponents(); }
        catch (Exception exception) { firstException ??= exception; }
        if (firstException != null)
            ExceptionDispatchInfo.Capture(firstException).Throw();
    }
}
