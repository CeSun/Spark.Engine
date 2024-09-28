#define TraceShaderUniformError
using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using Silk.NET.OpenGLES;
using Spark.Core.Assets;

namespace Spark.Core.Render;
public class Shader
{
    public uint ProgramId;

    public required GL gl;
  
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

    public void SetTexture(string name, int offset, TextureProxy texture)
    {
        SetInt(name, offset);
        gl.ActiveTexture(GLEnum.Texture0 + offset);
        gl.BindTexture(GLEnum.Texture2D, texture.TextureId);
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
}


public static class ShaderHelper
{


    static string RemoveNonAsciiCharacters(string input)
    {
        // 使用正则表达式匹配非ASCII字符并替换为空字符串
        return Regex.Replace(input, @"[^\x00-\x7F]", string.Empty);
    }
    public static Shader CreateShader(this GL gl, string VertShaderSource, string FragShaderSource)
    {
        VertShaderSource = RemoveNonAsciiCharacters(VertShaderSource);
        FragShaderSource = RemoveNonAsciiCharacters(FragShaderSource);
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

        var ProgramId = gl.CreateProgram();
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

        return new Shader() { ProgramId = ProgramId, gl = gl };
    }
}