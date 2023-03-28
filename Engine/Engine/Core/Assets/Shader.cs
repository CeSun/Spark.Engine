using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Silk.NET.OpenGL;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core.Assets;

public class Shader : Asset
{
    public uint ProgramId;
    public string? VertShaderSource;
    public string? FragShaderSource;
    public Shader(string Path) : base(Path)
    {
        
    }
    
    protected override void LoadAsset()
    {
        using (StreamReader sr = new StreamReader("./Assets" + Path + ".vert"))
        {
            VertShaderSource =  sr.ReadToEnd();
        }
        using (StreamReader sr = new StreamReader("./Assets" + Path + ".frag"))
        {
            FragShaderSource =  sr.ReadToEnd();
        }

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
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
        gl.Uniform1(location, value);
    }

    public void SetFloat(string name, float value)
    {
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
        gl.Uniform1(location, value);
    }

    public void SetVector2(string name, Vector2 value)
    {
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
        gl.Uniform2(location, value);
    }

    public void SetVector3(string name, Vector3 value)
    {
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
        gl.Uniform3(location, value);
    }

    public unsafe void SetMatrix(string name, Matrix4x4 value)
    {
        gl.UseProgram(ProgramId);
        var location = gl.GetUniformLocation(ProgramId, name);
        gl.UniformMatrix4(location,1, false, (float*)&value);
    }
    public void Use()
    {
        gl.UseProgram(ProgramId);
    }

    public void UnUse()
    {
        gl.UseProgram(0);
    }

    public static Shader? GlobalShader;

}
