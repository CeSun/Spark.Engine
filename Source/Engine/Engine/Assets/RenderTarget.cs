using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Util;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Assets;

public class RenderTarget() : AssetBase(true)
{
    private bool _isDefaultRenderTarget;

    public bool IsDefaultRenderTarget
    {
        get => _isDefaultRenderTarget;
        set => ChangeProperty(ref _isDefaultRenderTarget, value);

    }
    private int _width;
    public int Width 
    {
        get => _width;
        set => ChangeProperty(ref _width, value);
    }

    private int _height;
    public int Height
    {
        get => _height;
        set => ChangeProperty(ref _height, value);
    }

    public void Resize(int width, int height)
    {
        Height = height;
        Width = width;
    }

    private IReadOnlyList<FrameBufferConfig> _configs = [];
    public IReadOnlyList<FrameBufferConfig> Configs
    {
        get => _configs;
        set => ChangeProperty(ref _configs, value);
    }
    protected unsafe override int assetPropertiesSize => sizeof(RenderTargetProxyProperties); 
    public override nint CreateProperties()
    {
        var ptr = base.CreateProperties();
        ref var properties = ref UnsafeHelper.AsRef<RenderTargetProxyProperties>(ptr);
        properties.Width = _width;
        properties.Height = _height;
        properties.IsDefaultRenderTarget = IsDefaultRenderTarget;
        properties.Configs = new(Configs);

        return ptr;
    }
    public unsafe override nint GetCreateProxyFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<GCHandle>)&CreateProxy;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static GCHandle CreateProxy() => GCHandle.Alloc(new RenderTargetProxy(), GCHandleType.Normal);
    public unsafe override nint GetPropertiesDestoryFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&DestoryProperties;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void DestoryProperties(IntPtr ptr)
    {
        ref var properties = ref UnsafeHelper.AsRef<RenderTargetProxyProperties>(ptr);
        properties.Configs.Dispose();
        Marshal.FreeHGlobal(ptr);
    }

}



public class RenderTargetProxy : AssetRenderProxy, IDisposable
{
    public uint FrameBufferId { private set; get; }
    public List<uint> AttachmentTextureIds { private set; get; } = [];
    public uint DepthId { private set; get; }
    public bool IsDefaultRenderTarget { get; set; }
    public int Width { set; get; }
    public int Height { set; get; }


    public override void DestoryGpuResource(BaseRenderer renderer)
    {
        base.DestoryGpuResource(renderer);
        var gl = renderer.gl;
        if (IsDefaultRenderTarget == false)
        {
            foreach (var id in AttachmentTextureIds)
            {
                if (id != 0)
                {
                    gl.DeleteTexture(id);
                }
            }
            if (FrameBufferId != 0)
            {
                gl.DeleteFramebuffer(FrameBufferId);
            }
            AttachmentTextureIds.Clear();
        }
    }

    public unsafe override void UpdatePropertiesAndRebuildGPUResource(BaseRenderer renderer, IntPtr propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);
        var gl = renderer.gl;
        ref var properties = ref UnsafeHelper.AsRef<RenderTargetProxyProperties>(propertiesPtr);
        Width = properties.Width;
        Height = properties.Height;
        IsDefaultRenderTarget = properties.IsDefaultRenderTarget;

        FrameBufferId = gl.GenFramebuffer();
        gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);
        for (int i = 0; i < properties.Configs.Count; i++)
        {
            GenFrameBuffer(gl,properties.Configs[i], i);
        }
        var state = gl.CheckFramebufferStatus(GLEnum.Framebuffer);
        if (state != GLEnum.FramebufferComplete)
        {
            Console.WriteLine("fbo 出错！" + state);
        }
        gl.BindFramebuffer(GLEnum.Framebuffer, 0);
    }

    protected virtual unsafe void GenFrameBuffer(GL gl, in FrameBufferConfig config, int index)
    {
        var textureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, textureId);
        gl.TexImage2D(GLEnum.Texture2D, 0, (int)config.InternalFormat, (uint)Width, (uint)Height, 0, (GLEnum)config.Format, (GLEnum)config.PixelType, (void*)0);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)config.MagFilter);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)config.MinFilter);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.FramebufferTexture2D(GLEnum.Framebuffer, config.FramebufferAttachment, GLEnum.Texture2D, textureId, 0);
        AttachmentTextureIds.Add(textureId);
    }

    GL? _tmpGl;
    public RenderTargetProxy Begin(GL gl)
    {
        _tmpGl = gl;
        gl.BindFramebuffer(GLEnum.Framebuffer, FrameBufferId);
        gl.Viewport(new Rectangle(0, 0, Width, Height));
        return this;
    }

    public void Dispose()
    {
        _tmpGl?.BindFramebuffer(GLEnum.Framebuffer, 0);
    }
}


public struct FrameBufferConfig
{
    public TextureMagFilter MagFilter;

    public TextureMinFilter MinFilter;

    public InternalFormat InternalFormat;

    public PixelType PixelType;

    public FramebufferAttachment FramebufferAttachment;

    public PixelFormat Format;
}

public struct RenderTargetProxyProperties
{
    public AssetProperties Base;

    public bool IsDefaultRenderTarget;

    public int Width;

    public int Height;

    public UnmanagedArray<FrameBufferConfig> Configs;
}
