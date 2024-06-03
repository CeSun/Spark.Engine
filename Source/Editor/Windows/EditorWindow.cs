using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Editor.Panels;
using Editor.Subsystem;
using Spark.Engine;

namespace Editor.Windows;

public class EditorWindow : WindowBase
{
    public EditorWindow(Engine engine) : base(engine)
    {
        var imGuiSubSystem = engine.GetSubSystem<ImGuiSubSystem>()!;
        _panels.AddRange([ 
            new MainPanel(imGuiSubSystem),
            new LevelPanel(imGuiSubSystem),
            new PlaceActorsPanel(imGuiSubSystem),
            new ContentViewerPanel(imGuiSubSystem),
            new OutlinerPanel(imGuiSubSystem),
            new DetailsPanel(imGuiSubSystem)
        ]);
    }
}

