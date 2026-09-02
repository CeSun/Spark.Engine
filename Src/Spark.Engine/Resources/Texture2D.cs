namespace Spark.Engine.Resources;

/// <summary>2D 纹理资产：RGBA8 像素数据，实现 ISceneResource 走统一上传通道。</summary>
public sealed class Texture2D : SceneResource
{
    private readonly byte[] _pixelData;

    /// <summary>纹理 ID（即全局 ResourceId 的别名）。</summary>
    public int TextureId => ResourceId;

    public uint Width { get; }

    public uint Height { get; }

    /// <summary>RGBA8 像素数据，长度 = Width * Height * 4。</summary>
    public ReadOnlyMemory<byte> PixelData => _pixelData;

    public Texture2D(uint width, uint height, byte[] rgba8)
    {
        if (width == 0 || height == 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (rgba8 == null || rgba8.Length != width * height * 4)
            throw new ArgumentException("rgba8 must be width*height*4 bytes", nameof(rgba8));

        Width = width;
        Height = height;
        _pixelData = rgba8.ToArray();
    }
}
