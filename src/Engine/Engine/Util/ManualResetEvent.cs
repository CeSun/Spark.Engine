using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Util
{
    public class ManualResetEvent : IDisposable
    {
        bool IsBlocking;
        public ManualResetEvent(bool Reset)
        {
            IsBlocking = !Reset;
        }

        public void WaitOne()
        {
            while (IsBlocking)
            {
                Thread.Sleep(0);
            }
        }

        public void Set()
        {
            IsBlocking = false;
        }

        public void Reset()
        {
            IsBlocking = true;
        }

        public void Dispose()
        {
        }
    }
}
