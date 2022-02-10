using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.RenderData
{
    public class RenderQueue
    {
        public RenderQueue()
        {
            _Queue = new Queue<RenderMeta<Vertex>>();
        }
        static public RenderQueue? _Instance;
        static public RenderQueue Instance 
        { 
            get 
            { 
                if (_Instance == null)
                    _Instance = new RenderQueue();
                return _Instance; 
            } 
        }
        public void EnQueue(RenderMeta<Vertex> meta)
        {
            _Queue.Enqueue(meta);
        }

        public Queue<RenderMeta<Vertex>> _Queue;
    }
}
