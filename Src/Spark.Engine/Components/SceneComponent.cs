using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Spark.Engine.Components;

public class SceneComponent : ActorComponent
{
    private List<SceneComponent> _attachChildren = [];

    public Vector3 RelativeLocation { get; set; }

    private Vector3 _relativeScale = Vector3.One;

    public Vector3 RelativeScale { get => _relativeScale; set => _relativeScale = value; }

    private Quaternion _relativeRotation = Quaternion.Identity;

    public Quaternion RelativeRotation { get => _relativeRotation; set => _relativeRotation = value; }

    /// <summary>世界变换矩阵（父级挂载尚未实现，简化为相对即世界）。</summary>
    public Matrix4x4 WorldTransform =>
        Matrix4x4.CreateScale(_relativeScale) *
        Matrix4x4.CreateFromQuaternion(_relativeRotation) *
        Matrix4x4.CreateTranslation(RelativeLocation);

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
