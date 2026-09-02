using System.Collections.Concurrent;
using System.Numerics;
using SixLabors.Fonts;
using Spark.Engine.Render;
using Spark.Engine.Render.Common;

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
    private readonly ConcurrentQueue<int> _pendingTextureReleases = new();
    // 按 targetId 隔离的裁剪栈：多窗口/多 overlay pass 时不会互相污染 push/pop 状态。
    private readonly Dictionary<int, Stack<UIRect>> _clipStacks = new();
    private TextRenderer? _text;

    /// <summary>当前等待渲染线程上传的 UI 纹理数量（诊断用途）。</summary>
    public int PendingTextureUploadCount => _pendingTextures.Count;

    /// <summary>当前等待渲染线程释放的 UI 纹理数量（诊断用途）。</summary>
    public int PendingTextureReleaseCount => _pendingTextureReleases.Count;

    /// <summary>本帧待绘制的基元（游戏/编辑器代码在 OnUpdate 期间写入）。</summary>
    public FrameBuffer<UIPrimitive> Primitives => _primitives;

    /// <summary>默认文本渲染器（惰性创建，首次取用时加载系统字体）。</summary>
    public TextRenderer Text => _text ??= CreateDefaultTextRenderer();

    /// <summary>开始一帧 UI 收集，供文本缓存记录最近使用时间。</summary>
    internal void BeginFrame() => _text?.BeginFrame();

    /// <summary>结束一帧 UI 收集，淘汰长期未使用的文本纹理。</summary>
    internal void EndFrame() => _text?.EndFrame(this);

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

    /// <summary>清空本帧基元与所有 target 的裁剪栈（FillFrameData 拷贝进快照后调用）。</summary>
    public void Clear()
    {
        _primitives.Clear();
        foreach (var stack in _clipStacks.Values)
            stack.Clear();
    }

    // ———————————— 裁剪栈（P6 scissor 支持，按 targetId 隔离）————————————

    private Stack<UIRect> GetStack(int targetId)
    {
        if (!_clipStacks.TryGetValue(targetId, out var stack))
        {
            stack = new Stack<UIRect>();
            _clipStacks[targetId] = stack;
        }
        return stack;
    }

    /// <summary>压入一个裁剪矩形（与当前栈顶取交集），作用于指定 targetId。</summary>
    public void PushClip(int targetId, UIRect rect)
    {
        var stack = GetStack(targetId);
        if (stack.Count > 0)
        {
            var current = stack.Peek();
            rect = Intersect(current, rect);
        }

        stack.Push(rect);
    }

    /// <summary>弹出指定 targetId 最近的裁剪矩形。</summary>
    public void PopClip(int targetId)
    {
        if (_clipStacks.TryGetValue(targetId, out var stack) && stack.Count > 0)
            stack.Pop();
    }

    /// <summary>暂时挂起指定目标的裁剪栈，供需要越出祖先边界的弹出层绘制。</summary>
    internal IDisposable SuspendClip(int targetId)
    {
        if (!_clipStacks.TryGetValue(targetId, out var stack) || stack.Count == 0)
            return NoopClipSuspension.Instance;

        var saved = stack.ToArray();
        stack.Clear();
        return new ClipSuspension(stack, saved);
    }

    private sealed class ClipSuspension(Stack<UIRect> stack, UIRect[] saved) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            for (int i = saved.Length - 1; i >= 0; i--)
                stack.Push(saved[i]);
        }
    }

    private sealed class NoopClipSuspension : IDisposable
    {
        public static NoopClipSuspension Instance { get; } = new();
        public void Dispose() { }
    }

    /// <summary>
    /// 指定 targetId 当前有效裁剪区（栈为空时表示无裁剪）。
    /// 裁剪区可能为「空交集」（宽/高为负）：调用方应视为「完全裁剪，跳过绘制」，
    /// 与「无裁剪」（null）区分——否则完全越出视口的内容会被当成无裁剪画出来。
    /// </summary>
    public UIRect? CurrentClip(int targetId)
        => _clipStacks.TryGetValue(targetId, out var stack) && stack.Count > 0 ? stack.Peek() : null;

    private static UIRect Intersect(UIRect a, UIRect b)
    {
        float x = System.Math.Max(a.X, b.X);
        float y = System.Math.Max(a.Y, b.Y);
        float right = System.Math.Min(a.Right, b.Right);
        float bottom = System.Math.Min(a.Bottom, b.Bottom);
        float w = right - x;
        float h = bottom - y;
        if (w <= 0f || h <= 0f)
            return new UIRect(x, y, -1f, -1f); // 空交集：标记为「完全裁剪」（负尺寸）
        return new UIRect(x, y, w, h);
    }

    /// <summary>画一个着色矩形（采样整张纹理 × 颜色），自动注入当前 targetId 的裁剪栈信息。</summary>
    public void DrawRect(int targetId, Vector2 position, Vector2 size, Vector4 color)
    {
        var clip = CurrentClip(targetId);
        _primitives.Add(new UIPrimitive
        {
            TargetId = targetId,
            Rect = new Vector4(position.X, position.Y, size.X, size.Y),
            UV = new Vector4(0f, 0f, 1f, 1f),
            Color = color,
            TextureId = 0,
            ScissorRect = clip.HasValue ? new Vector4(clip.Value.X, clip.Value.Y, clip.Value.Width, clip.Value.Height) : default,
        });
    }

    /// <summary>绘制屏幕空间线段；渲染线程会将其展开为带厚度的四边形。</summary>
    public void DrawLine(int targetId, Vector2 start, Vector2 end, float thickness, Vector4 color)
    {
        var clip = CurrentClip(targetId);
        _primitives.Add(new UIPrimitive
        {
            TargetId = targetId,
            Color = color,
            TextureId = 0,
            IsLine = true,
            LineStart = start,
            LineEnd = end,
            LineThickness = MathF.Max(1f, thickness),
            ScissorRect = clip.HasValue ? new Vector4(clip.Value.X, clip.Value.Y, clip.Value.Width, clip.Value.Height) : default,
        });
    }

    /// <summary>逻辑线程：排队一个 UI 纹理待渲染线程上传。</summary>
    public void EnqueueTexture(UITextureUpload upload) => _pendingTextures.Enqueue(upload);

    /// <summary>逻辑线程：排队释放已从文本缓存淘汰的纹理。</summary>
    internal void EnqueueTextureRelease(int textureId) => _pendingTextureReleases.Enqueue(textureId);

    /// <summary>渲染线程：取一个待上传的 UI 纹理。</summary>
    public bool TryDequeueTexture(out UITextureUpload upload) => _pendingTextures.TryDequeue(out upload);

    /// <summary>渲染线程：取下一个待释放的 UI 纹理。</summary>
    internal bool TryDequeueTextureRelease(out int textureId) => _pendingTextureReleases.TryDequeue(out textureId);

    // ———————————— 渲染视图支持（UIRenderView 控件）————————————

    private readonly ConcurrentDictionary<int, RenderViewInfo> _renderViews = new();

    /// <summary>渲染视图信息（仅用于逻辑线程布局计算）。</summary>
    private readonly struct RenderViewInfo
    {
        public readonly uint Width;
        public readonly uint Height;

        public RenderViewInfo(uint width, uint height)
        {
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// 注册一个渲染视图供 UIRenderView 控件使用。
    /// 应在创建 TextureRenderTarget 后调用，传入其 Id 和尺寸。
    /// </summary>
    /// <param name="renderViewId">TextureRenderTarget.Id</param>
    /// <param name="width">渲染视图宽度</param>
    /// <param name="height">渲染视图高度</param>
    public void RegisterRenderView(int renderViewId, uint width, uint height)
    {
        _renderViews[renderViewId] = new RenderViewInfo(width, height);
    }

    /// <summary>注销一个渲染视图。</summary>
    public void UnregisterRenderView(int renderViewId)
    {
        _renderViews.TryRemove(renderViewId, out _);
    }

    /// <summary>获取渲染视图尺寸（用于布局计算）。未注册时返回 (0, 0)。</summary>
    public (uint Width, uint Height) GetRenderViewSize(int renderViewId)
    {
        if (_renderViews.TryGetValue(renderViewId, out var info))
            return (info.Width, info.Height);
        return (0, 0);
    }

    /// <summary>绘制一个渲染视图到指定位置（发出特殊 UIPrimitive，TextureId 为负值表示渲染视图 ID）。</summary>
    public void DrawRenderView(int targetId, int renderViewId, Vector2 position, Vector2 size)
    {
        var clip = CurrentClip(targetId);
        _primitives.Add(new UIPrimitive
        {
            TargetId = targetId,
            Rect = new Vector4(position.X, position.Y, size.X, size.Y),
            UV = new Vector4(0f, 0f, 1f, 1f),
            Color = Vector4.One,
            TextureId = -renderViewId, // 负值表示渲染视图 ID
            ScissorRect = clip.HasValue ? new Vector4(clip.Value.X, clip.Value.Y, clip.Value.Width, clip.Value.Height) : default,
        });
    }

    private static TextRenderer CreateDefaultTextRenderer()
    {
        var font = LoadSystemFont(16f);
        var fallbacks = LoadFallbackFonts(font.Family);
        return new TextRenderer(font, fallbacks);
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

    private static IReadOnlyList<FontFamily> LoadFallbackFonts(FontFamily primary)
    {
        var result = new List<FontFamily>();
        foreach (var name in new[]
                 {
                     "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "Noto Sans CJK SC",
                     "Noto Sans SC", "PingFang SC", "WenQuanYi Micro Hei", "DejaVu Sans",
                 })
        {
            if (SystemFonts.TryGet(name, out var family) &&
                !family.Equals(primary) && !result.Contains(family))
                result.Add(family);
        }
        return result;
    }
}
