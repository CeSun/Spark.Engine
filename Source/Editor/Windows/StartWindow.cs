
using Editor.Panels;
using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;

namespace Editor.Windows;

public class StartWindow : WindowBase
{
    private StartPanel startPanel;
    public StartWindow(Engine engine): base(engine)
    {
        var imGuiSubSystem = engine.GetSubSystem<ImGuiSubSystem>()!;
        startPanel = new StartPanel(imGuiSubSystem);
        _panels.AddRange([
            startPanel
        ]);

        startPanel.OnCreateProject += OnCreateProject;
        startPanel.OnOpenProject += OnOpenProject;
    }

    public void OnCreateProject(string projectName, string projectDir)
    {

    }


    public void OnOpenProject(string projectDir)
    {
        Close();

        new EditorWindow(_engine).Open();

    }
}

