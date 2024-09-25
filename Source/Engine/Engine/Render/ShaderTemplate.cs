using Silk.NET.OpenGLES;
using Spark.Core.Assets;
using System.Numerics;

namespace Spark.Core.Render;

public class ShaderTemplate : IDisposable
{
    public string Name = "";
    public string VertexShaderSource = "";
    public string FragmentShaderSource = "";
    public List<string> IncludeSource = [];
    public Dictionary<string, Shader> ShaderMap = new Dictionary<string, Shader>();

    private Shader? currentShader;
    public ShaderTemplate Use(GL gl, params List<string> macros)
    {
        var key = string.Join("_", macros!);
        if (ShaderMap.TryGetValue(key, out currentShader) == false)
        {
            var vertexShaderSource = PreProcessShaderSource(VertexShaderSource, macros);
            var fragmentShaderSource = PreProcessShaderSource(FragmentShaderSource, macros);
            // todo cache
            currentShader = gl.CreateShader(vertexShaderSource, fragmentShaderSource);
            ShaderMap.Add(key, currentShader);
        }
        currentShader.Use();
        return this;
    }

    public string PreProcessShaderSource(string shaderSource, List<string> macros)
    {
        var macroCode = string.Join("\n", (from macro in macros select "#define " + macro).ToList());
        return shaderSource.Replace("#version 300 es", "#version 300 es").
            Replace("//{MacroSourceCode}", macroCode).
            Replace("//{IncludeSourceCode}", string.Join("\n", IncludeSource));
    }

    public void Dispose()
    {
        currentShader?.UnUse();
        currentShader = null;
    }


    public void SetInt(string name, int value) => currentShader?.SetInt(name, value);

    public void SetFloat(string name, float value) => currentShader?.SetFloat(name, value);

    public void SetVector2(string name, Vector2 value) => currentShader?.SetVector2(name, value);

    public void SetVector3(string name, Vector3 value) => currentShader?.SetVector3(name, value);

    public void SetMatrix(string name, Matrix4x4 value) => currentShader?.SetMatrix(name, value);

    public void SetTexture(string name, int offset, TextureProxy texture) => currentShader?.SetTexture(name, offset, texture);
}
