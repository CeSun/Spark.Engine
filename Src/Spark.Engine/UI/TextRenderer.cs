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
        // 行高 = 含 ascender/descender 的参考字形 ("Ag") 的行框高度，与文本内容无关。
        // 布局/垂直居中应使用它而非 Measure 的墨水包围盒高度（后者随文本字符变化，
        // 会导致同字号按钮高度不一致、文字基线不对齐）。
        LineHeight = MeasureLineHeight(font);
    }

    /// <summary>字体行高（逻辑像素），与文本内容无关。</summary>
    public float LineHeight { get; }

    private static float MeasureLineHeight(Font font)
    {
        // 用多行文本测真实排版行高（含 line gap）：
        // SixLabors 多行渲染时每行行距比单行墨水盒高，若用墨水盒当行高，
        // N 行文本实际渲染高度 > N × LineHeight → 布局分配不足 → 文字底部被裁剪。
        // 三行墨水盒总高 = 2×行高 + 单行墨水盒高（首末行各一个墨水盒，中间是行距）
        // → 行高 = (三行高 - 单行高) / 2
        var options = new RichTextOptions(font) { Dpi = Dpi, Origin = new PointF(0f, 0f) };
        var one = TextMeasurer.MeasureBounds("Ag", options).Height;
        var three = TextMeasurer.MeasureBounds("Ag\nAg\nAg", options).Height;
        float lineHeight = (three - one) / 2f;
        return System.Math.Max(1f, lineHeight);
    }

    /// <summary>测量文本的逻辑像素尺寸。返回值与 CreateTexture 生成的纹理尺寸**完全一致**，
    /// 保证布局分配的空间恰好容纳绘制内容，垂直排列时不重叠。</summary>
    public Vector2 Measure(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        var bounds = MeasureBounds(text);
        return new Vector2(
            System.Math.Max(1, (int)System.MathF.Ceiling(bounds.Right - bounds.Left) + 2),
            System.Math.Max(1, (int)System.MathF.Ceiling(bounds.Bottom - bounds.Top) + 2));
    }

    /// <summary>
    /// 把 <paramref name="text"/> 截断到不超过 <paramref name="maxWidth"/>（逻辑像素）：
    /// 逐字符测量直到超宽，返回「前缀 + …」。比「按字符数比例截断」精确（非等宽字体下后者仍可能超宽）。
    /// </summary>
    public string Truncate(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (Measure(text).X <= maxWidth)
            return text;

        const string ellipsis = "…";
        float ellipsisW = Measure(ellipsis).X;
        var sb = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            sb.Append(text[i]);
            if (Measure(sb.ToString()).X + ellipsisW > maxWidth)
            {
                sb.Length--; // 回退当前字符
                break;
            }
        }

        if (sb.Length == 0)
            return ellipsis;

        return sb.ToString() + ellipsis;
    }

    /// <summary>
    /// 测量文本块（支持多行 <c>'\n'</c>）：宽度 = 最宽一行（逐行 <see cref="Measure"/> 取 Max），
    /// 高度 = 行数 × <see cref="LineHeight"/>（固定行高，与文本内容无关）。
    /// <para>
    /// 高度**必须**用固定行高而非 <see cref="Measure"/> 的墨水盒高：
    /// 墨水盒随字符变化（含 descender/ascender 的文本更高），会导致同字号文本布局高度波动
    /// （如状态文字变化 → 下方控件整体位移）。LineHeight 已含 line gap，
    /// 行框足以容纳墨水（渲染纹理的 +2 余量由裁剪/抗锯齿兜底）。
    /// </para>
    /// </summary>
    public Vector2 MeasureBlock(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        int lineCount = 1;
        float maxLineW = 0f;
        int start = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i == text.Length || text[i] == '\n')
            {
                var line = text[start..i];
                maxLineW = System.Math.Max(maxLineW, Measure(line).X);
                if (i < text.Length)
                    lineCount++;
                start = i + 1;
            }
        }

        return new Vector2(maxLineW, LineHeight * lineCount);
    }

    /// <summary>在 <paramref name="position"/>（逻辑像素，行框左上）绘制着色文本。</summary>
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

    /// <summary>取文本紧贴墨水包围盒（相对 Origin=(0,0)，即行框左上角）。</summary>
    private FontRectangle MeasureBounds(string text)
    {
        var options = new RichTextOptions(_font) { Dpi = Dpi, Origin = new PointF(0f, 0f) };
        return TextMeasurer.MeasureBounds(text, options);
    }

    private int CreateTexture(UIManager ui, string text)
    {
        // MeasureBounds 返回相对 Origin=(0,0)（行框左上角）的紧贴墨水盒。
        // Left/Top 通常是小的正值（字形左侧 bearing / ascender 顶部距行框顶部的距离），
        // 也可能是负值（斜体左侧悬突、组合符上附加符号超出）。
        var bounds = MeasureBounds(text);
        float left = bounds.Left;
        float top = bounds.Top;
        float right = bounds.Right;
        float bottom = bounds.Bottom;

        // 全包围盒 + 四向各 1px 抗锯齿余量（与 Measure 完全一致）。
        int width = System.Math.Max(1, (int)System.MathF.Ceiling(right - left) + 2);
        int height = System.Math.Max(1, (int)System.MathF.Ceiling(bottom - top) + 2);

        // 绘制原点平移：让墨水盒 [left,right]×[top,bottom] 落到纹理像素 [1, 1+ceil(right-left)] 区间内，
        // 左/上各留 1px 余量，ascender/overhang 像素不会被纹理边界裁掉。
        var drawOptions = new RichTextOptions(_font)
        {
            Dpi = Dpi,
            Origin = new PointF(1f - left, 1f - top),
        };

        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.DrawText(drawOptions, text, Color.White));

        var rgba = new byte[width * height * 4];
        image.CopyPixelDataTo(rgba);

        // 纹理内墨水起点像素 (1,1) 对应逻辑坐标 (left, top)。
        // 四边形放在 position + (left-1, top-1)，使该像素落到屏幕 position + (left, top)，
        // 与「position 为行框左上角」的语义一致。
        var offset = new Vector2(left - 1f, top - 1f);

        int id = _nextTextureId++;
        _textureSizes[id] = new Vector2(width, height);
        _textureOffsets[id] = offset;
        ui.EnqueueTexture(new UITextureUpload(id, (uint)width, (uint)height, rgba));
        return id;
    }
}
