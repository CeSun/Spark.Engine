using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.GUI;

public class ImGUIWindow
{
    protected Level Level;
    public ImGUIWindow(Level level)
    {
        this.Level = level;
    }

    public void AddToViewPort()
    {
        Level.ImGuiWarp.AddCanvas(this);
    }


    public void RemoveFromViewPort()
    {
        Level.ImGuiWarp.RemoveCanvas(this);
    }
    public virtual void Render(double deltaTime)
    {

    }
}
