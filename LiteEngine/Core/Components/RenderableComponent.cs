using LiteEngine.Core.Render;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components
{
    public class RenderableComponent : Component
    {
        protected GL gl { get => Engine.Instance.Gl; }
        public RenderableComponent(Component parent, string name) : base(parent, name)
        {
            IsVisible = true;
             Layer = RenderLayer.Layer1;
        }

        private RenderLayer _Layer;

        public bool IsVisible { get; set; }
        public RenderLayer Layer
        {
            get => _Layer;
            set
            {
                if (value != _Layer)
                {
                    if (_Layer > 0)
                        Engine.Instance.World.RemoveComponentFromLayer(this, _Layer);
                    Engine.Instance.World.AddComponentToLayer(this, value);
                    _Layer = value;
                }
            }
        }

        public virtual void Render()
        {
            if (!IsVisible)
                return;

        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);
        }

        public override void Destory()
        {
            base.Destory();
            Engine.Instance.World.RemoveComponentFromLayer(this, _Layer);
        }

    }
}
