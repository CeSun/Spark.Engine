using System.Collections.Concurrent;
using System.Numerics;
using SixLabors.Fonts;
using Spark.Engine.Render;

namespace Spark.Engine.UI;

/// <summary>
/// 逻辑线程侧的 UI 管理器：每帧收集屏幕空间绘制基元，由 <see cref="EngineApplication"/> 在
/// FillFrameData 时拷贝进场景快照。持有每窗口 <c>UICanvas</c>（控件树）、默认文本渲染器，
/// 以及「逻辑线程 → 渲染线程」的纹理上传队列。
/// </summary>
public sealed class UIManager
{
    private readonly FrameBuffer<UIPrimitive> _primitives = new();
    private readonly Dictionary<int, UICanvas> _canvases = new();
    private readonly ConcurrentQueue<UITextureUpload> _pendingTextures = new();
    private TextRenderer? _text;

    /// <summary>本帧待绘制的基元（游戏/编辑器代码在 OnUpdate 期间写入）。</summary>
    public FrameBuffer<UIPrimitive> Primitives => _primitives;

    /// <summary>默认文本渲染器（惰性创建，首次取用时加载系统字体）。</summary>
    public TextRenderer Text => _text ??= CreateDefaultTextRenderer();

    /// <summary>取（或创建）指定渲染目标的画布。</summary>
    public UICanvas GetOrCreateCanvas(int targetId)
    {
        if (!_canvases.TryGetValue(targetId, out var canvas))
        {
            canvas = new UICanvas(targetId);
            _canvases[targetId] = canvas;
        }

        return canvas;
    }

    /// <summary>清空本帧基元（FillFrameData 拷贝进快照后调用）。</summary>
    public void Clear() => _primitives.Clear();

    /// <summary>画一个着色矩形（采样整张纹理 × 颜色）。</summary>
    public void DrawRect(int targetId, Vector2 position, Vector2 size, Vector4 color)
    {
        _primitives.Add(new UIPrimitive
        {
            TargetId = targetId,
            Rect = new Vector4(position.X, position.Y, size.X, size.Y),
            UV = new Vector4(0f, 0f, 1f, 1f),
            Color = color,
            TextureId = 0,
        });
    }

    /// <summary>逻辑线程：排队一个 UI 纹理待渲染线程上传。</summary>
    public void EnqueueTexture(UITextureUpload upload) => _pendingTextures.Enqueue(upload);

    /// <summary>渲染线程：取一个待上传的 UI 纹理。</summary>
    public bool TryDequeueTexture(out UITextureUpload upload) => _pendingTextures.TryDequeue(out upload);

    private static TextRenderer CreateDefaultTextRenderer()
    {
        var font = LoadSystemFont(16f);
        return new TextRenderer(font);
    }

    private static Font LoadSystemFont(float size)
    {
        foreach (var name in new[] { "Arial", "Segoe UI", "DejaVu Sans", "Liberation Sans", "Verdana", "Tahoma" })
        {
            if (SystemFonts.TryGet(name, out var family))
                return family.CreateFont(size, FontStyle.Regular);
        }

        foreach (var family in SystemFonts.Families)
            return family.CreateFont(size, FontStyle.Regular);

        throw new InvalidOperationException("No system fonts available for UI text rendering.");
    }
}
