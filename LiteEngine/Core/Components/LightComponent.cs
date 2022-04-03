using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components
{
    public class LightComponent : RenderableComponent
    {
        protected LightComponent(Component parent, string name) : base(parent, name)
        {
            IsEnable = true;
            Color = new Vector3(1, 1, 1);
        }

        public bool IsEnable { get; set; }
        public Vector3 Color { get; set; }



    }
}
