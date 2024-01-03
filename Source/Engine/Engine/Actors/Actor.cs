using Silk.NET.OpenGLES;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Actors;

public partial class Actor
{
    /// <summary>
    /// Actor所在关卡
    /// </summary>
    public Level CurrentLevel { get; private set; }
    static Dictionary<string, int> NameMap = new Dictionary<string, int>();

    protected virtual bool ReceieveUpdate => false;

    public string Name { get ; private set; }
 
    /// <summary>
    /// Actor所在世界
    /// </summary>
    public World CurrentWorld { get => CurrentLevel.CurrentWorld; }

    public Actor(Level level, string Name = "")
    {
        if (string.IsNullOrEmpty(Name))
        {
            var typeName = GetType().Name;

            if (NameMap.ContainsKey(typeName) == false)
            {
                NameMap[typeName] = 0;
            }
            var index = ++NameMap[typeName] ;

            this.Name = typeName + index;
        }
        else
        {
            this.Name = Name;
        }
        CurrentLevel = level;
        level.RegistActor(this);
        CurrentLevel.Engine.NextFrame.Add(BeginPlay);
        if (ReceieveUpdate == true)
        {
            CurrentLevel.UpdateManager.RegistUpdate(Update);
        }
    }

    public void BeginPlay()
    {
        IsBegined = true;
        OnBeginPlay();
    }
    private bool IsBegined = false;
    public void Update(double DeltaTime)
    {
        if (IsBegined == false)
            return;
        OnUpdate(DeltaTime);
    }

    protected virtual void OnUpdate(double DeltaTime)
    {

    }
    protected virtual void OnBeginPlay()
    {

    }
    public void Destory()
    {
        OnEndPlay();
        foreach (var component in PrimitiveComponents.ToList())
        {
            UnregistComponent(component);
        }
        if (ReceieveUpdate)
        {
            CurrentLevel.UpdateManager.UnregistUpdate(Update);
        }
        CurrentLevel.UnregistActor(this);
    }

    protected virtual void OnEndPlay()
    {

    }

    public Vector3 ForwardVector => Vector3.Transform(new Vector3(0, 0, -1), WorldRotation);
    public Vector3 RightVector => Vector3.Transform(new Vector3(1, 0, 0), WorldRotation);
    public Vector3 UpVector => Vector3.Transform(new Vector3(0, 1, 0), WorldRotation);

}


[ActorInfo(Group = "Base")]
public partial class Actor
{
    public PrimitiveComponent? RootComponent;

    public Vector3 WorldLocation
    {
        get 
        {
            if (RootComponent == null)
                return Vector3.Zero;
            return RootComponent.WorldLocation;
        }
        set
        {
            if (RootComponent == null)
                return;
            RootComponent.WorldLocation = value;
        }
    }
    public Quaternion WorldRotation
    {
        get
        {
            if (RootComponent == null)
                return Quaternion.Zero;
            return RootComponent.WorldRotation;
        }
        set
        {
            if (RootComponent == null)
                return;
            RootComponent.WorldRotation = value;
        }
    }
    public Vector3 WorldScale
    {
        get
        {
            if (RootComponent == null)
                return Vector3.One;
            return RootComponent.WorldScale;
        }
        set
        {
            if (RootComponent == null)
                return;
            RootComponent.WorldScale = value;
        }
    }



}

/// <summary>
/// Component
/// </summary>
public partial class Actor : ISerializable
{
    List<PrimitiveComponent> _PrimitiveComponents = new List<PrimitiveComponent>();
    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents { get { return _PrimitiveComponents; } }

    /// <summary>
    /// 向Actor上注册组件
    /// </summary>
    /// <param name="Component"></param>
    public void RegistComponent(PrimitiveComponent Component)
    {
        if (PrimitiveComponents.Contains(Component))
        {
            return;
        }
        _PrimitiveComponents.Add(Component);
        CurrentLevel.RegistComponent(Component);
        foreach (var SubComponent in Component.ChildrenComponent)
        {
            if (PrimitiveComponents.Contains(SubComponent))
            {
                continue;
            }
            _PrimitiveComponents.Add(SubComponent);
            CurrentLevel.RegistComponent(SubComponent);
        }
    }

    /// <summary>
    /// 从Actor上注销组件
    /// </summary>
    /// <param name="Component"></param>
    public void UnregistComponent(PrimitiveComponent Component)
    {
        if (!PrimitiveComponents.Contains(Component))
        {
            return;
        }
        _PrimitiveComponents.Remove(Component);
        CurrentLevel.UnregistComponent(Component);
        foreach (var SubComponent in Component.ChildrenComponent)
        {
            if (!PrimitiveComponents.Contains(SubComponent))
            {
                continue;
            }
            _PrimitiveComponents.Remove(SubComponent);
            CurrentLevel.UnregistComponent(SubComponent);
        }
    }


    public T? GetComponent<T>() where T : PrimitiveComponent
    {
        foreach(var comp in PrimitiveComponents)
        {
            if (comp is T c)
            {
                return c;
            }
        }
        return null;
    }

    public List<T> GetComponents<T>() where T : PrimitiveComponent
    {
        var list = new List<T>();
        foreach(var comp in PrimitiveComponents)
        {
            if (comp is T c)
            {
                list.Add(c);
            }
        }
        return list;
    }

    public void Serialize(BinaryWriter bw, Engine engine)
    {
        bw.WriteString2(GetType().FullName);
        bw.WriteString2(Name);
        ISerializable.ReflectionSerialize(this, bw, engine);
        bw.WriteInt32(PrimitiveComponents.Count);
        foreach(var component in  PrimitiveComponents)
        {
            ISerializable.ReflectionSerialize(component, bw, engine);
        }
    }

    public void Deserialize(BinaryReader br, Engine engine)
    {
        Name = br.ReadString2();
        ISerializable.ReflectionDeserialize(this, br, engine);
        var cmpNums = br.ReadInt32();
        if (cmpNums != PrimitiveComponents.Count)
            throw new Exception("");
        foreach(var component in PrimitiveComponents)
        {
            ISerializable.ReflectionDeserialize(component, br, engine);
        }

    }
}
