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

public class ImGuiSystem
{

    List<ImGUICanvas> ImGUICanvasList = new List<ImGUICanvas>();
    public void AddCanvas(ImGUICanvas imGUICanvas)
    {
        ImGUICanvasList.Add(imGUICanvas);
       
    }

    public void RemoveCanvase(ImGUICanvas imGUICanvas)
    {
        ImGUICanvasList.Remove(imGUICanvas);
        

    }
    Level CurrentLevel { get; set; }

    public ImGuiSystem(Level level)
    {
        CurrentLevel = level;
    }


    ImGuiController? Controller;
    public void Init()
    {
        try
        {
            Controller = new ImGuiController(CurrentLevel.Engine.Gl, CurrentLevel.CurrentWorld.Engine.View, CurrentLevel.CurrentWorld.Engine.Input);
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
        ImGUICanvasList.ForEach(item => item.Render(DeltaTime));
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
