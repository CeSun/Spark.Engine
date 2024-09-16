using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core;

public class UpdateManager
{
    private readonly List<Action<double>> _updateFunctions = [];
    private readonly List<Action<double>> _addUpdateFunctions = [];
    private readonly List<Action<double>> _removeUpdateFunctions = [];
    public void RegisterUpdate(Action<double> tickFunction)
    {
        _addUpdateFunctions.Add(tickFunction);
    }
    public void UnregisterUpdate(Action<double> tickFunction)
    {
        _removeUpdateFunctions.Add(tickFunction);
    }
    public void Update(double deltaTime)
    {
        _updateFunctions.AddRange(_addUpdateFunctions);
        _addUpdateFunctions.Clear();
        _removeUpdateFunctions.ForEach(fun => _updateFunctions.Remove(fun));
        _removeUpdateFunctions.Clear();

        foreach (var fun in _updateFunctions)
        {
            fun.Invoke(deltaTime);
        }

    }
}
