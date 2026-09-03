namespace Spark.Engine.Builder;

public class EngineOptions
{
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 600;
    public int TargetFrameRate { get; set; } = 60;
    /// <summary>应用启动时切换到的工作目录；为空时保留宿主当前目录。</summary>
    public string? WorkingDirectory { get; set; }
}
