using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Editor.Subsystem;
using ImGuiNET;

namespace Editor.Panels;

public class StartPanel(ImGuiSubSystem imGuiSubSystem) : BasePanel(imGuiSubSystem)
{
    private string _projectName = string.Empty;
    private string _projectDir = string.Empty;
    public override void Render(double deltaTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        
        ImGui.Begin("MainWindow",  ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoTitleBar);

        if (ImGui.Button("新建"))
        {
            ImGui.OpenPopup("创建项目##CreateProjectModal");
        }

        bool t = true;
        if (ImGui.BeginPopupModal("创建项目##CreateProjectModal", ref t, ImGuiWindowFlags.None))
        {
            ImGui.Text("项目名称：");
            ImGui.InputText("##projectName", ref _projectName, 128 );
            ImGui.Text("项目路径");
            ImGui.InputText("##projectDir", ref _projectDir, 128);
            ImGui.SameLine();
            ImGui.Button("浏览");
            ImGui.Button("创建");
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        ImGui.Button("导入");
        ImGui.SameLine();
        ImGui.Button("扫描");


        ImGui.End();
    }
}
