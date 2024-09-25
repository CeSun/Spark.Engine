using Silk.NET.OpenGLES;

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
}
