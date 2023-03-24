using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.OpenGL;
using Spark.Engine.Core.Components;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core;

public class Level
{
    private List<PrimitiveComponent> _PrimitiveComponents { get; set; }

    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents { get => _PrimitiveComponents;  }

    public void RegistComponent(PrimitiveComponent component)
    {
        if (PrimitiveComponents.Contains(component))
        {
            return;
        }
        _PrimitiveComponents.Add(component);
    }

    public void UnregistComponent(PrimitiveComponent component)
    {
        if (!PrimitiveComponents.Contains(component))
        {
            return;
        }
        _PrimitiveComponents.Remove(component);
    }
    public World CurrentWorld { private set; get; }
    public Level(World world)
    {
        CurrentWorld = world;
    }

    public void BeginPlay()
    {

    }

    public void Destory() 
    { 

    }

    public void Update(double DeltaTime)
    {

    }

    public void Render(double DeltaTime)
    {
        _PrimitiveComponents.ForEach(component => component.Render(DeltaTime));
    }
}
