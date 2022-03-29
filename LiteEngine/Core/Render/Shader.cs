using LiteEngine.Core.SubSystem;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Render;

public class Shader
{
    private GL gl { get => Engine.Instance.Gl; }
    private FileSystem fileSystem { get => Engine.Instance.FileSystem; }

    public uint Id { get; private set; }
    public Shader(string vertex, string frag)
    {

        var VertexShaderSource = fileSystem.LoadFileString(vertex).Replace("{GLVERSION}", Engine.Instance.ShaderHead);
        var FragmentShaderSource = fileSystem.LoadFileString(frag).Replace("{GLVERSION}", Engine.Instance.ShaderHead);
        uint vertexShader = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vertexShader, VertexShaderSource);
        gl.CompileShader(vertexShader);

        string infoLog = gl.GetShaderInfoLog(vertexShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            throw new ($"Error compiling vertex shader {infoLog}");
        }

        uint fragmentShader = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fragmentShader, FragmentShaderSource);
        gl.CompileShader(fragmentShader);

        infoLog = gl.GetShaderInfoLog(fragmentShader);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            throw new ($"Error compiling fragment shader {infoLog}");
        }

        Id = gl.CreateProgram();
        gl.AttachShader(Id, vertexShader);
        gl.AttachShader(Id, fragmentShader);
        gl.LinkProgram(Id);

        gl.GetProgram(Id, GLEnum.LinkStatus, out var status);
        if (status == 0)
        {
            throw new ($"Error linking shader {gl.GetProgramInfoLog(Id)}");
        }

        gl.DetachShader(Id, vertexShader);
        gl.DetachShader(Id, fragmentShader);
        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);

    }

    public void Use()
    {
        gl.UseProgram(Id);
    }
}
