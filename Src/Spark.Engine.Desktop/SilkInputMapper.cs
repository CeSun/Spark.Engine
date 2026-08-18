using Spark.Engine.Input;
using SilkKey = Silk.NET.Input.Key;

namespace Spark.Engine.Desktop;

/// <summary>把 Silk.NET.Input 枚举映射为引擎自有枚举（平台类型不出核心库，P22）。</summary>
internal static class SilkInputMapper
{
    /// <summary>把 Silk 按键映射为引擎 <see cref="Key"/>；未覆盖的按键返回 <see cref="Key.Unknown"/>。</summary>
    public static Key MapKey(SilkKey key) => key switch
    {
        SilkKey.A => Key.A,
        SilkKey.B => Key.B,
        SilkKey.C => Key.C,
        SilkKey.D => Key.D,
        SilkKey.E => Key.E,
        SilkKey.F => Key.F,
        SilkKey.G => Key.G,
        SilkKey.H => Key.H,
        SilkKey.I => Key.I,
        SilkKey.J => Key.J,
        SilkKey.K => Key.K,
        SilkKey.L => Key.L,
        SilkKey.M => Key.M,
        SilkKey.N => Key.N,
        SilkKey.O => Key.O,
        SilkKey.P => Key.P,
        SilkKey.Q => Key.Q,
        SilkKey.R => Key.R,
        SilkKey.S => Key.S,
        SilkKey.T => Key.T,
        SilkKey.U => Key.U,
        SilkKey.V => Key.V,
        SilkKey.W => Key.W,
        SilkKey.X => Key.X,
        SilkKey.Y => Key.Y,
        SilkKey.Z => Key.Z,

        SilkKey.Number0 => Key.D0,
        SilkKey.Number1 => Key.D1,
        SilkKey.Number2 => Key.D2,
        SilkKey.Number3 => Key.D3,
        SilkKey.Number4 => Key.D4,
        SilkKey.Number5 => Key.D5,
        SilkKey.Number6 => Key.D6,
        SilkKey.Number7 => Key.D7,
        SilkKey.Number8 => Key.D8,
        SilkKey.Number9 => Key.D9,

        SilkKey.F1 => Key.F1,
        SilkKey.F2 => Key.F2,
        SilkKey.F3 => Key.F3,
        SilkKey.F4 => Key.F4,
        SilkKey.F5 => Key.F5,
        SilkKey.F6 => Key.F6,
        SilkKey.F7 => Key.F7,
        SilkKey.F8 => Key.F8,
        SilkKey.F9 => Key.F9,
        SilkKey.F10 => Key.F10,
        SilkKey.F11 => Key.F11,
        SilkKey.F12 => Key.F12,

        SilkKey.ShiftLeft => Key.LeftShift,
        SilkKey.ShiftRight => Key.RightShift,
        SilkKey.ControlLeft => Key.LeftControl,
        SilkKey.ControlRight => Key.RightControl,
        SilkKey.AltLeft => Key.LeftAlt,
        SilkKey.AltRight => Key.RightAlt,

        SilkKey.Space => Key.Space,
        SilkKey.Enter => Key.Enter,
        SilkKey.Escape => Key.Escape,
        SilkKey.Tab => Key.Tab,
        SilkKey.Backspace => Key.Backspace,
        SilkKey.Delete => Key.Delete,
        SilkKey.Insert => Key.Insert,

        SilkKey.Up => Key.Up,
        SilkKey.Down => Key.Down,
        SilkKey.Left => Key.Left,
        SilkKey.Right => Key.Right,

        SilkKey.Home => Key.Home,
        SilkKey.End => Key.End,
        SilkKey.PageUp => Key.PageUp,
        SilkKey.PageDown => Key.PageDown,

        SilkKey.CapsLock => Key.CapsLock,
        SilkKey.NumLock => Key.NumLock,
        SilkKey.PrintScreen => Key.PrintScreen,
        SilkKey.Pause => Key.Pause,

        SilkKey.GraveAccent => Key.Grave,
        SilkKey.Minus => Key.Minus,
        SilkKey.Equal => Key.Equal,
        SilkKey.LeftBracket => Key.LeftBracket,
        SilkKey.RightBracket => Key.RightBracket,
        SilkKey.BackSlash => Key.Backslash,
        SilkKey.Semicolon => Key.Semicolon,
        SilkKey.Apostrophe => Key.Apostrophe,
        SilkKey.Comma => Key.Comma,
        SilkKey.Period => Key.Period,
        SilkKey.Slash => Key.Slash,

        _ => Key.Unknown,
    };
}
