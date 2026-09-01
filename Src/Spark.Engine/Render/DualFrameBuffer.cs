using System;
using System.Threading;

namespace Spark.Engine.Render;

/// <summary>
/// 单生产者/单消费者双缓冲帧同步器。
/// 逻辑线程写入，渲染线程读取，最多只允许超前 1 帧。
/// </summary>
/// <typeparam name="T">缓冲区数据类型</typeparam>
public sealed class DualFrameBuffer<T> : IDisposable
{
    private readonly T[] _buffers;
    private readonly SemaphoreSlim _emptySlots;
    private readonly SemaphoreSlim _readySlots;
    private readonly SemaphoreSlim _readySlotAvailable;
    private readonly CancellationTokenSource _disposeCts = new();

    private int _emptyIdx;
    private int _readyIdx;
    private int _writingIdx;
    private int _disposed;

    public DualFrameBuffer(Func<T> bufferFactory)
    {
        if (bufferFactory == null)
            throw new ArgumentNullException(nameof(bufferFactory));

        _buffers = new[] { bufferFactory(), bufferFactory() };
        _emptySlots = new SemaphoreSlim(2, 2);
        _readySlots = new SemaphoreSlim(0, 1);
        _readySlotAvailable = new SemaphoreSlim(1, 1);

        _emptyIdx = 0;
        _readyIdx = -1;
        _writingIdx = 0;
    }

    public T GetEmptyBuffer()
    {
        ThrowIfDisposed();
        _emptySlots.Wait(_disposeCts.Token);
        ThrowIfDisposed();

        int idx = Volatile.Read(ref _emptyIdx);
        Volatile.Write(ref _writingIdx, idx);
        return _buffers[idx];
    }

    public void SubmitReady()
    {
        ThrowIfDisposed();
        _readySlotAvailable.Wait(_disposeCts.Token);
        ThrowIfDisposed();

        CommitReadyFrame();
    }

    public T GetReadyBuffer()
    {
        ThrowIfDisposed();
        _readySlots.Wait(_disposeCts.Token);
        ThrowIfDisposed();

        return GetReadyBufferCore();
    }

    public void ReturnEmpty()
    {
        ThrowIfDisposed();

        int readyIdx = Volatile.Read(ref _readyIdx);
        Volatile.Write(ref _readyIdx, -1);
        Volatile.Write(ref _emptyIdx, readyIdx);

        _emptySlots.Release();
        _readySlotAvailable.Release();
    }

    /// <summary>
    /// 归还已取但未提交的空槽（异常路径回滚）：不提交帧，仅把当前写缓冲退回空池。
    /// 供逻辑线程在 <see cref="GetEmptyBuffer"/> 之后、<see cref="SubmitReady"/> 之前发生异常时调用，防止帧槽泄漏（S2）。
    /// </summary>
    public void Abandon()
    {
        // 已 Dispose 时静默返回（此时空槽已无意义，避免对已释放信号量 Release）
        if (Volatile.Read(ref _disposed) != 0)
            return;

        int idx = Volatile.Read(ref _writingIdx);
        Volatile.Write(ref _emptyIdx, idx);
        _emptySlots.Release();
    }

    /// <summary>
    /// 请求停止所有等待者，但保留同步原语，直到生产者和消费者都退出后再调用 <see cref="Dispose"/>。
    /// </summary>
    public void RequestStop()
    {
        if (Volatile.Read(ref _disposed) == 0)
            _disposeCts.Cancel();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _disposeCts.Cancel();
        _emptySlots.Dispose();
        _readySlots.Dispose();
        _readySlotAvailable.Dispose();
        _disposeCts.Dispose();
    }

    private void CommitReadyFrame()
    {
        int writtenIdx = Volatile.Read(ref _writingIdx);
        int oldReady = Interlocked.Exchange(ref _readyIdx, writtenIdx);

        if (oldReady == -1)
        {
            Volatile.Write(ref _emptyIdx, 1 - writtenIdx);
        }
        else
        {
            Volatile.Write(ref _emptyIdx, oldReady);
        }

        _readySlots.Release();
    }

    private T GetReadyBufferCore()
    {
        int idx = Volatile.Read(ref _readyIdx);
        if (idx < 0 || idx >= _buffers.Length)
            throw new InvalidOperationException("The ready frame index is invalid.");

        return _buffers[idx];
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(DualFrameBuffer<T>));
    }
}
