
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

    public override void Open()
    {
        base.Open();

        if (_engine.Args.Count > 0)
        {
            OnOpenProject(_engine.Args[0]);
        }
    }



    public void OnCreateProject(string projectName, string projectDir)
    {
        if (Directory.Exists(projectDir + "/" + projectName))
            return;
        Directory.CreateDirectory(projectDir + "/" + projectName);
        Directory.CreateDirectory(projectDir + "/" + projectName + "/Config");
        Directory.CreateDirectory(projectDir + "/" + projectName + "/Content");
        using var sw = new StreamWriter(projectDir + "/" + projectName + "/" + projectName + ".sproject");
        sw.Write("{}");
    }


    public void OnOpenProject(string projectFilePath)
    {
        Close();
        new EditorWindow(_engine).OpenProject(projectFilePath);

    }
}

