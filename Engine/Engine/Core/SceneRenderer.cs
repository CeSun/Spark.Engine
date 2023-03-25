using Spark.Engine.Core.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class SceneRenderer
{
    World World { get; set; }

    public CameraComponent? CurrentCamera { get; set; }
    public SceneRenderer(World world) 
    {
        World = world;
    }

    public void Render(double DeltaTime)
    {
        foreach(var component in World.CurrentLevel.PrimitiveComponents)
        {
            if (component.IsDestoryed == false)
            {
                component.Render(DeltaTime);
            }
        }
        
    }
}
