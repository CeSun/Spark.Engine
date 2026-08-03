using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Windowing;

namespace Spark.Engine;

public class EngineApplication
{
    public IView? View { get; private set; }

    public IWindow? Window => View as IWindow;

    public bool IsClosing => View?.IsClosing ?? false;

    public ServiceProvider ServiceProvider { get; private set; }

    public EngineApplication(ServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        View = serviceProvider.GetService<IView>();
    }

    public void Run()
    {
        if (View == null)
        {
            while (IsClosing == false)
            {
                Update(1);
            }
        }
        else
        {
            View.Initialize();
            while (IsClosing == false)
            {
                View.DoEvents();
                Update(1);
            }
            View.Dispose();
        }
    }


    public void Update(float deltaTime)
    {

    }
}
