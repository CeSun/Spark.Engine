using Silk.NET.WebGPU;
using Spark.Engine.Builder;
using Spark.Engine.Render.Common;

namespace Spark.Engine.Render.RenderGraph;

/// <summary>
/// 帧内纹理资源池：复用 <see cref="TextureRenderTarget"/> 实例。
/// 相同描述（宽/高/格式/用途）的 transient 纹理可复用同一物理 GPU 纹理。
/// 资源在帧末归还空闲池，只有描述首次出现时才创建 GPU 纹理；渲染线程退出时统一释放。
/// </summary>
internal sealed unsafe class TransientResourcePool : IDisposable
{
    private readonly WebGPUContext? _webGpu;
    private readonly Dictionary<TextureResourceDesc, Stack<TextureRenderTarget>> _free = new();
    private readonly List<(TextureRenderTarget Target, TextureResourceDesc Desc)> _inUse = new();
    private int _nextId = -1000; // 负数 ID 避免与正数 external 冲突
    private int _disposed;

    public TransientResourcePool(WebGPUContext? webGpu)
    {
        _webGpu = webGpu;
    }

    /// <summary>分配一个 ID（负数，图内唯一）。</summary>
    public int AllocateId() => Interlocked.Decrement(ref _nextId);

    /// <summary>根据描述分配一个 TextureRenderTarget，优先复用空闲目标。</summary>
    public TextureRenderTarget Allocate(in TextureResourceDesc desc)
    {
        ThrowIfDisposed();
        if (_webGpu == null)
            throw new InvalidOperationException("A WebGPU context is required to allocate transient resources.");

        TextureRenderTarget target;
        if (_free.TryGetValue(desc, out var available) && available.Count > 0)
        {
            target = available.Pop();
        }
        else
        {
            target = new TextureRenderTarget(
                AllocateId(),
                _webGpu.Api,
                _webGpu.Device,
                desc.Width,
                desc.Height,
                desc.Format,
                desc.Usage,
                desc.IsDepth);
        }

        _inUse.Add((target, desc));
        return target;
    }

    /// <summary>帧末释放所有 transient 资源。</summary>
    public void ReleaseAll()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;

        ReleaseAllCore();
    }

    private void ReleaseAllCore()
    {
        foreach (var (target, desc) in _inUse)
        {
            if (!_free.TryGetValue(desc, out var available))
                _free[desc] = available = new Stack<TextureRenderTarget>();
            available.Push(target);
        }
        _inUse.Clear();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // Dispose 允许释放当前帧尚未归还的目标，避免异常路径泄漏。
        ReleaseAllCore();

        foreach (var targets in _free.Values)
            while (targets.Count > 0)
                targets.Pop().Dispose();
        _free.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(TransientResourcePool));
    }
}
