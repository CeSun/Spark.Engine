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
                    var oldOwener = _Owner;
                    _Owner = null;
                    OnRemoved(oldOwener);
                }
                _Owner = value;
                if (_Owner != null)
                {
                    _Owner.AddComponent(this);
                    OnAttached(_Owner);
                }
            }
        }
        private GameObject? _Owner;
        public virtual void Tick()
        {

        }
        public virtual void OnRemoved(GameObject? OldOwner)
        {

        }
        public virtual void OnAttached(GameObject? Owner)
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
