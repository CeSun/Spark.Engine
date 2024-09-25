using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using Spark.Core.Components;

namespace Spark.Core.Render;

public class DeferredRenderer : BaseRenderer
{
    Pass DirectionLightShadowMapPass = new DirectionLightShadowMapPass();
    Pass PointLightShadowMapPass = new PointLightShadowMapPass();
    Pass SpotLightShadowMapPass = new SpotLightShadowMapPass();

    Pass PrezPass = new PrezPass();
    Pass BasePass = new BasePass();

    Pass DirectionLightShadingPass = new DirectionLightShadingPass();
    Pass PointLightShadingPass = new PointLightShadingPass();
    Pass SpotLightShadingPass = new SpotLightShadingPass();
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



}
