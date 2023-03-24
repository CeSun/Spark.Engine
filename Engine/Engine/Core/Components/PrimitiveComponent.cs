using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Spark.Engine.Core.Actors;

namespace Spark.Engine.Core.Components;

public class PrimitiveComponent
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="actor"></param>
    public PrimitiveComponent(Actor actor)
    {
        Owner = actor;
        ChildrenComponent = new List<PrimitiveComponent>();
        Owner.RegistComponent(this);
    }

    /// <summary>
    /// 渲染
    /// </summary>
    /// <param name="DeltaTime"></param>
    public virtual void Render(double DeltaTime)
    {

    }
    
    /// <summary>
    /// 组件拥有者
    /// </summary>
    public Actor Owner;

    public PrimitiveComponent? ParentComponent;

    public List<PrimitiveComponent> ChildrenComponent;

    public Vector3 Location;

    public Quaternion Rotation;

    public Vector3 Scale;

    public Vector3 RelativeLocation;

    public Quaternion RelativeRotation;

    public Vector3 RelativeScale;
}
