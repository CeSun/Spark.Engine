using System.Numerics;
using Spark.Engine.Render;
using Spark.Engine.Resources;

namespace Spark.Engine.Components;

/// <summary>
/// 骨骼网格组件：渲染一个 <see cref="SkeletalMesh"/>。每帧由 <see cref="BoneTransforms"/>（当前骨骼世界变换）
/// 计算皮肤矩阵（骨骼世界 × 逆绑定矩阵）进 payload，渲染线程据此做 GPU 蒙皮。
/// 对应的 <see cref="SkeletalMeshSceneProxy"/> 与 <see cref="SkeletalMeshPayload"/> 由 SceneProxy 源生成器产出。
/// </summary>
[SceneProxy(SceneCategory.SkeletalMesh)]
public partial class SkeletalMeshComponent : SceneComponent
{
    [ScenePayload] public SkeletalMesh? Mesh { get; set; }

    [ScenePayload] public Material? Material { get; set; }

    /// <summary>当前骨骼世界变换（长度 = Mesh.BoneCount；缺省按单位阵处理）。仅逻辑线程读写。</summary>
    public Matrix4x4[]? BoneTransforms { get; set; }

    /// <summary>是否投射阴影（进 header 的 Visibility 标记，阴影 pass 据此收集 caster）。</summary>
    public bool CastShadow { get; set; } = true;

    /// <summary>皮肤矩阵（每帧计算，进 payload；payload 只承载值，不携带数组引用）。</summary>
    [ScenePayload]
    public BoneMatrixArray BoneMatrices
    {
        get
        {
            var result = new BoneMatrixArray();
            if (Mesh != null)
            {
                var pose = BoneTransforms;
                int n = System.Math.Min(Mesh.BoneCount, SkeletalMeshConstants.MaxBones);
                for (int i = 0; i < n; i++)
                {
                    var boneWorld = pose != null && i < pose.Length ? pose[i] : Matrix4x4.Identity;
                    result[i] = boneWorld * Mesh.BindPoseInverse.Span[i];
                }
            }
            return result;
        }
    }

    partial void OnProxyMapped(SkeletalMeshSceneProxy proxy)
    {
        proxy.Bounds = Mesh == null ? default : Mesh.Bounds.Transform(WorldTransform);

        var flags = VisibilityFlags.Visible | VisibilityFlags.ReceiveShadow;
        if (CastShadow)
            flags |= VisibilityFlags.CastShadow;
        proxy.Visibility = flags;
    }
}
