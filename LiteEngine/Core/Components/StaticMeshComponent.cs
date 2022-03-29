using LiteEngine.Core.Render;
using LiteEngine.Core.Resources;
using Silk.NET.OpenGL;


namespace LiteEngine.Core.Components;

public class StaticMeshComponent : RenderableComponent
{
    public StaticMesh? StaticMesh
    {
        get
        {
            return _StaticMesh;
        }
        set
        {
            if (StaticMesh != null)
                StaticMesh.Parent = null;
            _StaticMesh = value;
            if (_StaticMesh != null)
            {
                _StaticMesh.Parent = this;
            }
        }
    }
    private StaticMesh? _StaticMesh;
    public StaticMeshComponent(Component parent, string name) : base(parent, name)
    {
      
    }

    public override unsafe void Render()
    {
        base.Render();
        StaticMesh?.Render();
    }
}
