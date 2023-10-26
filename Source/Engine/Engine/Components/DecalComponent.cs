using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;

namespace Spark.Engine.Components;

public class DecalComponent : PrimitiveComponent
{
    public DecalComponent(Actor actor) : base(actor)
    {
    }


    private Material? _Material;
    public Material? Material 
    {
        get => _Material;
        set
        {
            _Material = value;
            if (_Material != null)
            {
                foreach (var texture in _Material.Textures)
                {
                    if (texture != null)
                        texture.InitRender(gl);
                }

            }
        }
    }
}
