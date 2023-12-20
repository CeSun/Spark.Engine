using ImGuiNET;
using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.ImGui;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;
using Spark.Util;
using System.ComponentModel;
using System.Numerics;

namespace Spark.Engine.GUI;

public class ImGuiWarp
{
    Level CurrentLevel { get; set; }

    public ImGuiWarp(Level level)
    {
        CurrentLevel = level;
    }


    ImGuiController? Controller;
    public void Init()
    {
        if (CurrentLevel.CurrentWorld.Engine.IsMobile)
            return;
        try
        {
            // Controller = new ImGuiController(CurrentLevel.Engine.Gl, CurrentLevel.CurrentWorld.Engine.View, CurrentLevel.CurrentWorld.Engine.Input);
        } 
        catch (Exception e)
        { 
            Console.WriteLine(e.ToString());
        }
    }

    public void Render(double DeltaTime)
    {
        if (Controller == null)
            return;
        if (CurrentLevel.Engine.Gl == null)
            return;
        
        Controller?.Update((float)DeltaTime);
        CurrentLevel.Engine.Gl.PushGroup("GUI Pass");
        Controller?.Render();
        CurrentLevel.Engine.Gl.PopGroup();

    }

    public void Fini()
    {
        if (CurrentLevel.CurrentWorld.Engine.IsMobile)
            return;
        Controller?.Dispose();
    }
}
