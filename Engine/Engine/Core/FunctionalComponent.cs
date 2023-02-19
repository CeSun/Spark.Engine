using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class FunctionalComponent
{
    private Actor? _Owner;
    public Actor? Owner 
    {
        get => Owner;
        set
        {
            if (_Owner != null)
            {
                _Owner.RemoveComponent(this);
            }
            _Owner = value;
            if (_Owner != null)
            {
                _Owner.AddComponent(this);
            }
        }
    }
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
