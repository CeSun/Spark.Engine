using System.Collections.Concurrent;

namespace Spark.Engine.Render;

/// <summary>
/// 网格资产库：按 MeshId 去重的「首次引用自动上传」门面。
/// 逻辑线程 <see cref="EnsureUploaded"/>（去重 + 入队），渲染线程消费队列建 GPU 资源（见 SceneRenderer）。
/// </summary>
public sealed class MeshLibrary
{
    private readonly ConcurrentQueue<StaticMesh> _pendingUploads;
    private readonly HashSet<int> _uploaded = new();

    public MeshLibrary(ConcurrentQueue<StaticMesh> pendingUploads)
    {
        _pendingUploads = pendingUploads ?? throw new ArgumentNullException(nameof(pendingUploads));
    }

    /// <summary>首次引用时入队上传；重复引用（同 MeshId）直接忽略。</summary>
    public void EnsureUploaded(StaticMesh? mesh)
    {
        if (mesh == null)
            return;

        if (_uploaded.Add(mesh.MeshId))
            _pendingUploads.Enqueue(mesh);
    }
}
