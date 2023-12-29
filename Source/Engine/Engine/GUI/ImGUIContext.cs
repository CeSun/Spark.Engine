using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.GUI;

public class ImGUIContext
{
    Level level;
    
    public ImGUIContext(Level level)
    {
        this.level = level;
    }

    public void AddToViewPort()
    {
        level.ImGuiWarp.AddCanvas(this);
    }
    public virtual void Render(double deltaTime)
    {

    }
}
