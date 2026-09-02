using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

/// <summary>
/// Runtime Actor/Component 扩展点。默认按场景保存的类型名创建组件，宿主可注册自定义类型和运行时行为。
/// </summary>
public sealed class RuntimeActorFactory
{
    private readonly Dictionary<string, Func<Actor>> _actorFactories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<ActorComponent>> _componentFactories = new(StringComparer.Ordinal);
    private readonly List<Action<World, SceneDocument>> _worldBehaviors = [];

    public void RegisterActor<T>(string? typeName = null) where T : Actor, new()
    {
        var key = typeName ?? typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name;
        _actorFactories[key] = static () => new T();
        if (typeof(T).FullName is { } fullName)
            _actorFactories[fullName] = static () => new T();
    }

    public void RegisterActor(string typeName, Func<Actor> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(factory);
        _actorFactories[typeName] = factory;
    }

    public void RegisterComponent<T>(string? typeName = null) where T : ActorComponent, new()
    {
        var key = typeName ?? typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name;
        _componentFactories[key] = static () => new T();
        if (typeof(T).FullName is { } fullName)
            _componentFactories[fullName] = static () => new T();
    }

    public void RegisterComponent(string typeName, Func<ActorComponent> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(factory);
        _componentFactories[typeName] = factory;
    }

    /// <summary>注册一次 Play 初始化行为；行为只能作用于新建的 RuntimeWorld。</summary>
    public void RegisterWorldBehavior(Action<World, SceneDocument> behavior)
    {
        ArgumentNullException.ThrowIfNull(behavior);
        _worldBehaviors.Add(behavior);
    }

    internal Actor CreateActor(SceneActorDocument record)
    {
        Actor actor;
        if (_actorFactories.TryGetValue(record.ActorType, out var registered))
        {
            actor = registered();
        }
        else
        {
            var type = Type.GetType(record.ActorType, throwOnError: false);
            if (type == null || !typeof(Actor).IsAssignableFrom(type) || type.IsAbstract)
                throw new InvalidDataException($"Cannot instantiate Actor type '{record.ActorType}'.");
            if (Activator.CreateInstance(type) is not Actor created)
                throw new InvalidDataException($"Actor type '{record.ActorType}' has no public parameterless constructor.");
            actor = created;
        }
        actor.ActorGuid = record.ActorGuid;
        actor.Name = record.Name;
        return actor;
    }

    internal ActorComponent CreateComponent(SceneComponentDocument record)
    {
        if (_componentFactories.TryGetValue(record.ComponentType, out var registered))
            return registered();

        var type = Type.GetType(record.ComponentType, throwOnError: false);
        if (type == null || !typeof(ActorComponent).IsAssignableFrom(type) || type.IsAbstract)
            throw new InvalidDataException($"Cannot instantiate component type '{record.ComponentType}'.");
        if (Activator.CreateInstance(type) is not ActorComponent component)
            throw new InvalidDataException($"Component type '{record.ComponentType}' has no public parameterless constructor.");
        return component;
    }

    internal void InitializeWorld(World world, SceneDocument document)
    {
        foreach (var behavior in _worldBehaviors.ToArray())
            behavior(world, document);
    }
}
