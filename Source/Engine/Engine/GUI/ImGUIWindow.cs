using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.GUI;

public class ImGUIWindow
{
    Level level;
    public ImGUIWindow(Level level)
    {
        this.level = level;
    }

    public void AddToViewPort()
    {
        level.ImGuiWarp.AddCanvas(this);
    }


    public void RemoveFromViewPort()
    {
        level.ImGuiWarp.RemoveCanvas(this);
    }
    public virtual void Render(double deltaTime)
    {

    }
}
