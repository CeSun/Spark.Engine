using Spark.Engine.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine
{
    public class BaseSubSystem
    {
        public virtual bool ReceiveUpdate => false;

        public Engine CurrentEngine { get; set; }
        public BaseSubSystem(Engine engine)
        {
            CurrentEngine = engine;
           
        }
        public virtual void BeginPlay()
        {

        }

        public virtual void Update(double DeltaTime)
        {

        }
        public virtual void EndPlay() 
        { 
        
        }
    }
}
