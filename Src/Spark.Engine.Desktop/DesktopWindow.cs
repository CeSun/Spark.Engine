using Spark.Engine.Builder;
using Spark.Engine.Input;
using Spark.Engine.Platforms;
using Spark.Engine.Render.Common;
using System.Numerics;
using SilkInput = Silk.NET.Input;
using SNW = Silk.NET.Windowing;

namespace Spark.Engine.Desktop;

public class DesktopWindow : IWindow, ICloseRequestWindow, IFileDropWindow
{
    private readonly SNW.IWindow _window;

    private readonly WebGPUContext _webGPUContext;

    private RenderSurface? _surface;

    private readonly WindowInput _input = new();

    private SilkInput.IInputContext? _inputContext;
    private WindowsImeContext? _imeContext;

    public Func<bool>? CloseRequested { get; set; }
    public event Action<IReadOnlyList<string>>? FilesDropped;

    public RenderSurface? Surface => _surface;

    public WindowInput Input => _input;

    public Vector2 Size { get => (Vector2)_window.Size; set => _window.Size = new Silk.NET.Maths.Vector2D<int>((int)value.X, (int)value.Y); }

    public Vector2 FramebufferSize
    {
        get
        {
            var fb = _window.FramebufferSize;
            // 窗口尚未显示时 framebuffer 尺寸可能为 0，回退到逻辑尺寸
            if (fb.X <= 0 || fb.Y <= 0)
                return (Vector2)_window.Size;
            return (Vector2)fb;
        }
    }

    public string Title { get => _window.Title; set => _window.Title = value; }

    public bool IsClosing => _window.IsClosing;

    public DesktopWindow(SNW.IWindow window, WebGPUContext webGPUContext)
    {
        _window = window;
        _webGPUContext = webGPUContext;
        _window.Closing += HandleClosing;
        _window.FileDrop += HandleFileDrop;
        _window.FocusChanged += HandleFocusChanged;
    }

    public void PollEvents()
    {
        _imeContext?.SetCandidatePosition(_input.ImeCandidatePosition);
        _window.DoEvents();
    }

    public void Close()
    {
        _window.Close();
    }

    public void Initialize()
    {
        _window.Initialize();

        _surface = _webGPUContext.CreateSurface(_window);

        var framebuffer = FramebufferSize;
        _surface.Resize((uint)framebuffer.X, (uint)framebuffer.Y);

        InitializeInput();
        if (OperatingSystem.IsWindows() && _window.Native?.Win32 is { } win32)
        {
            try { _imeContext = new WindowsImeContext(win32.Hwnd, _input); }
            catch { _imeContext = null; }
        }
    }

    public void Uninitialize()
    {
        // 只释放输入上下文。原生窗口销毁走 S4 握手：渲染线程释放 surface 后经 RenderTargetRegistry 登记，
        // 逻辑线程在下一帧 ProcessNativeDisposals 中调 DisposeNative 销毁（Silk/GLFW 原生窗口须在逻辑线程销毁）。
        _imeContext?.Dispose();
        _imeContext = null;
        _inputContext?.Dispose();
        _inputContext = null;
    }

    public void DisposeSurface()
    {
        _surface?.Dispose();
        _surface = null;
    }

    public void DisposeNative()
    {
        _window.FileDrop -= HandleFileDrop;
        _window.FocusChanged -= HandleFocusChanged;
        _window.Dispose();
    }

    private void HandleClosing()
    {
        var request = CloseRequested;
        if (request == null)
            return;

        bool allow;
        try { allow = request(); }
        catch { allow = false; }
        if (!allow)
            _window.IsClosing = false;
    }

    /// <summary>建立输入上下文并订阅鼠标/键盘事件，映射到引擎枚举后写入 <see cref="_input"/>。</summary>
    private void InitializeInput()
    {
        try
        {
            var context = SilkInput.InputWindowExtensions.CreateInput(_window);

            if (context.Mice.Count > 0)
            {
                var mouse = context.Mice[0];
                _input.MousePosition = mouse.Position;
                mouse.MouseDown += (_, button) => SetButton(button, down: true);
                mouse.MouseUp += (_, button) => SetButton(button, down: false);
                mouse.MouseMove += (_, position) =>
                {
                    _input.MouseDelta += position - _input.MousePosition;
                    _input.MousePosition = position;
                };
                mouse.Scroll += (_, wheel) => _input.ScrollDelta += wheel.Y;
            }

            if (context.Keyboards.Count > 0)
            {
                var keyboard = context.Keyboards[0];
                keyboard.KeyDown += (_, key, _) => SetKey(key, down: true);
                keyboard.KeyUp += (_, key, _) => SetKey(key, down: false);
                keyboard.KeyChar += (_, character) => _input.Text.Append(character);
            }

            _inputContext = context;
        }
        catch
        {
            // 输入是尽力而为：无可用输入后端（罕见）时窗口仍可用，只是没有输入事件。
            _inputContext = null;
        }
    }

    private void SetButton(SilkInput.MouseButton button, bool down)
    {
        int index = button switch
        {
            SilkInput.MouseButton.Left => 0,
            SilkInput.MouseButton.Right => 1,
            SilkInput.MouseButton.Middle => 2,
            SilkInput.MouseButton.Button4 => 3,
            SilkInput.MouseButton.Button5 => 4,
            SilkInput.MouseButton.Button6 => 5,
            SilkInput.MouseButton.Button7 => 6,
            SilkInput.MouseButton.Button8 => 7,
            _ => -1,
        };

        if (index >= 0)
            _input.Buttons.Set((MouseButton)index, down);
    }

    private void SetKey(SilkInput.Key key, bool down)
    {
        var mapped = SilkInputMapper.MapKey(key);
        if (mapped != Key.Unknown)
            _input.KeysDown.Set(mapped, down);
    }

    private void HandleFileDrop(string[] paths)
        => FilesDropped?.Invoke(paths);

    private void HandleFocusChanged(bool focused)
        => _input.SetFocused(focused);
}
