using Spark.Engine.Components;
using Spark.Engine.Render;
using Spark.Engine.Render.Common;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>
/// Owns an editor viewport camera independently of scene and runtime Worlds. The same session therefore keeps
/// its view transform, bookmarks and render target while the active World is reloaded or switched for Play.
/// </summary>
public sealed class EditorViewportSession : ICameraSnapshotSource, IDisposable
{
    private readonly IDisposable _registration;
    private int _disposed;

    public EditorViewportSession(CameraSnapshotSourceRegistry sources, RenderTarget renderTarget)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(renderTarget);
        Camera = new CameraComponent { RenderTarget = renderTarget };
        _registration = sources.Register(this);
    }

    public Guid SessionId { get; } = Guid.NewGuid();

    /// <summary>The detached camera used by editor navigation, picking and gizmo projection.</summary>
    public CameraComponent Camera { get; }

    public RenderTarget? RenderTarget
    {
        get => Camera.RenderTarget;
        set => Camera.RenderTarget = value;
    }

    public bool IsEnabled { get; set; } = true;

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public void CollectCameraSnapshots(World activeWorld, FrameBuffer<CameraSnapshot> destination)
    {
        ArgumentNullException.ThrowIfNull(activeWorld);
        ArgumentNullException.ThrowIfNull(destination);
        if (!IsEnabled || IsDisposed || RenderTarget is not RenderTarget target)
            return;

        destination.Add(new CameraSnapshot(
            target.Id,
            Camera.GetViewMatrix(),
            Camera.GetProjectionMatrix(target.AspectRatio),
            Camera.ClearColor));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _registration.Dispose();
    }
}
