using System;

namespace Spark.Engine.Input;

/// <summary>鼠标按钮（平台无关，平台层把原生枚举映射到本枚举）。</summary>
public enum MouseButton : byte
{
    Left = 0,
    Right = 1,
    Middle = 2,
    Button4 = 3,
    Button5 = 4,
    Button6 = 5,
    Button7 = 6,
    Button8 = 7,
}

/// <summary>8 按钮按位掩码（帧内按下状态，零分配）。</summary>
public struct MouseButtonMask : IEquatable<MouseButtonMask>
{
    private byte _bits;

    public static MouseButtonMask None => default;

    public bool Any => _bits != 0;

    public bool IsDown(MouseButton button) => (_bits & (1 << (int)button)) != 0;

    public void Set(MouseButton button, bool down)
    {
        int bit = 1 << (int)button;
        _bits = down ? (byte)(_bits | bit) : (byte)(_bits & ~bit);
    }

    /// <summary>返回 this 中排除 <paramref name="other"/> 的部分（用于算 pressed/released 边沿）。</summary>
    public MouseButtonMask AndNot(MouseButtonMask other)
    {
        var result = default(MouseButtonMask);
        result._bits = (byte)(_bits & ~other._bits);
        return result;
    }

    public bool Equals(MouseButtonMask other) => _bits == other._bits;

    public override bool Equals(object? obj) => obj is MouseButtonMask other && Equals(other);

    public override int GetHashCode() => _bits;
}
