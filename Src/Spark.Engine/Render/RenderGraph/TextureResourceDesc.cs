using Silk.NET.WebGPU;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 纹理资源描述（transient 资源据此在帧内池里分配 GPU 纹理）。
/// </summary>
public readonly struct TextureResourceDesc : IEquatable<TextureResourceDesc>
{
    public readonly uint Width;
    public readonly uint Height;
    public readonly TextureFormat Format;
    public readonly TextureUsage Usage;

    /// <summary>是否为深度格式。</summary>
    public bool IsDepth => Format is TextureFormat.Depth16Unorm
        or TextureFormat.Depth24Plus
        or TextureFormat.Depth24PlusStencil8;

    public TextureResourceDesc(uint width, uint height, TextureFormat format, TextureUsage usage)
    {
        Width = width;
        Height = height;
        Format = format;
        Usage = usage;
    }

    public bool Equals(TextureResourceDesc other)
        => Width == other.Width && Height == other.Height && Format == other.Format && Usage == other.Usage;

    public override bool Equals(object? obj) => obj is TextureResourceDesc other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Width, Height, Format, Usage);

    public override string ToString()
        => $"Texture({Width}x{Height}, {Format}, {Usage}, Depth={IsDepth})";
}
