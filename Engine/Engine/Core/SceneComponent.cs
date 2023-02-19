using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public partial class SceneComponent
{

    public Level Level { get; private set; }
    public SceneComponent(Level level)
    {
        Children = new List<SceneComponent>();
        _TransformDirtyFlag = true;
        Name = "Scene Component";
        if (level == null)
            throw new Exception("关卡不能为空");
        Level = level;

    }


    [Property(Category = "Info", DisplayName = "Component Name")]
    public string Name;

    public void Tick (double DeltaTime)
    {

    }

}
