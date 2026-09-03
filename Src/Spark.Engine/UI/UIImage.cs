using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>把完整 RGBA8 像素数据作为一张 UI 纹理显示。</summary>
public sealed class UIImage : UIElement, IDisposable
{
    private readonly byte[] _rgba8;
    private UIManager? _owner;
    private int _textureId;
    private int _disposed;

    public UIImage(uint width, uint height, ReadOnlySpan<byte> rgba8)
    {
        if (width == 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height == 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        var expectedLength = checked((ulong)width * height * 4);
        if (expectedLength > int.MaxValue || rgba8.Length != (int)expectedLength)
            throw new ArgumentException("RGBA8 data length must equal width * height * 4.", nameof(rgba8));

        Width = width;
        Height = height;
        _rgba8 = rgba8.ToArray();
        ClipToBounds = true;
    }

    public uint Width { get; }
    public uint Height { get; }
    public bool MaintainAspectRatio { get; set; } = true;

    protected override void OnPaint(UIManager ui, int targetId)
    {
        if (Volatile.Read(ref _disposed) != 0 || Bounds.Width <= 0f || Bounds.Height <= 0f)
            return;

        EnsureUploaded(ui);
        var rect = CalculateDisplayRect();
        ui.DrawTexture(targetId, _textureId, new Vector2(rect.X, rect.Y),
            new Vector2(rect.Width, rect.Height), Vector4.One);
    }

    private void EnsureUploaded(UIManager ui)
    {
        if (ReferenceEquals(_owner, ui) && _textureId > 0)
            return;

        if (_owner != null && _textureId > 0)
            _owner.EnqueueTextureRelease(_textureId);
        _owner = ui;
        _textureId = ui.AllocateTextureId();
        ui.EnqueueTexture(new UITextureUpload(_textureId, Width, Height, _rgba8));
    }

    private UIRect CalculateDisplayRect()
    {
        if (!MaintainAspectRatio)
            return Bounds;

        var scale = MathF.Min(Bounds.Width / Width, Bounds.Height / Height);
        var width = Width * scale;
        var height = Height * scale;
        return new UIRect(
            Bounds.X + (Bounds.Width - width) * 0.5f,
            Bounds.Y + (Bounds.Height - height) * 0.5f,
            width,
            height);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        if (_owner != null && _textureId > 0)
            _owner.EnqueueTextureRelease(_textureId);
        _owner = null;
        _textureId = 0;
    }
}
