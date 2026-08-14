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
        if (!_actors.Contains(actor) || _pendingRemoveActors.Contains(actor))
            return;

        _pendingRemoveActors.Add(actor);
    }

    public void Update(float deltaTime)
    {
        foreach (var actor in _pendingAddActors)
        {
            _actors.Add(actor);
            actor.BeginPlay();
        }
        _pendingAddActors.Clear();

        foreach (var actor in _pendingRemoveActors)
        {
            actor.EndPlay();
            _actors.Remove(actor);
            actor.SetWorld(null);
        }
        _pendingRemoveActors.Clear();

        foreach (var actor in _actors)
        {
            actor.Update(deltaTime);
        }
    }

    /// <summary>收集本帧活跃相机（绑定了渲染目标的 CameraComponent）。</summary>
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

    /// <summary>收集本帧可渲染物体（持有网格的 StaticMeshComponent）。</summary>
    public void CollectRenderItems(List<RenderItem> result)
    {
        foreach (var actor in _actors)
        {
            foreach (var component in actor.Components)
            {
                if (component is StaticMeshComponent meshComponent && meshComponent.Mesh != null)
                {
                    result.Add(new RenderItem(meshComponent.Mesh.MeshId, meshComponent.WorldTransform));
                }
            }
        }
    }
}
