using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>
/// 编辑器入口（保持文档示例可用）：
/// <c>ui.GetOrCreateCanvas(viewport.Id).Root = EditorLayout.Build(app.WorldContext.CurrentWorld).Root;</c>
/// 返回的 <see cref="EditorUi"/> 需配合 <see cref="EditorRefreshActor"/> 每帧调用 <see cref="EditorUi.Refresh"/>。
/// </summary>
public static class EditorLayout
{
    public static EditorUi Build(World? world, Action? backToHub = null) => new(world, backToHub);
}
