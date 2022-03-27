using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LiteEngine.Core.Components;

namespace LiteEngine.Core.Actors;


public partial class Actor
{
    public Actor()
    {
        World.AddActor(this);
        Name = "Actor";
        RootComponent = new RootComponent(this);
    }

    public string Name { get; set; }

    public void Destory()
    {
        RootComponent.Destory();
        World.DestoryActor(this);
    }

    public RootComponent RootComponent { get; private set; }

}