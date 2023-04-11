using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core.Manager;

public class UpdateManager
{
    private List<Action<double>> UpdateFunctions = new List<Action<double>>();
    private List<Action<double>> AddUpdateFunctions = new List<Action<double>>();
    private List<Action<double>> RemoveUpdateFunctions = new List<Action<double>>();
    public void RegistUpdate(Action<double> TickFunction)
    {
        AddUpdateFunctions.Add(TickFunction);
    }

    public void UnregistUpdate(Action<double> TickFunction)
    {
        RemoveUpdateFunctions.Add(TickFunction);
    }
    public void Update(double DeltaTime)
    {
        UpdateFunctions.AddRange(AddUpdateFunctions);
        AddUpdateFunctions.Clear();
        RemoveUpdateFunctions.ForEach(fun => UpdateFunctions.Remove(fun));
        RemoveUpdateFunctions.Clear();

        foreach (var fun in UpdateFunctions)
        {
            fun.Invoke(DeltaTime);
        }

    }
}
