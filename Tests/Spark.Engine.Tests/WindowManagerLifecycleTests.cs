using System.Numerics;
using Spark.Engine.Input;
using Spark.Engine.Platforms;
using Spark.Engine.Render.Common;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class WindowManagerLifecycleTests
{
    [Fact]
    public void CreateWindowFailureRollsBackNativeWindow()
    {
        var window = new TestWindow { FailInitialize = true };
        var manager = new WindowManager(new TestBackend(window), new RenderTargetRegistry());

        Assert.Throws<InvalidOperationException>(() => manager.CreateWindow("test", 320, 200));

        Assert.Empty(manager.Windows);
        Assert.Equal(1, window.UninitializeCount);
        Assert.Equal(1, window.DisposeNativeCount);
        manager.Dispose();
    }

    [Fact]
    public void DisposeCleansActiveAndPendingWindows()
    {
        var first = new TestWindow();
        var second = new TestWindow();
        var manager = new WindowManager(new TestBackend(first, second), new RenderTargetRegistry());

        manager.CreateWindow("first", 320, 200);
        manager.CreateWindow("second", 320, 200);
        manager.Dispose();
        manager.Dispose();

        Assert.Equal(1, first.UninitializeCount);
        Assert.Equal(1, first.DisposeNativeCount);
        Assert.Equal(1, second.UninitializeCount);
        Assert.Equal(1, second.DisposeNativeCount);
        Assert.Empty(manager.Windows);
    }

    private sealed class TestBackend(params TestWindow[] windows) : IWindowBackend
    {
        private int _index;

        public IWindow CreateWindow(string title, int width, int height)
            => windows[_index++];
    }

    private sealed class TestWindow : IWindow
    {
        private readonly WindowInput _input = new();
        private Vector2 _size;

        public bool FailInitialize { get; init; }
        public int UninitializeCount { get; private set; }
        public int DisposeNativeCount { get; private set; }

        public WindowInput Input => _input;
        public Vector2 Size { get => _size; set => _size = value; }
        public Vector2 FramebufferSize => _size;
        public string Title { get; set; } = string.Empty;
        public bool IsClosing => false;
        public RenderSurface? Surface => null;

        public void Initialize()
        {
            if (FailInitialize)
                throw new InvalidOperationException("window initialization failed");
        }

        public void Uninitialize() => UninitializeCount++;
        public void DisposeSurface() { }
        public void DisposeNative() => DisposeNativeCount++;
        public void PollEvents() { }
        public void Close() { }
    }
}
