using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spark.Engine.Builder;
using Spark.Engine.Render.UI;

namespace Spark.Engine.Editor;

public static class EditorBuilderExtensions
{
    /// <summary>
    /// 启用编辑器。编辑器会在游戏内容初始化后自动挂载到主窗口，并随当前世界逐帧刷新。
    /// </summary>
    /// <param name="builder">引擎 Builder。</param>
    /// <param name="configure">可选的编辑器 UI 配置，在自动挂载后执行。</param>
    public static EngineBuilder UseEditor(
        this EngineBuilder builder,
        Action<EngineApplication, EditorUi>? configure = null,
        IEditorSceneService? sceneService = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseUI();

        var registration = builder.Services
            .Where(descriptor => descriptor.ServiceType == typeof(EditorRegistration))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<EditorRegistration>()
            .SingleOrDefault();

        if (registration == null)
        {
            registration = new EditorRegistration();
            builder.Services.AddSingleton(registration);
        }

        if (configure != null)
            registration.Configure += configure;

        if (sceneService != null)
            registration.SceneService = sceneService;

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IEngineApplicationInitializer, EditorApplicationInitializer>());

        return builder;
    }
}
