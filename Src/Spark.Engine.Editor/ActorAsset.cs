using Spark.Engine.Actors;
using Spark.Engine.Resources;

namespace Spark.Engine.Editor;

/// <summary>可在 Actor 编辑器中编辑、预览并保存为 `.asset` 的 Actor 定义。</summary>
public sealed class ActorAsset : SceneResource
{
    public SceneActorDocument Document { get; private set; }
    public Actor? EditableActor { get; }

    public ActorAsset(SceneActorDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        if (Document.ActorGuid == Guid.Empty)
            throw new InvalidDataException("Actor assets require a non-empty ActorGuid.");
    }

    public ActorAsset(Actor actor) : this(SceneDocument.CaptureActor(actor))
    {
        EditableActor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    public void SyncFromActor(Actor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        Document = SceneDocument.CaptureActor(actor);
    }
}
