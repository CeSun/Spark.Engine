using ImGuiNET;
using Spark.Engine;
using Spark.Engine.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels
{
    public class DetailsPanel : ImGUIWindow
    {
        public DetailsPanel(Level level) : base(level)
        {
        }

        public override void Render(double deltaTime)
        {

            ImGui.Begin("Details##details");
            ImGui.End();
        }
    }
}
