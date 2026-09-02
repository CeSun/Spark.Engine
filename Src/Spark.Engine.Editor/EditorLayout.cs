using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>
/// 手动创建编辑器布局的低层入口。常规应用应通过
/// <see cref="EditorBuilderExtensions.UseEditor"/>
/// 启用并挂载编辑器：
/// <c>ui.GetOrCreateCanvas(viewport.Id).Root = EditorLayout.Build(world).Root;</c>
/// </summary>
public static class EditorLayout
{
    public static EditorUi Build(
        World world,
        Action? backToHub = null,
        IEditorSceneService? sceneService = null)
        => new(world, backToHub, sceneService);
}
