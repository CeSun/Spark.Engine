using System;

namespace Spark.Engine.Input;

/// <summary>
/// 引擎键盘按键（平台无关的常用子集，值稳定，平台层把原生枚举映射到本枚举）。
/// 文本输入走 <see cref="InputState.Text"/>（字符），不经过本枚举。
/// </summary>
public enum Key : byte
{
    Unknown = 0,

    // 字母
    A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8, I = 9, J = 10, K = 11, L = 12, M = 13,
    N = 14, O = 15, P = 16, Q = 17, R = 18, S = 19, T = 20, U = 21, V = 22, W = 23, X = 24, Y = 25, Z = 26,

    // 数字行
    D0 = 27, D1 = 28, D2 = 29, D3 = 30, D4 = 31, D5 = 32, D6 = 33, D7 = 34, D8 = 35, D9 = 36,

    // 功能键
    F1 = 37, F2 = 38, F3 = 39, F4 = 40, F5 = 41, F6 = 42, F7 = 43, F8 = 44,
    F9 = 45, F10 = 46, F11 = 47, F12 = 48,

    // 修饰键
    LeftShift = 49, RightShift = 50, LeftControl = 51, RightControl = 52, LeftAlt = 53, RightAlt = 54,

    // 常用键
    Space = 55, Enter = 56, Escape = 57, Tab = 58, Backspace = 59, Delete = 60, Insert = 61,

    // 方向键
    Up = 62, Down = 63, Left = 64, Right = 65,

    // 导航键
    Home = 66, End = 67, PageUp = 68, PageDown = 69,

    // 锁键/系统键
    CapsLock = 70, NumLock = 71, PrintScreen = 72, Pause = 73,

    // 常用符号
    Grave = 74, Minus = 75, Equal = 76, LeftBracket = 77, RightBracket = 78,
    Backslash = 79, Semicolon = 80, Apostrophe = 81, Comma = 82, Period = 83, Slash = 84,
}

/// <summary>按键按位掩码（128 位，覆盖 <see cref="Key"/> 全集，零分配）。</summary>
public struct KeyMask : IEquatable<KeyMask>
{
    private ulong _lo;
    private ulong _hi;

    public static KeyMask None => default;

    public bool Any => _lo != 0 || _hi != 0;

    public bool IsDown(Key key)
    {
        int index = (int)key;
        if (index < 64)
            return (_lo & (1UL << index)) != 0;
        if (index < 128)
            return (_hi & (1UL << (index - 64))) != 0;
        return false;
    }

    public void Set(Key key, bool down)
    {
        int index = (int)key;
        if (index < 0 || index >= 128)
            return;

        if (index < 64)
        {
            ulong bit = 1UL << index;
            _lo = down ? (_lo | bit) : (_lo & ~bit);
        }
        else
        {
            ulong bit = 1UL << (index - 64);
            _hi = down ? (_hi | bit) : (_hi & ~bit);
        }
    }

    /// <summary>返回 this 中排除 <paramref name="other"/> 的部分（用于算 pressed/released 边沿）。</summary>
    public KeyMask AndNot(KeyMask other)
    {
        var result = default(KeyMask);
        result._lo = _lo & ~other._lo;
        result._hi = _hi & ~other._hi;
        return result;
    }

    /// <summary>枚举所有处于按下状态的按键（按枚举值升序）。</summary>
    public IEnumerable<Key> Enumerate()
    {
        for (int i = 0; i < 128; i++)
        {
            var key = (Key)i;
            if (IsDown(key))
                yield return key;
        }
    }

    public bool Equals(KeyMask other) => _lo == other._lo && _hi == other._hi;

    public override bool Equals(object? obj) => obj is KeyMask other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_lo, _hi);
}
