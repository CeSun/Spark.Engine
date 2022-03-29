using LiteEngine.Core.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Actors;

public class CameraActor : Actor
{
    public CameraCpmponent CameraComponent { get; private set; }
    public CameraActor():base()
    {
        CameraComponent = new CameraCpmponent(RootComponent, "TestComponent");
    }
    
}
