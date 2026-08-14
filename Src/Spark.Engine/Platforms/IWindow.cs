using Silk.NET.WebGPU;
using System.Numerics;

namespace Spark.Engine.Platforms;

public unsafe interface IWindow
{
    Vector2 Size { get; set; }

    string Title { get; set; }

    bool IsClosing { get; }

    /// <summary>WebGPU surface, available after <see cref="Initialize"/> has run.</summary>
    Surface* Surface { get; }

    void Initialize();

    void Uninitialize();

    void PollEvents();

    void Close();
}
