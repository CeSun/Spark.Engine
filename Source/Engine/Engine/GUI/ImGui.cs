using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.GUI;

public class ImGui
{
    ImGuiController? Controller;
    public void Init()
    {
        Controller = new ImGuiController(gl, Engine.Instance.View, Engine.Instance.Input);
    }


    public void Render(double DeltaTime)
    {
        Controller?.Update((float)DeltaTime);
        ImGuiNET.ImGui.ShowDemoWindow();

        Controller?.Render();

    }


    public void Fini()
    {
        Controller?.Dispose();
    }
}
