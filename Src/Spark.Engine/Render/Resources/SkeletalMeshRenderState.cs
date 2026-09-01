using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Spark.Engine.Render.Resources;

/// <summary>
/// 骨骼网格实例的渲染侧状态（按 ProxyId 生命周期管理）：object uniform（group1 binding0）+ 骨骼矩阵 uniform
/// （group1 binding1）+ 二合一 bind group。骨骼矩阵缓冲每帧由 stage 按 snapshot payload 重写。
/// </summary>
public unsafe sealed class SkeletalMeshRenderState : IPerInstanceState
{
    private readonly WebGPU _api;
    private int _disposed;

    /// <summary>每实例 object uniform（world + normalMatrix），渲染时 QueueWriteBuffer 更新。</summary>
    public Buffer* ObjectBuffer { get; }

    /// <summary>皮肤矩阵 uniform（MaxBones × mat4x4），每帧按 payload.BoneMatrices 重写。</summary>
    public Buffer* BoneBuffer { get; }

    /// <summary>group1：binding0 = object uniform，binding1 = 骨骼矩阵。</summary>
    public BindGroup* ObjectBindGroup { get; }

    public SkeletalMeshRenderState(WebGPU api, Buffer* objectBuffer, Buffer* boneBuffer, BindGroup* objectBindGroup)
    {
        _api = api;
        ObjectBuffer = objectBuffer;
        BoneBuffer = boneBuffer;
        ObjectBindGroup = objectBindGroup;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (ObjectBindGroup != null) _api.BindGroupRelease(ObjectBindGroup);
        if (BoneBuffer != null) _api.BufferRelease(BoneBuffer);
        if (ObjectBuffer != null) _api.BufferRelease(ObjectBuffer);
    }
}
