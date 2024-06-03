using Editor.Subsystem;
using Spark.Engine;

namespace Editor.GUI;

public class BasePanel(ImGuiSubSystem imGuiSubSystem)
{
    protected ImGuiSubSystem ImGuiSubSystem { get; } = imGuiSubSystem;

    protected Engine Engine { get; } = imGuiSubSystem.Engine;
    public void AddToViewPort()
    {
        ImGuiSubSystem.AddCanvas(this);
    }


    public void RemoveFromViewPort()
    {
        ImGuiSubSystem.RemoveCanvas(this);
    }

    public virtual void Render(double deltaTime)
    {

    }
}
