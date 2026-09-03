using Spark.Engine.Worlds;
using Spark.Engine.Platforms;

namespace Spark.Engine.Editor;

internal sealed class EditorApplicationInitializer(EditorRegistration registration) : IEngineApplicationInitializer
{
    public void Initialize(EngineApplication application)
    {
        var world = application.WorldContext.CurrentWorld;
        if (world == null)
        {
            world = new World(application.ResourceManager);
            application.WorldContext.CurrentWorld = world;
        }
        application.WorldContext.TickCurrentWorld = false;

        var viewport = application.WindowManager.GetViewport(application.WindowManager.MainWindow)
            ?? throw new InvalidOperationException("The editor requires a viewport for the main window.");

        var project = registration.ProjectDirectory == null
            ? EditorProject.TryFind()
            : EditorProject.Open(registration.ProjectDirectory);
        if (project != null)
            project.EnsureDescriptor();
        var editorUi = new EditorUi(
            world,
            sceneService: registration.SceneService,
            worldContext: application.WorldContext,
            project: project);
        if (project != null && Directory.Exists(project.ContentDirectory))
            editorUi.ScanAssetDirectory(project.ContentDirectory);
        if (application.WindowManager.MainWindow is IFileDropWindow fileDropWindow)
            fileDropWindow.FilesDropped += editorUi.HandleFilesDropped;
        var canvas = application.UIManager.GetOrCreateCanvas(viewport.Id);
        canvas.Root = editorUi.Root;
        canvas.GlobalKeyDown = (key, keysDown, focused) => editorUi.HandleGlobalKey(key, keysDown, focused);

        if (application.WindowManager.MainWindow is ICloseRequestWindow closeRequestWindow)
        {
            var allowNextClose = false;
            closeRequestWindow.CloseRequested = () =>
            {
                if (allowNextClose)
                {
                    allowNextClose = false;
                    return true;
                }

                return editorUi.RequestClose(() =>
                {
                    allowNextClose = true;
                    application.WindowManager.MainWindow.Close();
                });
            };
        }
        application.Ticks.Register(_ => editorUi.Refresh());

        registration.Configure?.Invoke(application, editorUi);
    }
}

internal sealed class EditorRegistration
{
    public Action<EngineApplication, EditorUi>? Configure { get; set; }

    public IEditorSceneService? SceneService { get; set; }
    public string? ProjectDirectory { get; set; }
}
