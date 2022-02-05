using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class AnimationController : Component
    {
        public Skeleton AnimationSkeleton { get; private set; }
        public AnimationController(Skeleton animationSkeleton)
        {
            AnimationSkeleton = animationSkeleton;
        }
        Animation? Animation { get; set; }

        
        public override void Tick()
        {
            base.Tick();
            if (Animation == null)
                return;
            
        }
    }
}
