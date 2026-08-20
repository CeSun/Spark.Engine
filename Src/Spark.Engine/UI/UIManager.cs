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
    // 按 targetId 隔离的裁剪栈：多窗口/多 overlay pass 时不会互相污染 push/pop 状态。
    private readonly Dictionary<int, Stack<UIRect>> _clipStacks = new();
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

    /// <summary>指定 targetId 当前有效裁剪区（栈为空时表示无裁剪）。</summary>
    public UIRect? CurrentClip(int targetId)
        => _clipStacks.TryGetValue(targetId, out var stack) && stack.Count > 0 ? stack.Peek() : null;

    private static UIRect Intersect(UIRect a, UIRect b)
    {
        float x = System.Math.Max(a.X, b.X);
        float y = System.Math.Max(a.Y, b.Y);
        float right = System.Math.Min(a.Right, b.Right);
        float bottom = System.Math.Min(a.Bottom, b.Bottom);
        float w = System.Math.Max(0f, right - x);
        float h = System.Math.Max(0f, bottom - y);
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

    /// <summary>逻辑线程：排队一个 UI 纹理待渲染线程上传。</summary>
    public void EnqueueTexture(UITextureUpload upload) => _pendingTextures.Enqueue(upload);

    /// <summary>渲染线程：取一个待上传的 UI 纹理。</summary>
    public bool TryDequeueTexture(out UITextureUpload upload) => _pendingTextures.TryDequeue(out upload);

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
