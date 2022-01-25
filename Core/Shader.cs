using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class Shader
    {
        private static Dictionary<(string, string), Shader> ShaderPool = new Dictionary<(string, string), Shader>();

        public static Shader LoadShader(string vertShaderPath, string fragShaderPath)
        {
            var shader = ShaderPool.GetValueOrDefault((vertShaderPath, fragShaderPath));
            if (shader == null)
            {
                shader = LoadShaderFromSource(File.ReadAllText(vertShaderPath), File.ReadAllText(fragShaderPath));
                ShaderPool.Add((vertShaderPath, fragShaderPath), shader);
            }
            return shader;
        }

        public static Shader LoadShaderFromSource(string vertShaderSource, string fragShaderSource)
        {
            var shader = new Shader();
            var VertId = CreateShader(vertShaderSource, ShaderType.VertexShader);
            var FragId = CreateShader(fragShaderSource, ShaderType.FragmentShader);
            var programId = LinkProgram(VertId, FragId);
            shader.ProgramId = programId;
            GL.GetProgram(programId, GetProgramParameterName.ActiveUniforms, out var nums);
            for (var i = 0; i < nums; i++)
            {
                var key = GL.GetActiveUniform(programId, i, out _, out _);
                var location = GL.GetUniformLocation(programId, key);
                shader.UniformLocations.Add(key, location);
            }
            return shader;
        }
        private static int CreateShader(string source, ShaderType type)
        {
            var id = GL.CreateShader(type);
            GL.ShaderSource(id, source);
            GL.CompileShader(id);
            GL.GetShader(id, ShaderParameter.CompileStatus, out var status);
            if (status != (int)(All.True))
            {
                var error = GL.GetShaderInfoLog(id);
                throw new Exception($"shader, error:{error}");
            }
            return id;
        }

        private static int LinkProgram(int vertShader, int fragShader)
        {
            var id = GL.CreateProgram();
            GL.AttachShader(id, vertShader);
            GL.AttachShader(id, fragShader);
            GL.LinkProgram(id);
            GL.GetProgram(id, GetProgramParameterName.LinkStatus, out var status);
            if (status != (int)(All.True))
            {
                var info = GL.GetProgramInfoLog(id);
                throw new Exception($"Link Program shader error{info}");
            }
            GL.DetachShader(id, vertShader);
            GL.DetachShader(id, fragShader);
            GL.DeleteShader(fragShader);
            GL.DeleteShader(vertShader);
            return id;
        }

        public void Use()
        {
            GL.UseProgram(ProgramId);
        }

        public void SetVector4(string name, Vector4 data)
        {
            Use();
            GL.Uniform4(UniformLocations[name], ref data);
        }
        public void SetVector3(string name, Vector3 data)
        {
            Use();
            GL.Uniform3(UniformLocations[name], ref data);
        }
        public void SetVector2(string name, Vector2 data)
        {
            Use();
            GL.Uniform2(UniformLocations[name], ref data);
        }
        public void SetFloat(string name, float data)
        {
            Use();
            GL.Uniform1(UniformLocations[name], data);
        }
        public void SetMatrix4(string name, Matrix4 data)
        {
            Use();
            GL.UniformMatrix4(UniformLocations[name], true, ref data);
        }
        public void SetMatrix2(string name, Matrix2 data)
        {
            Use();
            GL.UniformMatrix2(UniformLocations[name], true, ref data);
        }
        public void SetMatrix3(string name, Matrix3 data)
        {
            Use();
            GL.UniformMatrix3(UniformLocations[name], true, ref data);
        }

        public void SetInt(string name, int data)
        {
            Use();
            GL.Uniform1(UniformLocations[name], data);
        }
        public int GetAttribLocation(string attribName)
        {
            return GL.GetAttribLocation(ProgramId, attribName);
        }

        private int ProgramId;

        private Dictionary<string, int> UniformLocations = new Dictionary<string, int>();
    }
}
