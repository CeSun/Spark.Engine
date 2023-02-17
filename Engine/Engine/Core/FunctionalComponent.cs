using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class FunctionalComponent
{
    public void BeginPlay()
    {
        OnBeginPlay();
    }

    protected virtual void OnBeginPlay()
    {

    }


    public void Destory()
    {
        OnDestory();
    }
    public virtual void OnDestory()
    {

    }
}
