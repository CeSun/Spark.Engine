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
        RendererLightShadowMap(world);
        foreach (var camera in world.CameraComponentProxies)
        {
            var gbuffer = CheckGbufffer(camera);
            if (gbuffer == null)
                continue;
            using (gbuffer.Begin(gl))
            {
                PrezPass.Render(this, world, camera);
                BasePass.Render(this, world, camera);
            }
            using (camera.RenderTarget?.Begin(gl))
            {
                DirectionLightShadingPass.Render(this, world, camera);
                PointLightShadingPass.Render(this, world, camera);
                SpotLightShadingPass.Render(this, world, camera);
            }
        }
    }

    private void RendererLightShadowMap(WorldProxy world)
    {
        foreach (var directionLight in world.DirectionalLightComponentProxies)
        {
            if (directionLight.Hidden == true)
                continue;
            if (directionLight.CastShadow == false)
                continue;
            DirectionLightShadowMapPass.Render(this, world, directionLight);
        }
        foreach (var pointLight in world.PointLightComponentProxies)
        {
            if (pointLight.Hidden == true)
                continue;
            if (pointLight.CastShadow == false)
                continue;
            PointLightShadowMapPass.Render(this, world, pointLight);
        }
        foreach (var spotLight in world.SpotLightComponentProxies)
        {
            if (spotLight.Hidden == true)
                continue;
            if (spotLight.CastShadow == false)
                continue;
            SpotLightShadowMapPass.Render(this, world, spotLight);
        }
    }
    private RenderTargetProxy? CheckGbufffer(CameraComponentProxy camera)
    {

        if (camera.RenderTarget == null)
            return null;
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

        return camera.RenderTargets[0];
    }
    public void BatchDrawStaticMesh(Span<StaticMeshComponentProxy> staticMeshComponentProxies, Matrix4x4 View, Matrix4x4 Projection, bool OnlyDepth)
    {
        Span<string> Macros = ["_NOTHING_"];
        Span<string> MaskedMacros = ["_BLENDMODE_MASKED_"];
        if (OnlyDepth)
        {
            MaskedMacros = [.. MaskedMacros, "_DEPTH_ONLY_"];
            Macros = ["_DEPTH_ONLY_"];
        }

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
                    shader.Use(gl, Macros);
                else
                    shader.Use(gl, MaskedMacros);
                if (OnlyDepth)
                    DrawElementDepth(shader, mesh, staticmesh.Trasnform, View, Projection);
                else
                    DrawElement(shader, mesh, staticmesh.Trasnform, View, Projection);
                shader.Dispose();
            }
        }
    }

    public void BatchDrawSkeletalMesh(Span<SkeletalMeshComponentProxy> skeletalMeshComponentProxes, Matrix4x4 View, Matrix4x4 Projection, bool OnlyDepth)
    {
        Span<string> Macros = ["_SKELETAL_MESH_"];
        Span<string> MaskedMacros = ["_BLENDMODE_MASKED_", .. Macros];
        if (OnlyDepth)
        {
            MaskedMacros = [.. MaskedMacros, "_DEPTH_ONLY_"];
            Macros = [.. Macros, "_DEPTH_ONLY_"];
        }

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
                    shader.Use(gl, Macros);
                else
                    shader.Use(gl, MaskedMacros); 
                for (int i = 0; i < 100; i++)
                {
                    shader.SetMatrix($"animTransform[{i}]", skeletalMesh.AnimBuffer[i]);
                }
                if (OnlyDepth)
                    DrawElementDepth(shader, mesh, skeletalMesh.Trasnform, View, Projection);
                else
                    DrawElement(shader, mesh, skeletalMesh.Trasnform, View, Projection);
                shader.Dispose();
            }
        }
    }

    public void DrawElement(ShaderTemplate shader, ElementProxy element, Matrix4x4 Model, Matrix4x4 View, Matrix4x4 Projection)
    {
        if (element.Material == null)
            return;
        shader.SetMatrix("model", Model);
        shader.SetMatrix("view", View);
        shader.SetMatrix("projection", Projection);
        if (element.Material.Textures.TryGetValue("BaseColor", out var textureBaseColor))
            shader.SetTexture("Texture_BaseColor", 1, textureBaseColor);
        if (element.Material.Textures.TryGetValue("Normal", out var textureNormal))
            shader.SetTexture("Texture_Normal", 2, textureNormal);
        if (element.Material.Textures.TryGetValue("Metalness", out var textureMetalness))
            shader.SetTexture("Texture_Metalness", 3, textureMetalness);
        if (element.Material.Textures.TryGetValue("Roughness", out var textureRoughness))
            shader.SetTexture("Texture_Roughness", 4, textureRoughness);
        this.Draw(element);
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
            if (element.Material.Textures.TryGetValue("BaseColor", out var texture))
                shader.SetTexture("Texture_BaseColor", 1, texture);
        }
        this.Draw(element);
    }

}
