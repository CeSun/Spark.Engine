// #define TraceShaderUniformError 

using System.Diagnostics;
using System.Numerics;
using Silk.NET.OpenGLES;
using Spark.Engine.Platform;

namespace Spark.Engine.Render;

public class Shader
{
    public uint ProgramId;
    public string VertShaderSource;
    public string FragShaderSource;
    public GL gl;
    List<string> Macros;

    public string? Path;
    
    public Shader (string VertShaderSource, string FragShaderSource, List<string> Macros, GL GL)
    {
        this.VertShaderSource = VertShaderSource;
        this.FragShaderSource = FragShaderSource;
        this.Macros = Macros;
        this.gl = GL;
        InitRender();
    }

    public void InitRender()
    {
        VertShaderSource = AddMacros(VertShaderSource, Macros);
        FragShaderSource = AddMacros(FragShaderSource, Macros);
        var vert = gl.CreateShader(GLEnum.VertexShader);
        gl.ShaderSource(vert, VertShaderSource);
        gl.CompileShader(vert);
        gl.GetShader(vert, GLEnum.CompileStatus, out int code);
        if (code == 0)
        {
            var info = gl.GetShaderInfoLog(vert);
            Console.WriteLine(VertShaderSource);
            throw new Exception(info);
        }
        var frag = gl.CreateShader(GLEnum.FragmentShader);
        gl.ShaderSource(frag, FragShaderSource);
        gl.CompileShader(frag);
        gl.GetShader(frag, GLEnum.CompileStatus, out code);
        if (code == 0)
        {
            gl.DeleteShader(vert);
            var info = gl.GetShaderInfoLog(frag);
            Console.WriteLine(FragShaderSource);
            throw new Exception(info);
        }
     
        ProgramId = gl.CreateProgram();
        gl.AttachShader(ProgramId, vert);
        gl.AttachShader(ProgramId, frag);
        gl.LinkProgram(ProgramId);
        gl.GetProgram(ProgramId, GLEnum.LinkStatus, out code);
        if (code == 0)
        {
            gl.DeleteShader(vert);
            gl.DeleteShader(frag);

            var info = gl.GetProgramInfoLog(ProgramId);
            throw new Exception(info);
        }
        gl.DeleteShader(vert);
        gl.DeleteShader(frag);
    }
  
    public void SetInt(string name, int value)
    {
        if (gl == null)
            return;
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
#if DEBUG && TraceShaderUniformError
        if (location < 0)
        {
            string stackInfo = new StackTrace().ToString();
            Console.WriteLine("Not Found Location: " + name);
            Console.WriteLine(stackInfo);
        }

#else
        if (location < 0)
        {
            return;
        }
#endif
        gl.Uniform1(location, value);
    }

    public void SetFloat(string name, float value)
    {
        if (gl == null)
            return;
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
#if DEBUG && TraceShaderUniformError
        if (location < 0)
        {
            string stackInfo = new StackTrace().ToString();
            Console.WriteLine("Not Found Location: " + name);
            Console.WriteLine(stackInfo);
        }

#else
        if (location < 0)
        {
            return;
        }
#endif
        gl.Uniform1(location, value);
    }

    public void SetVector2(string name, Vector2 value)
    {
        if (gl == null)
            return;
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
#if DEBUG && TraceShaderUniformError
        if (location < 0)
        {
            string stackInfo = new StackTrace().ToString();
            Console.WriteLine("Not Found Location: " + name);
            Console.WriteLine(stackInfo);
        }

#else
        if (location < 0)
        {
            return;
        }
#endif
        gl.Uniform2(location, value);
    }


    public void SetVector3(string name, Vector3 value)
    {
        if (gl == null)
            return;
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
#if DEBUG && TraceShaderUniformError
        if (location < 0)
        {
            string stackInfo = new StackTrace().ToString();
            Console.WriteLine("Not Found Location: " + name);
            Console.WriteLine(stackInfo);
        }

#else
        if (location < 0)
        {
            return;
        }
#endif
        gl.Uniform3(location, value);
    }

    public unsafe void SetMatrix(string name, Matrix4x4 value)
    {
        if (gl == null)
            return;
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
#if DEBUG && TraceShaderUniformError
        if (location < 0)
        {
            string stackInfo = new StackTrace().ToString();
            Console.WriteLine("Not Found Location: " + name);
            Console.WriteLine(stackInfo);
        }
#else
        if (location < 0)
        {
            return;
        }
#endif
        gl.UniformMatrix4(location, 1, false, (float*)&value);
    }
    public void Use()
    {
        if (gl == null)
            return;
        gl.UseProgram(ProgramId);
    }

    public void UnUse()
    {
        if (gl == null)
            return;
        gl.UseProgram(0);
    }

    public string AddMacros(string source, List<string> Macros)
    {
        var lines = source.Split("\n").ToList();
        int i = 0;
        for (; i < lines.Count; i++)
        {
            if (lines[i].Trim().IndexOf("#version") != 0)
                continue;
            break;
        }
        foreach (var macros in Macros)
        {
            lines.Insert(i + 1, "#define " + macros);
        }
        return string.Join("\n", lines);
    }


}
