using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;


public class MainPanel : ImGUIWindow
{
    public MainPanel(Level level) : base(level)
    {

    }


    public override void Render(double DeltaTime)
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


        ImGui.Button("Save##editorsave");
        ImGui.SameLine();
        ImGui.Button("Run##editorrun");
        ImGui.DockSpace(viewport.ID);

        ImGui.End();

    }
}
