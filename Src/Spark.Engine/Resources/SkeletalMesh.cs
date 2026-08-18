using System.Numerics;
using System.Runtime.CompilerServices;
using Spark.Engine.Math;

namespace Spark.Engine.Resources;

/// <summary>骨骼网格常量（与 WGSL <c>array&lt;mat4x4f, MAX_BONES&gt;</c> 同步）。</summary>
public static class SkeletalMeshConstants
{
    public const int MaxBones = 32;
}

/// <summary>
/// 固定容量骨骼矩阵数组（InlineArray）：既作场景 payload 的传输半区，又作 GPU uniform（group1 骨骼矩阵缓冲）。
/// 元素 = 皮肤矩阵（当前骨骼世界 × 逆绑定矩阵），行主序 mat4x4f。
/// </summary>
[InlineArray(SkeletalMeshConstants.MaxBones)]
public struct BoneMatrixArray
{
    private Matrix4x4 _e0;
}

/// <summary>
/// 骨骼网格顶点：位置 + 颜色 + UV + 法线 + 骨骼索引（4×uint8 打包进 u32）+ 骨骼权重（vec4）。
/// stride = 44（基础）+ 4（索引）+ 16（权重）= 64 字节，与 WGSL VertexInput 6 属性一一对应。
/// </summary>
public readonly struct SkeletalMeshVertex
{
    public readonly Vector3 Position;
    public readonly Vector3 Color;
    public readonly Vector2 Uv;
    public readonly Vector3 Normal;

    /// <summary>4 个骨骼索引，各占 1 字节（[0] 在低 8 位），WGSL 端按位抽取。</summary>
    public readonly uint BoneIndices;

    public readonly Vector4 BoneWeights;

    public SkeletalMeshVertex(Vector3 position, Vector3 color, Vector2 uv, Vector3 normal, uint boneIndices, Vector4 boneWeights)
    {
        Position = position;
        Color = color;
        Uv = uv;
        Normal = normal;
        BoneIndices = boneIndices;
        BoneWeights = boneWeights;
    }
}

/// <summary>
/// 骨骼网格资产：顶点（含蒙皮属性）/索引 + 逆绑定矩阵（rest pose 的骨世界逆）。
/// 皮肤矩阵 = 当前骨骼世界 × 逆绑定矩阵，由 <c>SkeletalMeshComponent</c> 每帧计算进 payload。
/// GPU 表示（顶点/索引缓冲）按 ResourceId 由渲染线程上传（handle 模式）。
/// </summary>
public sealed class SkeletalMesh : SceneResource
{
    /// <summary>网格 ID（即全局 ResourceId 的别名）。</summary>
    public int MeshId => ResourceId;

    public SkeletalMeshVertex[] Vertices { get; }

    public uint[] Indices { get; }

    /// <summary>每骨骼逆绑定矩阵（rest pose 逆），长度即骨骼数。</summary>
    public Matrix4x4[] BindPoseInverse { get; }

    public int BoneCount { get; }

    /// <summary>本地空间包围球（bind pose），供实例代理变换到世界空间做视锥剔除。</summary>
    public BoundingSphere Bounds { get; }

    public SkeletalMesh(SkeletalMeshVertex[] vertices, uint[] indices, Matrix4x4[] bindPoseInverse)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Indices = indices ?? throw new ArgumentNullException(nameof(indices));
        BindPoseInverse = bindPoseInverse ?? throw new ArgumentNullException(nameof(bindPoseInverse));
        BoneCount = bindPoseInverse.Length;

        var positions = new Vector3[Vertices.Length];
        for (int i = 0; i < Vertices.Length; i++)
            positions[i] = Vertices[i].Position;

        Bounds = BoundingSphere.CreateFromPoints(positions);
    }
}
