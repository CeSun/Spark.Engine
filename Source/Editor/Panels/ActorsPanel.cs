using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

[AddPanelToEditor]
public class ActorsPanel : IPanel
{

    public ActorsPanel() { }

    string s = "";
    public void Renderer(double DeltaTime)
    {
        ImGui.Begin("Actors");
        ImGui.InputText("测试", ref s, 100);
        ImGui.End();


    }
}
