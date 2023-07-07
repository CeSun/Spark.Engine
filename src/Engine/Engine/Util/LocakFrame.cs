using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Util
{
    public class LocakFrame
    {
        private float FrameTime;

        private Stopwatch ThreadTimer = new Stopwatch();
        public LocakFrame(float FrameTime) 
        { 
            this.FrameTime = FrameTime;
        }

        public double Wait()
        {
            if (ThreadTimer.IsRunning == false)
                ThreadTimer.Start();
            var deltaTime = ThreadTimer.Elapsed.TotalMilliseconds;
            while (deltaTime < FrameTime)
            {
                var idleTime = FrameTime - deltaTime;

                if (idleTime > 5)
                {
                    Thread.Sleep((int)(idleTime - 5));
                }
                else
                {
                    Thread.Sleep(0);
                }
                deltaTime = ThreadTimer.Elapsed.TotalMilliseconds;
            }
            ThreadTimer.Restart();
            return deltaTime;
        }
    }
}
