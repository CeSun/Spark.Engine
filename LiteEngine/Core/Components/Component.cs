using LiteEngine.Core.Actors;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components;
public class Component
{

    protected List<Component> SubComponents = new List<Component>();

    public Actor? Owner { get; protected set; }

    public Component(Component parent, string name)
    {
        Parent = parent;
        if(Parent != null)
        {
            Owner = Parent.Owner;
            Parent.SubComponents.Add(this);
        }
        Name = name;
    }


    public string Name { get; set; }


    public void RecursionSubComponent(Action<Component> action)
    {
        action(this);
        foreach(var subComponent in SubComponents)
        {
            subComponent.RecursionSubComponent(action);
        }
    }
    public Component? Parent { get; private set; }


    public virtual void Update(float deltaTime)
    {
        var scaleMat4 = Matrix4x4.CreateScale(RelativeScale);
        var rotationMat4 = Matrix4x4.CreateFromQuaternion(RelativeRotation);
        var translateMat4 = Matrix4x4.CreateTranslation(RelativeLocation);
        RelativeTransform = scaleMat4 * rotationMat4 * translateMat4;
        if (Parent != null)
        {
            WorldTransform = RelativeTransform * Parent.WorldTransform;
        }
        SubComponents.ForEach((x) => x.Update(deltaTime));
    }

    public Engine EngineInstance { get => Engine.Instance; }


    public Vector3 WorldLocation { get; set; }
    public Vector3 WorldScale { get; set; }
    public Quaternion WorldRotation { get; set; }

    public Vector3 RelativeLocation { get; set; }

    public Vector3 RelativeScale { get; set; }

    public Quaternion RelativeRotation { get; set; }
    public Matrix4x4 RelativeTransform { get; set; }
    public Matrix4x4 WorldTransform { get; set; }

    public virtual void Destory()
    {
        SubComponents.ForEach((x) => x.Destory());
    }
}
