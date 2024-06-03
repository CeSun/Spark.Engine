
using Editor.Panels;
using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;

namespace Editor.Windows;

public class StartWindow : WindowBase
{
    public StartWindow(Engine engine): base(engine)
    {
        var imGuiSubSystem = engine.GetSubSystem<ImGuiSubSystem>()!;
        _panels.AddRange([
            new StartPanel(imGuiSubSystem)
        ]);
    }

}

