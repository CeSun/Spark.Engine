using Silk.NET.OpenGL;
using System.Numerics;

namespace LiteEngine.Core.Render.Object;
public class UniformBufferObject
{
    public uint Ubo;
    uint Size;
    GL gl { get => Engine.Instance.Gl; }
    public unsafe UniformBufferObject(uint size)
    {
        Size = size;
        Ubo = gl.GenBuffer();
        gl.BindBuffer(GLEnum.UniformBuffer, Ubo);
        gl.BufferData(GLEnum.UniformBuffer, size, null, BufferUsageARB.DynamicDraw);
        gl.BindBuffer(GLEnum.UniformBuffer, 0);
        gl.BindBufferRange(GLEnum.UniformBuffer, 0, Ubo, 0, size);
    }

    public unsafe void UpdateData(void* data, nint offset, uint size)
    {
        gl.BindBuffer(GLEnum.UniformBuffer, Ubo);
        gl.BufferSubData(GLEnum.UniformBuffer, offset, size, data);
        gl.BindBuffer(GLEnum.UniformBuffer, 0);
    }

    public unsafe void UpdateData(void* data)
    {
        UpdateData(data, 0, Size);
    }

    public void Use()
    {
        gl.BindBuffer(GLEnum.UniformBuffer, Ubo);
    }

    public void Clear()
    {
        gl.BindBuffer(GLEnum.UniformBuffer, 0);
    }

}
