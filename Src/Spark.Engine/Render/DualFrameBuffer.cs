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
    private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromSeconds(30);

    private readonly T[] _buffers;
    private readonly SemaphoreSlim _emptySlots;
    private readonly SemaphoreSlim _readySlots;
    private readonly SemaphoreSlim _readySlotAvailable;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly TimeSpan _waitTimeout;

    private int _emptyIdx;
    private int _readyIdx;
    private int _writingIdx;
    private int _disposed;

    public DualFrameBuffer(Func<T> bufferFactory)
        : this(bufferFactory, DefaultWaitTimeout)
    {
    }

    /// <summary>
    /// 创建双缓冲，并为每个同步点设置最长等待时间。
    /// 超时用于把渲染线程/GPU 停滞从“窗口无响应”转换为可诊断的引擎异常。
    /// </summary>
    public DualFrameBuffer(Func<T> bufferFactory, TimeSpan waitTimeout)
    {
        if (bufferFactory == null)
            throw new ArgumentNullException(nameof(bufferFactory));
        if (waitTimeout <= TimeSpan.Zero || waitTimeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(waitTimeout));

        _waitTimeout = waitTimeout;
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
        WaitFor(_emptySlots, "empty buffer");
        ThrowIfDisposed();

        int idx = Volatile.Read(ref _emptyIdx);
        Volatile.Write(ref _writingIdx, idx);
        return _buffers[idx];
    }

    public void SubmitReady()
    {
        ThrowIfDisposed();
        WaitFor(_readySlotAvailable, "ready slot");
        ThrowIfDisposed();

        CommitReadyFrame();
    }

    public T GetReadyBuffer()
    {
        ThrowIfDisposed();
        WaitFor(_readySlots, "ready buffer");
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

    private void WaitFor(SemaphoreSlim semaphore, string resource)
    {
        if (semaphore.Wait(_waitTimeout, _disposeCts.Token))
            return;

        throw new TimeoutException(
            $"Timed out waiting for {resource} after {_waitTimeout.TotalSeconds:0.###}s. " +
            $"empty={_emptySlots.CurrentCount}, ready={_readySlots.CurrentCount}, " +
            $"readySlot={_readySlotAvailable.CurrentCount}, " +
            $"emptyIdx={Volatile.Read(ref _emptyIdx)}, readyIdx={Volatile.Read(ref _readyIdx)}, " +
            $"writingIdx={Volatile.Read(ref _writingIdx)}.");
    }
}
