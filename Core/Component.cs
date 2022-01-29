using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class Component
    {
        public GameObject? Owner { 
            get => _Owner; 
            set
            {
                if (value == _Owner)
                    return;
                if (_Owner != null)
                {
                    _Owner.RemoveComponent(this);
                    _Owner = null;
                }
                _Owner = value;
                if (_Owner != null)
                {
                    _Owner.AddComponent(this);
                }
            }
        }
        private GameObject? _Owner;
        public virtual void Tick()
        {

        }
    }



    public class RenderComponent: Component
    {
        public virtual void Draw(double deltaTime)
        {

        }
    }


}
