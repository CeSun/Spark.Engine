namespace Spark.Engine.Render;

public interface IRenderer
{
    void Render(double DeltaTime);

    public RenderTarget CreateRenderTarget(int width, int height, uint GbufferNums);

    public RenderTarget CreateRenderTarget(int width, int height);

    public Shader CreateShader(string Path, List<string> Macros);

}
