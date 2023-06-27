using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
