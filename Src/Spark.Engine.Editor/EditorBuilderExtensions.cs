using Spark.Engine.Builder;
using Spark.Engine.Render.UI;

namespace Spark.Engine.Editor;

public static class EditorBuilderExtensions
{
    /// <summary>启用编辑器：注册 UI 渲染覆盖层（编辑器界面依赖 UI overlay）。</summary>
    public static EngineBuilder UseEditor(this EngineBuilder builder)
    {
        builder.UseUI();
        return builder;
    }
}
