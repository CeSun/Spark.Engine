using System;
using System.Numerics;

namespace Spark.Engine.UI;

/// <summary>UI 尺寸（逻辑像素）。</summary>
public struct UISize
{
    public float Width;
    public float Height;

    public UISize(float width, float height)
    {
        Width = width;
        Height = height;
    }
}

/// <summary>UI 矩形（逻辑像素，左上原点，Y 向下）。</summary>
public struct UIRect
{
    public float X;
    public float Y;
    public float Width;
    public float Height;

    public UIRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public float Right => X + Width;

    public float Bottom => Y + Height;

    /// <summary>点是否落在矩形内（含边界）。</summary>
    public bool Contains(Vector2 point)
        => point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    /// <summary>向内收缩内边距后的内容矩形。</summary>
    public UIRect Deflate(UIEdgeInsets insets) => new UIRect(
        X + insets.Left,
        Y + insets.Top,
        System.Math.Max(0f, Width - insets.Left - insets.Right),
        System.Math.Max(0f, Height - insets.Top - insets.Bottom));
}

/// <summary>UI 内边距/外边距（左/上/右/下）。</summary>
public struct UIEdgeInsets
{
    public float Left;
    public float Top;
    public float Right;
    public float Bottom;

    public UIEdgeInsets(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static UIEdgeInsets All(float value) => new(value, value, value, value);

    public static UIEdgeInsets HorizontalVertical(float horizontal, float vertical) => new(horizontal, vertical, horizontal, vertical);
}
