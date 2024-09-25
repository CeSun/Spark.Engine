using Jitter2;
using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;
using System.Numerics;

namespace Spark.Core.Render;

public class DeferredRenderer : BaseRenderer
{
    DirectionLightShadowMapPass DirectionLightShadowMapPass = new DirectionLightShadowMapPass();
    PointLightShadowMapPass PointLightShadowMapPass = new PointLightShadowMapPass();
    SpotLightShadowMapPass SpotLightShadowMapPass = new SpotLightShadowMapPass();

    PrezPass PrezPass = new PrezPass();
    BasePass BasePass = new BasePass();

    DirectionLightShadingPass DirectionLightShadingPass = new DirectionLightShadingPass();
    PointLightShadingPass PointLightShadingPass = new PointLightShadingPass();
    SpotLightShadingPass SpotLightShadingPass = new SpotLightShadingPass();
    public DeferredRenderer(Engine engine) : base(engine)
    {

    }

    public override void RendererWorld(WorldProxy world)
    {
        foreach(var directionLight in world.DirectionalLightComponentProxies)
        {
            DirectionLightShadowMapPass.Render(this, world, directionLight);
        }
        foreach (var pointLight in world.PointLightComponentProxies)
        {
            PointLightShadowMapPass.Render(this, world, pointLight);
        }
        foreach (var spotLight in world.SpotLightComponentProxies)
        {
            SpotLightShadowMapPass.Render(this, world, spotLight);
        }
        foreach(var camera in world.CameraComponentProxies)
        {
            PrezPass.Render(this, world, camera);
            BasePass.Render(this, world, camera);
            DirectionLightShadingPass.Render(this, world, camera);
            PointLightShadingPass.Render(this, world, camera);
            SpotLightShadingPass.Render(this, world, camera);
        }
    }

    private void CheckGbufffer(CameraComponentProxy camera)
    {

        if (camera.RenderTarget == null)
            return;
        if (camera.RenderTargets.Count == 0)
        {
            camera.RenderTargets.Add(new RenderTargetProxy());
        }
        if (camera.RenderTargets[0].Width != camera.RenderTarget.Width || camera.RenderTargets[0].Height != camera.RenderTarget.Height)
        {
            RenderTargetProxyProperties properties = new RenderTargetProxyProperties()
            {
                IsDefaultRenderTarget = false,
                Width = camera.RenderTarget.Width,
                Height = camera.RenderTarget.Height,
                Configs = new UnmanagedArray<FrameBufferConfig>([
                    new FrameBufferConfig{Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba8, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.ColorAttachment0, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest},
                    new FrameBufferConfig{Format = PixelFormat.Rgba, InternalFormat = InternalFormat.Rgba8, PixelType= PixelType.UnsignedByte, FramebufferAttachment = FramebufferAttachment.ColorAttachment1, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest},
                    new FrameBufferConfig{Format = PixelFormat.DepthComponent, InternalFormat = InternalFormat.DepthComponent32f, PixelType= PixelType.Float, FramebufferAttachment = FramebufferAttachment.DepthAttachment, MagFilter = TextureMagFilter.Nearest, MinFilter = TextureMinFilter.Nearest}
                ])
            };
            unsafe
            {
                camera.RenderTargets[0].UpdatePropertiesAndRebuildGPUResource(this, properties);
            }
            properties.Configs.Dispose();
        }
    }
    public void BatchDrawStaticMeshDepth(Span<StaticMeshComponentProxy> staticMeshComponentProxies, Matrix4x4 View, Matrix4x4 Projection)
    {
        foreach (var staticmesh in staticMeshComponentProxies)
        {
            if (staticmesh.StaticMeshProxy == null)
                continue;
            if (staticmesh.Hidden)
                continue;
            foreach (var mesh in staticmesh.StaticMeshProxy.Elements)
            {
                if (mesh.Material == null)
                    continue;
                if (mesh.Material.ShaderTemplate == null)
                    continue;
                var shader = mesh.Material.ShaderTemplate;
                if (mesh.Material.BlendMode == BlendMode.Opaque)
                    shader.Use(gl, "_DEPTH_ONLY_");
                else
                    shader.Use(gl, "_DEPTH_ONLY_", "_BLENDMODE_MASKED_");
                DrawElementDepth(shader, mesh, staticmesh.Trasnform, View, Projection);
                shader.Dispose();
            }
        }
    }

    public void BatchDrawSkeletalMeshDepth(Span<SkeletalMeshComponentProxy> skeletalMeshComponentProxes, Matrix4x4 View, Matrix4x4 Projection)
    {
        foreach (var skeletalMesh in skeletalMeshComponentProxes)
        {
            if (skeletalMesh.SkeletalMeshProxy == null)
                continue;
            if (skeletalMesh.Hidden)
                continue;
            foreach (var mesh in skeletalMesh.SkeletalMeshProxy.Elements)
            {
                if (mesh.Material == null)
                    continue;
                if (mesh.Material.ShaderTemplate == null)
                    continue;
                var shader = mesh.Material.ShaderTemplate;
                if (mesh.Material.BlendMode == BlendMode.Opaque)
                    shader.Use(gl, "_DEPTH_ONLY_", "_SKELETAL_MESH_");
                else
                    shader.Use(gl, "_DEPTH_ONLY_", "_SKELETAL_MESH_", "_BLENDMODE_MASKED_"); 
                for (int i = 0; i < 100; i++)
                {
                    shader.SetMatrix($"animTransform[{i}]", skeletalMesh.AnimBuffer[i]);
                }
                DrawElementDepth(shader, mesh, skeletalMesh.Trasnform, View, Projection);
                shader.Dispose();
            }
        }
    }

    public void DrawElementDepth(ShaderTemplate shader, ElementProxy element, Matrix4x4 Model, Matrix4x4 View, Matrix4x4 Projection)
    {
        if (element.Material == null)
            return;
        shader.SetMatrix("model", Model);
        shader.SetMatrix("view", View);
        shader.SetMatrix("projection", Projection);
        if (element.Material.BlendMode == BlendMode.Masked)
        {
            int offset = 0;
            if (element.Material.Textures.TryGetValue("BaseColor", out var texture))
            {
                shader.SetTexture("Texture_BaseColor", offset, texture);
            }
            else
            {
            }
        }
        this.Draw(element);
    }

}
