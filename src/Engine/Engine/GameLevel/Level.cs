using Spark.Engine.Components;
using Spark.Engine.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.GameLevel;

public class Level
{
    public Engine Engine { get; private set; }
    public Level(Engine engine)
    {
        Engine = engine;
    }

    private UpdatableList<PrimitiveComponent> _PrimitiveComponents = new UpdatableList<PrimitiveComponent>();
    public IReadOnlyCollection<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;


    public void AddComponent(PrimitiveComponent Component)
    {
        _PrimitiveComponents.Add(Component);

    }

    public void RemoveComponent(PrimitiveComponent Component)
    {
        if (_PrimitiveComponents.Contains(Component))
        {
            _PrimitiveComponents.Remove(Component);
        }
    }

    public void Update(double DeltaTime)
    {
        _PrimitiveComponents.Update();
    }
}
