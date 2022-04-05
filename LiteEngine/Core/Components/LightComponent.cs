using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components;

public class LightComponent : RenderableComponent
{
    protected LightComponent(Component parent, string name) : base(parent, name)
    {
        IsEnable = true;
    }

    public bool IsEnable { get; set; }



}

