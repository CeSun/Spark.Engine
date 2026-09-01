using Spark.Engine.Platforms;
using Spark.Engine.Render.Common;
using System.Runtime.ExceptionServices;

namespace Spark.Engine;

public class WindowManager
{
    private List<IWindow> _windows = new();
    private List<IWindow> _pendingAddWindows = new();
    private List<IWindow> _pendingRemoveWindows = new();

    private readonly IWindowBackend _windowBackend;
    private readonly RenderTargetRegistry _targets;
    private readonly Dictionary<IWindow, Viewport> _viewports = new();
    private int _disposed;

    public IReadOnlyList<IWindow> Windows => _windows;

    public IWindow MainWindow => _windows.Count > 0 ? _windows[0] : throw new InvalidOperationException("No main window available.");

    public WindowManager(IWindowBackend windowBackend, RenderTargetRegistry targets)
    {
        _windowBackend = windowBackend ?? throw new ArgumentNullException(nameof(windowBackend));
        _targets = targets;
    }

    public IWindow CreateWindow(string title, int width, int height)
    {
        ThrowIfDisposed();
        var window = _windowBackend.CreateWindow(title, width, height);
        Viewport? viewport = null;
        bool initialized = false;

        try
        {
            if (Windows.Count == 0)
            {
                window.Initialize();
                initialized = true;
                _windows.Add(window);
            }
            else
            {
                _pendingAddWindows.Add(window);
            }

            // 创建并注册窗口渲染目标（帧由相机驱动，Viewport 本身不持有相机）
            viewport = new Viewport(_targets.AllocateId(), window);
            _viewports.Add(window, viewport);
            _targets.Register(viewport);

            return window;
        }
        catch
        {
            _pendingAddWindows.Remove(window);
            _windows.Remove(window);
            if (viewport != null)
            {
                _viewports.Remove(window);
                if (_targets.Discard(viewport.Id, out var discarded))
                    discarded?.Dispose();
            }

            TryCleanupWindow(window, initialized);
            throw;
        }
    }

    /// <summary>获取窗口对应的渲染视口。</summary>
    public Viewport? GetViewport(IWindow window) => _viewports.TryGetValue(window, out var vp) ? vp : null;

    /// <summary>
    /// 排空渲染线程已释放 surface 的窗口，销毁其原生句柄（逻辑线程执行）。
    /// Silk/GLFW 原生窗口必须在创建它的逻辑线程销毁，故由渲染线程释放 surface 后经 <see cref="RenderTargetRegistry"/> 回传此处。
    /// </summary>
    public void ProcessNativeDisposals()
    {
        while (_targets.TryDequeueNativeDisposal(out var window))
        {
            if (window != null)
                window.DisposeNative();
        }
    }

    private void RemoveWindow(IWindow window)
    {
        if (_windows.Contains(window))
        {
            _pendingRemoveWindows.Add(window);
        }
    }

    public void UpdateWindow()
    {
        ThrowIfDisposed();
        // 销毁上一帧已被渲染线程释放 surface 的窗口原生句柄（逻辑线程执行，S4 握手）
        ProcessNativeDisposals();

        if (_pendingAddWindows.Count > 0)
        {
            foreach (var window in _pendingAddWindows)
            {
                window.Initialize();
            }
            _windows.AddRange(_pendingAddWindows);
            _pendingAddWindows.Clear();
        }

        foreach (var window in _windows)
        {
            window.PollEvents();

            if (window.IsClosing)
            {
                RemoveWindow(window);
            }
        }

        if (_pendingRemoveWindows.Count > 0)
        {
            foreach (var window in _pendingRemoveWindows)
            {
                // 注销渲染目标：入延迟删除队列，渲染线程帧末释放 surface（ADR-7）
                if (_viewports.Remove(window, out var viewport))
                {
                    _targets.Remove(viewport.Id);
                }

                window.Uninitialize();
                _windows.Remove(window);
            }
            _pendingRemoveWindows.Clear();
        }
    }

    /// <summary>释放所有窗口及其渲染目标。渲染线程退出后调用，可重复执行。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Exception? firstException = null;

        foreach (var window in _pendingAddWindows.ToArray())
        {
            try { TryCleanupWindow(window, initialized: false); }
            catch (Exception ex) { firstException ??= ex; }
        }
        _pendingAddWindows.Clear();

        foreach (var window in _windows.ToArray())
        {
            if (_viewports.Remove(window, out var viewport))
            {
                _targets.Discard(viewport.Id, out _);
                try { viewport.Dispose(); } catch (Exception ex) { firstException ??= ex; }
            }

            try { window.Uninitialize(); } catch (Exception ex) { firstException ??= ex; }
            try { window.DisposeNative(); } catch (Exception ex) { firstException ??= ex; }
        }

        _pendingRemoveWindows.Clear();
        _windows.Clear();
        _viewports.Clear();
        ProcessNativeDisposals();

        if (firstException != null)
            ExceptionDispatchInfo.Capture(firstException).Throw();
    }

    private void TryCleanupWindow(IWindow window, bool initialized)
    {
        Exception? firstException = null;
        if (initialized || window.Surface != null)
        {
            try { window.DisposeSurface(); } catch (Exception ex) { firstException ??= ex; }
        }

        try { window.Uninitialize(); } catch (Exception ex) { firstException ??= ex; }
        try { window.DisposeNative(); } catch (Exception ex) { firstException ??= ex; }

        if (firstException != null)
            ExceptionDispatchInfo.Capture(firstException).Throw();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(WindowManager));
    }
}
