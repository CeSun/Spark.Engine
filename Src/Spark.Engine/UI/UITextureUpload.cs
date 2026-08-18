namespace Spark.Engine.UI;

/// <summary>待上传到渲染线程的 UI 纹理（RGBA8 像素 + 尺寸）。</summary>
public readonly struct UITextureUpload
{
    public readonly int Id;
    public readonly uint Width;
    public readonly uint Height;
    public readonly byte[] Rgba;

    public UITextureUpload(int id, uint width, uint height, byte[] rgba)
    {
        Id = id;
        Width = width;
        Height = height;
        Rgba = rgba;
    }
}
