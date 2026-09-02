using Spark.Engine.Worlds;

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

        var viewport = application.WindowManager.GetViewport(application.WindowManager.MainWindow)
            ?? throw new InvalidOperationException("The editor requires a viewport for the main window.");

        var editorUi = new EditorUi(world);
        var canvas = application.UIManager.GetOrCreateCanvas(viewport.Id);
        canvas.Root = editorUi.Root;
        canvas.GlobalKeyDown = (key, keysDown, focused) => editorUi.HandleGlobalKey(key, keysDown, focused);
        application.Ticks.Register(_ => editorUi.Refresh());

        registration.Configure?.Invoke(application, editorUi);
    }
}

internal sealed class EditorRegistration
{
    public Action<EngineApplication, EditorUi>? Configure { get; set; }
}
