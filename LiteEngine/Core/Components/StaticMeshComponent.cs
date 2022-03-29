using LiteEngine.Core.Render;
using LiteEngine.Core.Resources;
using Silk.NET.OpenGL;


namespace LiteEngine.Core.Components;

public class StaticMeshComponent : RenderableComponent
{
    public StaticMesh? StaticMesh { get; set; }

    public StaticMeshComponent(Component parent, string name) : base(parent, name)
    {
      
    }

    public override unsafe void Render()
    {
        base.Render();
        StaticMesh?.Render();
    }
}
