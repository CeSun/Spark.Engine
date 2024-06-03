using ImGuiNET;
using Editor.GUI;
using Editor.Subsystem;

namespace Editor.Panels;


public class MainPanel(ImGuiSubSystem imGuiSubSystem) : BasePanel(imGuiSubSystem)
{
    public override void Render(double deltaTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGui.Begin("MainWindow", ImGuiWindowFlags.None | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBringToFrontOnFocus);
        if (ImGui.BeginMainMenuBar())
        {
            if(ImGui.BeginMenu("File"))
            {
                ImGui.MenuItem("Project Hub");
                ImGui.MenuItem("New Level");
                ImGui.MenuItem("Open Level");
                ImGui.MenuItem("Save Current Level");
                ImGui.MenuItem("Exit");
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Editor"))
            {
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Build"))
            {

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Help"))
            {

                ImGui.MenuItem("About");
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }


        ImGui.PushFont(ImGuiSubSystem.Fonts["forkawesome"]);
        ImGui.Button(new string([(char)0x000f0c7])+ "##editorsave");
        ImGui.SameLine();
        ImGui.Button(new string([(char)0x000f04b]) + "##editorrun");
        ImGui.PopFont();
        ImGui.DockSpace(viewport.ID);

        ImGui.End();

    }
}
