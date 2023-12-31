using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;


[AddPanelToEditor]
public class MainPanel : IPanel
{
    public void Renderer(double DeltaTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.Begin("MainWindow", ImGuiWindowFlags.None | ImGuiWindowFlags.NoTitleBar);
        if(ImGui.BeginMainMenuBar())
        {
            ImGui.Button("File");
            ImGui.Button("Run");
            ImGui.EndMainMenuBar();
        }

        ImGui.DockSpace(viewport.ID);



        ImGui.End();
    }
}
