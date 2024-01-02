using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

public class LevelPanel : ImGUIWindow
{
    EditorSubsystem EditorSubsystem;
    public LevelPanel(Level level) : base(level)
    {
        var system = level.Engine.GetSubSystem<EditorSubsystem>();
        if (system != null)
            EditorSubsystem = system;
        else
            throw new Exception("no editor subsystem");
    }

    public override void Render(double deltaTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.Begin("New Level##levelpanel");

        var windowSize = ImGui.GetContentRegionAvail();
        EditorSubsystem.LevelWorld.WorldMainRenderTarget.Resize((int)windowSize.X, (int)windowSize.Y);
        ImGui.Image((nint)EditorSubsystem.LevelWorld.WorldMainRenderTarget.GBufferIds[0], windowSize);
        ImGui.End();
    }
}
