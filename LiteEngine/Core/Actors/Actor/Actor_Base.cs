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
    internal Actor()
    {
        Name = "Actor";
        RootComponent = new Component();
    }
    public string Name { get; set; }

    public void Destory()
    {
        this.World.DestoryActor(this);
    }

    public Component RootComponent { get; private set; }

}