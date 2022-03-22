using System.Numerics;


namespace LiteEngine.Core.Actors;

public partial class Actor
{
    /// <summary>
    /// 相对世界的位置
    /// </summary>
    public Vector3 WorldLocation { get; set; }

    /// <summary>
    /// 相对世界的方向
    /// </summary>
    public Quaternion WorldRotation { get; set; }

    /// <summary>
    /// 相对世界的缩放
    /// </summary>
    public Vector3 WorldScale { get; set; }

    /// <summary>
    /// 相对父节点的位置
    /// </summary>
    public Quaternion RelativeLocation { get; set; }

    /// <summary>
    /// 相对父节点的旋转
    /// </summary>
    public Vector3 RelativeRotation { get; set; }

    /// <summary>
    /// 相对父节点的缩放
    /// </summary>
    public Vector3 RelativeScale { get; set; }
}
