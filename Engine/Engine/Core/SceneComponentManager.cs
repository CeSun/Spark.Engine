using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Core;

public class SceneComponentManager
{
    private Dictionary<string, List<SceneComponent>> ComponentMap = new Dictionary<string, List<SceneComponent>>();
    private List<SceneComponent> AddComponents = new List<SceneComponent>();
    private List<SceneComponent> DeleteComponents = new List<SceneComponent>();
    public void RegistComponent(SceneComponent component)
    {
        var TypeName = component.GetType().FullName;
        if (TypeName == null)
        {
            throw new Exception("类型名字为空");
        }
        AddComponents.Add(component);
    }

    public List<T>? GetComponent<T>() where T : SceneComponent
    {
        var typeName = typeof(T).FullName;
        if (typeName == null)
        {
            throw new Exception("类型名字为空");
        }
        if (!ComponentMap.TryGetValue(typeName, out var list))
        {
            return null;
        }
        if (list == null)
        {
            return null;
        }
        var TypeList = new List<T>();
        foreach (var component in list)
        {
            TypeList.Add((T)(component));
        }
        foreach (var component in AddComponents)
        {
            if (component is T NewComponent)
            {
                TypeList.Add(NewComponent);
            }
        }
        return TypeList;
    }

    public void UnregistComponent(SceneComponent component)
    {
        var TypeName = component.GetType().FullName;
        if (TypeName == null)
        {
            throw new Exception("类型名字为空");
        }
        DeleteComponents.Add(component);
    }
    public void Render(double DeltaTime)
    {
        foreach(var kv in ComponentMap)
        {
            foreach(var component in kv.Value)
            {
                component.Render(DeltaTime);
            }
        }
    }

    public void Tick(double DeltaTime)
    {
        foreach (var kv in ComponentMap)
        {
            foreach (var component in kv.Value)
            {
                component.Tick(DeltaTime);
            }
        }

        // 添加
        foreach(var component in AddComponents)
        {
            
            var TypeName = component.GetType().FullName;
            if (TypeName == null)
            {
                throw new Exception("类型名字为空");
            }
            List<SceneComponent>? list = null;
            if (!ComponentMap.TryGetValue(TypeName, out list))
            {
                list = new List<SceneComponent>();
            }
            if (list == null)
            {
                throw new Exception("列表初始化失败");
            }
            list.Add(component);
            ComponentMap[TypeName] = list;
        }
        foreach(var Component in DeleteComponents)
        {
            var TypeName = Component.GetType().FullName;
            if (TypeName == null)
            {
                throw new Exception("类型名字为空");
            }
            List<SceneComponent>? list = null;
            if (!ComponentMap.TryGetValue(TypeName, out list))
            {
                continue;
            }
            if (list == null)
            {
                continue;
            }
            list.Remove(Component);
        }

    }
}
