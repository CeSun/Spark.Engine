namespace Spark.Engine.Render;

public interface IRenderer
{
    void Render(double DeltaTime);

    public RenderTarget CreateRenderTargetByFrameBufferId(int width, int height);

}
