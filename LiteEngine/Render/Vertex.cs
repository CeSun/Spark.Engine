using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.Render;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Vertex
{
    // 位置
    public static readonly int LocationOffset = 0;
    public Vector3 Location;

    // 法向量
    public static readonly int NormalOffset = 12;
    public Vector3 Normal;

    // 颜色
    public static readonly int ColorOffset = 24;
    public Vector3 Color;

    // UV坐标
    public static readonly int TexCoordOffset = 36;
    public Vector2 TexCoord;
}


