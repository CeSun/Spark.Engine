using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Assets;

public class TextureCube(bool allowMuiltUpLoad = false) : AssetBase(allowMuiltUpLoad)
{
    public List<float>[] _hdrPixels = [[], [], [], [], [], []];

    public List<byte>[] _ldrPixels = [[], [], [], [], [], []];

    private uint _width;
    public uint Width
    {
        get => _width;
        set => ChangeProperty(ref _width, value);
    }

    private uint _height;
    public uint Height
    {
        get => _height;
        set => ChangeProperty(ref _height, value);
    }

    private TexChannel _channel;
    public TexChannel Channel
    {
        get => _channel;
        set => ChangeProperty(ref _channel, value);
    }


    private TexFilter _filter = TexFilter.Liner;
    public TexFilter Filter
    {
        get => _filter;
        set => ChangeProperty(ref _filter, value);
    }

    private bool _isHdrTexture = false;
    public bool IsHdrTexture
    {
        get => _isHdrTexture;
        set => ChangeProperty(ref _isHdrTexture, value);
    }
    public bool IsLdrTexture
    {
        get => !IsHdrTexture;
        set => IsHdrTexture = !value;
    }

    protected unsafe override int assetPropertiesSize => sizeof(TextureCubeProxyProperties);
    public override nint CreateProperties()
    {
        var ptr = base.CreateProperties();
        ref var properties = ref UnsafeHelper.AsRef<TextureCubeProxyProperties>(ptr);
        properties.Width = _width;
        properties.Height = _height;
        properties.Channel = _channel;
        properties.Filter = _filter;
        properties.IsHdrTexture = _isHdrTexture;
        for( var i = 0; i < 6; i++)
        {
            if (IsHdrTexture)
            {
                properties.HDRPixels = new UnmanagedArray<UnmanagedArray<float>>([
                    new UnmanagedArray<float>(CollectionsMarshal.AsSpan(_hdrPixels[0])),
                    new UnmanagedArray<float>(CollectionsMarshal.AsSpan(_hdrPixels[1])),
                    new UnmanagedArray<float>(CollectionsMarshal.AsSpan(_hdrPixels[2])),
                    new UnmanagedArray<float>(CollectionsMarshal.AsSpan(_hdrPixels[3])),
                    new UnmanagedArray<float>(CollectionsMarshal.AsSpan(_hdrPixels[4])),
                    new UnmanagedArray<float>(CollectionsMarshal.AsSpan(_hdrPixels[5])),
                ]);
            }
            else
            {
                properties.LDRPixels = new UnmanagedArray<UnmanagedArray<byte>>([
                    new UnmanagedArray<byte>(CollectionsMarshal.AsSpan(_ldrPixels[0])),
                    new UnmanagedArray<byte>(CollectionsMarshal.AsSpan(_ldrPixels[1])),
                    new UnmanagedArray<byte>(CollectionsMarshal.AsSpan(_ldrPixels[2])),
                    new UnmanagedArray<byte>(CollectionsMarshal.AsSpan(_ldrPixels[3])),
                    new UnmanagedArray<byte>(CollectionsMarshal.AsSpan(_ldrPixels[4])),
                    new UnmanagedArray<byte>(CollectionsMarshal.AsSpan(_ldrPixels[5])),
                ]);
            }
        }

        return ptr;
    }

    public unsafe override nint GetCreateProxyFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<GCHandle>)&CreateProxy;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static GCHandle CreateProxy() => GCHandle.Alloc(new TextureCube(), GCHandleType.Normal);
    public unsafe override nint GetPropertiesDestoryFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&DestoryProperties;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void DestoryProperties(IntPtr ptr)
    {
        ref var properties = ref UnsafeHelper.AsRef<TextureCubeProxyProperties>(ptr);
        for (int i = 0; i < properties.HDRPixels.Length; i++)
        {
            properties.HDRPixels[i].Dispose();
            properties.LDRPixels[i] = default;
        }
        properties.HDRPixels.Dispose();
        for (int i = 0; i < properties.LDRPixels.Length; i++)
        {
            properties.HDRPixels[i] = default;
            properties.LDRPixels[i].Dispose();
        }
        properties.LDRPixels.Dispose();
    }

    protected override void ReleaseAssetMemory()
    {
        base.ReleaseAssetMemory();
        _hdrPixels = [[], [], [], [], [], []];
        _ldrPixels = [[], [], [], [], [], []];
    }
}


public class TextureCubeProxy : AssetRenderProxy
{

    public uint TextureId;

    private static readonly GLEnum[] TexTargets =
    [
        GLEnum.TextureCubeMapPositiveX,
        GLEnum.TextureCubeMapNegativeX,

        GLEnum.TextureCubeMapPositiveY,
        GLEnum.TextureCubeMapNegativeY,

        GLEnum.TextureCubeMapPositiveZ,
        GLEnum.TextureCubeMapNegativeZ
    ];

    private static readonly string[] Attributes =
    [
        "Right",
        "Left",
        "Up",
        "Down",
        "Front",
        "Back"
    ];
    public uint Width { get; set; }
    public uint Height { get; set; }
    public TexChannel Channel { get; set; }
    public TexFilter Filter { get; set; }
    public bool IsHdrTexture { get; set; }

    
    public unsafe override void UpdatePropertiesAndRebuildGPUResource(BaseRenderer renderer, IntPtr propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);
        var gl = renderer.gl;
        ref var properties = ref UnsafeHelper.AsRef<TextureCubeProxyProperties>(propertiesPtr);
        Width = properties.Width;
        Height = properties.Height;
        Channel = properties.Channel;
        Filter = properties.Filter;
        IsHdrTexture = properties.IsHdrTexture;


        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.TextureCubeMap, TextureId);

        for (int i = 0; i < 6; i++)
        {
            if (IsHdrTexture == true)
            {
                gl.TexImage2D(TexTargets[i], 0, (int)Channel.ToGlHdrEnum(), Width, Height, 0, Channel.ToGlEnum(), GLEnum.Float, properties.HDRPixels[i].Ptr);
            }
            else 
            {
                gl.TexImage2D(TexTargets[i], 0, (int)Channel.ToGlEnum(), Width, Height, 0, Channel.ToGlEnum(), GLEnum.UnsignedByte, properties.LDRPixels[i].Ptr);
            }
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapR, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(GLEnum.TextureCubeMap, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        }
    }

    public override void DestoryGpuResource(BaseRenderer renderer)
    {
        base.DestoryGpuResource(renderer);
        var gl = renderer.gl;
        if (TextureId != 0)
        {
            gl.DeleteTexture(TextureId);
        }
    }
}

public struct TextureCubeProxyProperties
{
    public AssetProperties Base;
    public uint Width;
    public uint Height;
    public TexChannel Channel;
    public TexFilter Filter;
    public bool IsHdrTexture;
    public UnmanagedArray<UnmanagedArray<float>> HDRPixels;
    public UnmanagedArray<UnmanagedArray<byte>> LDRPixels;
}