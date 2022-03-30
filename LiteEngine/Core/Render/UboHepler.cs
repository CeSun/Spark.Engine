using Silk.NET.OpenGL;
using System.Numerics;

namespace LiteEngine.Core.Render;
public class UboHepler
{
    public uint Ubo;
    uint Size;
    GL gl { get => Engine.Instance.Gl; }
    public unsafe UboHepler(uint size)
    {
        Size = size;
        Ubo = gl.GenBuffer();
        gl.BindBuffer(GLEnum.UniformBuffer, Ubo);
        gl.BufferData(GLEnum.UniformBuffer, size, null, BufferUsageARB.DynamicDraw);
        gl.BindBuffer(GLEnum.UniformBuffer, 0);
        gl.BindBufferBase(GLEnum.UniformBuffer, 1, Ubo);
        gl.BindBuffer(GLEnum.UniformBuffer, 0);
    }

    public unsafe void UpdateData(void* data)
    {
        gl.BindBuffer(GLEnum.UniformBuffer, Ubo);
        gl.BufferSubData(GLEnum.UniformBuffer, 0, Size, data);
    }


}
