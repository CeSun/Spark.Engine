using Spark.Engine.Actors;
using Spark.Engine.Components;

namespace Spark.Engine.Editor;

/// <summary>不进入场景文件/Cook 的编辑器视口相机宿主；Play 时由 EditorContext 单独复制。</summary>
[SceneTransient]
public sealed class EditorViewportCameraActor : Actor
{
}
