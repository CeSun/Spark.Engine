using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Render;

namespace Spark.Engine.Worlds;

public class World
{
    private List<Actor> _actors = [];
    private List<Actor> _pendingAddActors = [];
    private List<Actor> _pendingRemoveActors = [];

    /// <summary>当前已进入世界的 Actor（BeginPlay 已调用）。</summary>
    public IReadOnlyList<Actor> Actors => _actors;

    /// <summary>渲染场景注册表：持有所有场景代理（网格/光源/…），每帧捕获为快照。</summary>
    public Scene Scene { get; } = new();

    public void AddActor(Actor actor)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (_actors.Contains(actor) || _pendingAddActors.Contains(actor))
            return;

        actor.SetWorld(this);
        _pendingAddActors.Add(actor);
    }

    public void RemoveActor(Actor actor)
    {
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

    public void Update(float deltaTime)
    {
        // 待添加：对副本迭代 + 只移除已处理项；BeginPlay 重入 Add/Remove 不破坏集合（中3）
        foreach (var actor in _pendingAddActors.ToArray())
        {
            if (!_pendingAddActors.Contains(actor))
                continue;   // 已被同帧 RemoveActor 取消（中2）

            _actors.Add(actor);
            try
            {
                actor.BeginPlay();
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
                actor.EndPlay();
            }
            finally
            {
                _actors.Remove(actor);
                actor.SetWorld(null);
                _pendingRemoveActors.Remove(actor);
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
        foreach (var actor in _actors)
        {
            foreach (var component in actor.Components)
            {
                if (component is CameraComponent camera && camera.RenderTarget != null)
                    result.Add(camera);
            }
        }
    }
}
