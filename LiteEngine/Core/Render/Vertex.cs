using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Render;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Vertex
{
    // 位置
    public static readonly int LocationOffset = 0;
    public Vector3 Location;

    // 法向量
    public static readonly int NormalOffset = sizeof(Vector3);
    public Vector3 Normal;

    // 颜色
    public static readonly int ColorOffset = NormalOffset + sizeof(Vector3);
    public Vector3 Color;

    // UV坐标
    public static readonly int TexCoordOffset = ColorOffset + sizeof(Vector3);
    public Vector2 TexCoord;
}


