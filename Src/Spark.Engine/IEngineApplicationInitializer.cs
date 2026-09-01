namespace Spark.Engine;

/// <summary>
/// 通过 <see cref="Builder.EngineBuilder"/> 注册的应用初始化器。
/// 在主窗口和游戏内容初始化完成后、渲染线程启动前执行。
/// </summary>
public interface IEngineApplicationInitializer
{
    void Initialize(EngineApplication application);
}
