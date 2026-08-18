using System.Numerics;
using Silk.NET.WebGPU;
using Spark.Engine.Render.Resources;

namespace Spark.Engine.Render.Pipeline.BlinnPhong.Stages;

/// <summary>
/// 「画静态网格」类 stage 的公共基类：持有 <see cref="BlinnPhongStageContext"/>（共享基建），
/// 提供共用的静态网格绘制（draw）与材质解析。子类只保留各自的附件配置、帧 uniform 内容与剔除。
/// </summary>
internal abstract unsafe class StaticMeshStage : IRenderStage
{
    protected readonly BlinnPhongStageContext Ctx;

    protected StaticMeshStage(BlinnPhongStageContext ctx)
    {
        Ctx = ctx;
    }

    public abstract void Initialize();

    public abstract void Dispose();

    /// <summary>解析物体材质（MaterialId → MaterialGPUResource，缺失回退默认材质）。</summary>
    protected MaterialGPUResource ResolveMaterial(in SceneObjectHeader obj, SceneSnapshot snapshot)
    {
        var payload = snapshot.StaticMeshes[obj.PayloadIndex];
        if (payload.MaterialId != 0 &&
            Ctx.GpuResources.TryGetValue(payload.MaterialId, out var mg) && mg is MaterialGPUResource m)
            return m;

        return Ctx.DefaultMaterialGpu;
    }

    /// <summary>共用的静态网格 draw：set pipeline / 四组 bind group / vertex / index / drawIndexed。</summary>
    protected void DrawStaticMesh(RenderPassEncoder* pass, in SceneObjectHeader obj, SceneSnapshot snapshot, ShaderPass shaderPass, TextureFormat format)
    {
        var payload = snapshot.StaticMeshes[obj.PayloadIndex];
        if (!Ctx.GpuResources.TryGetValue(payload.MeshId, out var gpu) || gpu is not MeshGPUResource mesh)
            return;
        if (!Ctx.ProxyStates.TryGetValue(obj.ProxyId, out var state) || state is not StaticMeshRenderState meshState)
            return;

        var material = ResolveMaterial(obj, snapshot);

        Matrix4x4.Invert(obj.WorldTransform, out var invWorld);
        ObjectUniformData objectData = new()
        {
            World = obj.WorldTransform,
            NormalMatrix = Matrix4x4.Transpose(invWorld),
        };
        ObjectUniformData* objectPtr = &objectData;
        Ctx.WebGpu.Api.QueueWriteBuffer(Ctx.WebGpu.Queue, meshState.ObjectBuffer, 0, objectPtr, (nuint)sizeof(ObjectUniformData));

        var pipeline = Ctx.ShaderCache.GetPipeline(material.ShaderKey, shaderPass, format);

        var api = Ctx.WebGpu.Api;
        api.RenderPassEncoderSetPipeline(pass, pipeline);
        api.RenderPassEncoderSetBindGroup(pass, 1, meshState.ObjectBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetBindGroup(pass, 2, material.ParamsBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetBindGroup(pass, 3, material.TexturesBindGroup, (nuint)0, null);
        api.RenderPassEncoderSetVertexBuffer(pass, 0, mesh.VertexBuffer, 0, mesh.VertexBufferSize);
        api.RenderPassEncoderSetIndexBuffer(pass, mesh.IndexBuffer, mesh.IndexFormat, 0, mesh.IndexBufferSize);
        api.RenderPassEncoderDrawIndexed(pass, mesh.IndexCount, 1, 0, 0, 0);
    }
}
