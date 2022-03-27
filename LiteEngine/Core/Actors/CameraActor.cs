using LiteEngine.Core.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Actors;

public class CameraActor : Actor
{
    public CameraActor():base()
    {
        var camera = new CameraCpmponent("TestComponent");
        camera.Attach(RootComponent);


    }
    
}
