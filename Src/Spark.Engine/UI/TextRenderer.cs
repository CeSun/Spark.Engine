using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Spark.Engine.UI;

/// <summary>
/// 字符串级文本渲染器（P3 v1）：把整段文本经 SixLabors 栅格化为白字透明底的 RGBA8 纹理，
/// 以带纹理四边形绘制（着色经 <see cref="UIPrimitive.Color"/> tint）。字形图集与按字形复用留待后续优化。
/// 运行于逻辑线程；新纹理经 <see cref="UIManager.EnqueueTexture"/> 排队，由渲染线程上传。
/// </summary>
public sealed class TextRenderer
{
    private const float Dpi = 72f; // 1pt = 1px，与测量/渲染保持一致

    private readonly Font _font;
    private readonly Dictionary<string, int> _textureIds = new();
    private readonly Dictionary<int, Vector2> _textureSizes = new();
    // 每个 UI 文本纹理的「墨水原点偏移」：DrawText 绘制四边形时把纹理左上角放到 position + offset，
    // 使纹理内的墨水像素落到 position 处（与文本逻辑原点一致）。详见 CreateTexture。
    private readonly Dictionary<int, Vector2> _textureOffsets = new();
    private int _nextTextureId = 1;

    public TextRenderer(Font font)
    {
        _font = font;
    }

    /// <summary>测量文本的逻辑像素尺寸。</summary>
    public Vector2 Measure(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        var size = TextMeasurer.MeasureSize(text, new TextOptions(_font) { Dpi = Dpi });
        return new Vector2(size.Width, size.Height);
    }

    /// <summary>在 <paramref name="position"/>（逻辑像素，左上）绘制着色文本。</summary>
    public void DrawText(UIManager ui, int targetId, string text, Vector2 position, Vector4 color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (!_textureIds.TryGetValue(text, out int textureId))
        {
            textureId = CreateTexture(ui, text);
            _textureIds[text] = textureId;
        }

        var size = _textureSizes[textureId];
        var offset = _textureOffsets[textureId];
        var clip = ui.CurrentClip(targetId);
        ui.Primitives.Add(new UIPrimitive
        {
            TargetId = targetId,
            Rect = new Vector4(position.X + offset.X, position.Y + offset.Y, size.X, size.Y),
            UV = new Vector4(0f, 0f, 1f, 1f),
            Color = color,
            TextureId = textureId,
            ScissorRect = clip.HasValue ? new Vector4(clip.Value.X, clip.Value.Y, clip.Value.Width, clip.Value.Height) : default,
        });
    }

    private int CreateTexture(UIManager ui, string text)
    {
        // 用与 Measure 一致的文本选项取墨水包围盒。MeasureBounds 返回相对 Origin=(0,0) 的紧贴墨水盒，
        // 其 Left/Top 可能为负（斜体左侧悬突、Å/É 等 ascender 超出线高、组合符上附加符号）。
        var measureOptions = new RichTextOptions(_font) { Dpi = Dpi, Origin = new PointF(0f, 0f) };
        var bounds = TextMeasurer.MeasureBounds(text, measureOptions);

        // 全包围盒 + 四向各 1px 抗锯齿余量；旧版只 ceil(Right/Bottom) 会把负的 Left/Top 裁掉。
        float left = bounds.Left;
        float top = bounds.Top;
        float right = bounds.Right;
        float bottom = bounds.Bottom;
        int width = System.Math.Max(1, (int)System.MathF.Ceiling(right - left) + 2);
        int height = System.Math.Max(1, (int)System.MathF.Ceiling(bottom - top) + 2);

        // 绘制原点平移：让墨水盒 [left,right]×[top,bottom] 落到纹理像素 [1, 1+ceil(right-left)] 区间内，
        // 左/上各留 1px 余量。这样 ascender/overhang 像素不会画到纹理边界外被裁。
        var drawOptions = new RichTextOptions(_font)
        {
            Dpi = Dpi,
            Origin = new PointF(1f - left, 1f - top),
        };

        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.DrawText(drawOptions, text, Color.White));

        var rgba = new byte[width * height * 4];
        image.CopyPixelDataTo(rgba);

        // DrawText 绘制四边形时，纹理左上角放在 position + offset，
        // 使纹理内像素 (1,1) 即墨水 (left, top) 落到屏幕 position + (left, top) —— 与文本逻辑原点一致。
        var offset = new Vector2(left - 1f, top - 1f);

        int id = _nextTextureId++;
        _textureSizes[id] = new Vector2(width, height);
        _textureOffsets[id] = offset;
        ui.EnqueueTexture(new UITextureUpload(id, (uint)width, (uint)height, rgba));
        return id;
    }
}
