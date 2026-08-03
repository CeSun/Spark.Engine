using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Spark.Engine.Components;

public class SceneComponent : ActorComponent
{
    private SceneComponent? _attachParent;

    public SceneComponent? AttachParent => _attachParent;

    private List<SceneComponent> _attachChildren = [];

    private Vector3 _relativeLocation;

    public Vector3 RelativeLocation => _relativeLocation;

    private Vector3 _relativeScale;

    public Vector3 RelativeScale => _relativeScale;

    private Quaternion _relativeRotation;

    public Quaternion RelativeRotation => _relativeRotation;

    public T GetComponent<T>() where T : ActorComponent
    {
        foreach (var component in _attachChildren)
        {
            if (component is T tComponent)
                return tComponent;
        }
        return null!;
    }
}
