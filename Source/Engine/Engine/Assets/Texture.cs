using Silk.NET.OpenGLES;
using Spark.Core.Render;
using Spark.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Spark.Core.Assets;

public class Texture(bool allowMuiltUpLoad = false) : AssetBase(allowMuiltUpLoad)
{
    private List<float> _hdrPixels = [];
    public List<float> HDRPixels
    {
        get => _hdrPixels;
        set => ChangeProperty(ref _hdrPixels, value);
    }

    private List<byte> _ldrPixels = [];
    public List<byte> LDRPixels
    {
        get => _ldrPixels;
        set => ChangeProperty(ref _ldrPixels, value);

    }

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
    protected unsafe override int assetPropertiesSize => sizeof(TextureProxyProperties);

    public override nint CreateProperties()
    {
        var ptr = base.CreateProperties();
        ref var properties = ref UnsafeHelper.AsRef<TextureProxyProperties>(ptr);
        properties.Width = _width;
        properties.Height = _height;
        properties.Channel = _channel;
        properties.Filter = _filter;
        properties.IsHdrTexture = _isHdrTexture;
        if (IsHdrTexture)
        {
            properties.HDRPixels = default;
            properties.HDRPixels = new UnmanagedArray<float>(CollectionsMarshal.AsSpan(HDRPixels));
        }
        else
        {
            properties.HDRPixels = default;
            properties.LDRPixels = new UnmanagedArray<byte>(CollectionsMarshal.AsSpan(LDRPixels));
        }

        return ptr;
    }

    public unsafe override nint GetCreateProxyFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<GCHandle>)&CreateProxy;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static GCHandle CreateProxy() => GCHandle.Alloc(new TextureProxy(), GCHandleType.Normal);
    public unsafe override nint GetPropertiesDestoryFunctionPointer() => (IntPtr)(delegate* unmanaged[Cdecl]<IntPtr, void>)&DestoryProperties;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    static void DestoryProperties(IntPtr ptr)
    {
        ref var properties = ref UnsafeHelper.AsRef<TextureProxyProperties>(ptr);
        properties.HDRPixels.Dispose();
        properties.LDRPixels.Dispose();
        Marshal.FreeHGlobal(ptr);
    }
    protected override void ReleaseAssetMemory()
    {
        base.ReleaseAssetMemory();
        _hdrPixels = [];
        _ldrPixels = [];
    }

}


public class TextureProxy : AssetRenderProxy
{
    public uint TextureId { get; protected set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public TexChannel Channel { get; set; }
    public TexFilter Filter { get; set; } = TexFilter.Liner;
    public bool IsHdrTexture { get; set; }
    public unsafe override void UpdatePropertiesAndRebuildGPUResource(BaseRenderer renderer, IntPtr propertiesPtr)
    {
        base.UpdatePropertiesAndRebuildGPUResource(renderer, propertiesPtr);
        var gl = renderer.gl;
        ref var properties = ref UnsafeHelper.AsRef<TextureProxyProperties>(propertiesPtr);
        Width = properties.Width;
        Height = properties.Height;
        Channel = properties.Channel;
        Filter = properties.Filter;
        IsHdrTexture = properties.IsHdrTexture;

        TextureId = gl.GenTexture();
        gl.BindTexture(GLEnum.Texture2D, TextureId);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)Filter.ToGlFilter());
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)Filter.ToGlFilter());
        if (IsHdrTexture)
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)GLEnum.Rgb16f, Width, Height, 0, Channel.ToGlEnum(), GLEnum.Float, properties.HDRPixels.Ptr);
        }
        else
        {
            gl.TexImage2D(GLEnum.Texture2D, 0, (int)Channel.ToGlEnum(), Width, Height, 0, Channel.ToGlEnum(), GLEnum.UnsignedByte, properties.LDRPixels.Ptr);
        }
        gl.BindTexture(GLEnum.Texture2D, 0);
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

public enum TexChannel
{
    Rgb,
    Rgba,
}

public enum TexFilter
{
    Liner,
    Nearest
}
public static class ChannelHelper
{
    public static GLEnum ToGlEnum(this TexChannel channel)
    {
        return channel switch
        {
            TexChannel.Rgb => GLEnum.Rgb,
            TexChannel.Rgba => GLEnum.Rgba,
            _ => throw new NotImplementedException()
        };
    }
    public static GLEnum ToGlHdrEnum(this TexChannel channel)
    {
        return channel switch
        {
            TexChannel.Rgb => GLEnum.Rgb16f,
            TexChannel.Rgba => GLEnum.Rgba16f,
            _ => throw new NotImplementedException()
        };
    }
    public static GLEnum ToGlFilter(this TexFilter filter)
    {
        return filter switch
        {
            TexFilter.Liner => GLEnum.Linear,
            TexFilter.Nearest => GLEnum.Nearest,
            _ => GLEnum.Linear
        };
    }

}


public struct TextureProxyProperties
{
    public AssetProperties Base;
    public uint Width;
    public uint Height;
    public TexChannel Channel;
    public TexFilter Filter;
    public bool IsHdrTexture;
    public UnmanagedArray<float> HDRPixels;
    public UnmanagedArray<byte> LDRPixels;
}

public unsafe struct UnmanagedArray<T> : IDisposable where T : unmanaged
{
    public T* Ptr { private set; get; }
    public int Count => Length;
    public int Length { private set; get; }
    public UnmanagedArray(ReadOnlySpan<T> data)
    {
        Length = data.Length;
        Ptr = (T*)Marshal.AllocHGlobal(Length * sizeof(T));
        for(int i = 0; i < Length; i++)
        {
            Ptr[i] = data[i];
        }
    }

    public UnmanagedArray(IReadOnlyList<T> data)
    {
        Length = data.Count;
        Ptr = (T*)Marshal.AllocHGlobal(Length * sizeof(T));
        for (int i = 0; i < Length; i++)
        {
            Ptr[i] = data[i];
        }
    }


    public T this[int index]
    {
        get
        {
            if (index >= Length || index < 0)
                throw new IndexOutOfRangeException();
            return Ptr[index];
        }
        set
        {
            if (index >= Length || index < 0)
                throw new IndexOutOfRangeException();
             Ptr[index] = value;
        }
    }

    public void Dispose()
    {
        if (Ptr != null)
        {
            Marshal.FreeHGlobal((nint)Ptr);
        }
        if (Length != 0)
        {
            Length = 0;
        }
    }

    public void Resize(int size)
    {
        Ptr = (T*)Marshal.AllocHGlobal(size * sizeof(T));
        Length = size;
    }
}