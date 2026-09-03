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
    /// <param name="projectDirectory">
    /// 可选的项目目录。建议使用相对于进程启动目录的路径（例如 <c>Demo</c> 或 <c>.</c>）；
    /// 该路径按当前目录直接解析，不会向父目录推断。
    /// </param>
    public static EngineBuilder UseEditor(
        this EngineBuilder builder,
        Action<EngineApplication, EditorUi>? configure = null,
        IEditorSceneService? sceneService = null,
        string? projectDirectory = null)
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
        if (projectDirectory != null)
            registration.ProjectDirectory = Path.GetFullPath(projectDirectory);

        if (registration.ProjectDirectory == null)
            registration.ProjectDirectory = EditorProject.TryFind()?.RootDirectory;
        if (registration.ProjectDirectory != null)
        {
            var options = builder.Services
                .Select(descriptor => descriptor.ImplementationInstance)
                .OfType<EngineOptions>()
                .SingleOrDefault();
            if (options != null)
                options.WorkingDirectory = registration.ProjectDirectory;
        }

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IEngineApplicationInitializer, EditorApplicationInitializer>());

        return builder;
    }
}
