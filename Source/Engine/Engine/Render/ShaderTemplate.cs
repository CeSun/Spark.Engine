

using Silk.NET.OpenGLES;

namespace Spark.Core.Render;

public class ShaderTemplate
{
    public string Name = "";
    public string VertexShaderSource = "";
    public string FragmentShaderSource = "";
    public List<string> IncludeSource = [];
    public Dictionary<string, Shader> ShaderMap = new Dictionary<string, Shader>();

    private string? vertexShaderSource;
    private string? fragmentShaderSource;
    public void Use(GL gl, params List<string> macros)
    {
        var key = string.Join("_", macros!);
        if (ShaderMap.TryGetValue(key, out var shader) == false)
        {
            if (vertexShaderSource == null )
            {
                var macroCode = string.Join("\n", (from m in macros select "#define " + macros).ToList());
                vertexShaderSource = VertexShaderSource.Replace("#version 300 es", "#version 300 es").Replace("//{MacroSourceCode}", macroCode).Replace("//{IncludeSourceCode}", string.Join("\n", IncludeSource));
            }
            if (fragmentShaderSource == null)
            {
                var macroCode = string.Join("\n", (from m in macros select "#define " + macros).ToList());
                fragmentShaderSource = FragmentShaderSource.Replace("#version 300 es", "#version 300 es").Replace("//{MacroSourceCode}", macroCode).Replace("//{IncludeSourceCode}", string.Join("\n", IncludeSource));
            }
            shader = gl.CreateShader(vertexShaderSource, fragmentShaderSource);
            ShaderMap.Add(key, shader);
        }
        shader.Use();
    }

    


}
