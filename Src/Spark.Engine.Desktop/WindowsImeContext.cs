using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Spark.Engine.Input;

namespace Spark.Engine.Desktop;

/// <summary>Win32 IME 组合串桥接；提交字符仍由 GLFW KeyChar 统一进入文本输入队列。</summary>
internal sealed class WindowsImeContext : IDisposable
{
    private const int WindowProcedureIndex = -4;
    private const uint ImeStartComposition = 0x010D;
    private const uint ImeEndComposition = 0x010E;
    private const uint ImeComposition = 0x010F;
    private const int CompositionString = 0x0008;
    private const int ResultString = 0x0800;
    private const uint CandidatePosition = 0x0040;

    private readonly nint _window;
    private readonly WindowInput _input;
    private readonly WindowProcedure _procedure;
    private readonly nint _procedurePointer;
    private readonly nint _previousProcedure;
    private int _disposed;

    public WindowsImeContext(nint window, WindowInput input)
    {
        if (window == 0)
            throw new ArgumentException("A Win32 HWND is required.", nameof(window));
        _window = window;
        _input = input;
        _procedure = ProcessWindowMessage;
        _procedurePointer = Marshal.GetFunctionPointerForDelegate(_procedure);
        Marshal.SetLastPInvokeError(0);
        _previousProcedure = SetWindowProcedure(_window, _procedurePointer);
        var error = Marshal.GetLastPInvokeError();
        if (_previousProcedure == 0 && error != 0)
            throw new InvalidOperationException(
                $"Failed to install the Win32 IME window procedure (error {error}).");
    }

    public void SetCandidatePosition(Vector2? position)
    {
        if (position is not { } point || Volatile.Read(ref _disposed) != 0)
            return;
        var inputContext = ImmGetContext(_window);
        if (inputContext == 0)
            return;
        try
        {
            var form = new CandidateForm
            {
                Style = CandidatePosition,
                CurrentPosition = new NativePoint(
                    (int)MathF.Round(point.X), (int)MathF.Round(point.Y)),
            };
            _ = ImmSetCandidateWindow(inputContext, ref form);
        }
        finally
        {
            _ = ImmReleaseContext(_window, inputContext);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _input.SetTextComposition(string.Empty, isComposing: false);
        _ = SetWindowProcedure(_window, _previousProcedure);
        GC.KeepAlive(_procedure);
    }

    private nint ProcessWindowMessage(nint window, uint message, nint wParam, nint lParam)
    {
        try
        {
            switch (message)
            {
                case ImeStartComposition:
                    _input.SetTextComposition(string.Empty, isComposing: true);
                    break;
                case ImeComposition:
                    var flags = unchecked((int)(long)lParam);
                    if ((flags & ResultString) != 0)
                        _input.SetTextComposition(string.Empty, isComposing: false);
                    else if ((flags & CompositionString) != 0)
                        _input.SetTextComposition(ReadCompositionString(window), isComposing: true);
                    break;
                case ImeEndComposition:
                    _input.SetTextComposition(string.Empty, isComposing: false);
                    break;
            }
        }
        catch
        {
            _input.SetTextComposition(string.Empty, isComposing: false);
        }
        return CallWindowProcW(_previousProcedure, window, message, wParam, lParam);
    }

    private static string ReadCompositionString(nint window)
    {
        var inputContext = ImmGetContext(window);
        if (inputContext == 0)
            return string.Empty;
        try
        {
            var byteCount = ImmGetCompositionStringW(inputContext, CompositionString, null, 0);
            if (byteCount <= 0)
                return string.Empty;
            var bytes = new byte[byteCount];
            var written = ImmGetCompositionStringW(inputContext, CompositionString, bytes, bytes.Length);
            return written > 0 ? Encoding.Unicode.GetString(bytes, 0, written) : string.Empty;
        }
        finally
        {
            _ = ImmReleaseContext(window, inputContext);
        }
    }

    private static nint SetWindowProcedure(nint window, nint procedure)
        => Environment.Is64BitProcess
            ? SetWindowLongPtr64(window, WindowProcedureIndex, procedure)
            : new nint(SetWindowLong32(window, WindowProcedureIndex, procedure.ToInt32()));

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y)
    {
        public readonly int X = x;
        public readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CandidateForm
    {
        public uint Index;
        public uint Style;
        public NativePoint CurrentPosition;
        public NativeRect Area;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint window, int index, nint value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint window, int index, int value);

    [DllImport("user32.dll")]
    private static extern nint CallWindowProcW(
        nint previousProcedure, nint window, uint message, nint wParam, nint lParam);

    [DllImport("imm32.dll")]
    private static extern nint ImmGetContext(nint window);

    [DllImport("imm32.dll")]
    private static extern int ImmReleaseContext(nint window, nint inputContext);

    [DllImport("imm32.dll")]
    private static extern int ImmGetCompositionStringW(
        nint inputContext, int index, byte[]? buffer, int bufferLength);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmSetCandidateWindow(nint inputContext, ref CandidateForm candidateForm);
}
