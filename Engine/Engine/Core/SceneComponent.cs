using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class SceneComponent
{

    public Vector3 WorldLocation { get; set; }

    public Vector3 WorldScale { get; set; }

    public Quaternion WorldRotation { get; set; }

    public Vector3 RelativeLocation { get; set; }
    public Vector3 RelativeScale { get; set; }
    public Quaternion RelativeRotation { get; set; }

}
