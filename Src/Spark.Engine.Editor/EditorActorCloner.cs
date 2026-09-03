using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;

namespace Spark.Engine.Editor;

public readonly record struct ActorCloneResult(Actor Source, Actor Copy);

/// <summary>按 SceneDocument 持久化边界深复制 Actor 图，并为副本生成全新的稳定身份。</summary>
public static class EditorActorCloner
{
    public static IReadOnlyList<ActorCloneResult> Clone(
        World world,
        IEnumerable<Actor> sources,
        IAssetRegistry assetRegistry,
        RuntimeActorFactory actorFactory)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(assetRegistry);
        ArgumentNullException.ThrowIfNull(actorFactory);

        var sourceActors = sources.Distinct().ToArray();
        if (sourceActors.Length == 0)
            return Array.Empty<ActorCloneResult>();
        var worldActors = world.EnumerateActors(includePendingActors: true).ToArray();
        foreach (var source in sourceActors)
        {
            if (!worldActors.Any(actor => ReferenceEquals(actor, source)))
                throw new InvalidOperationException("Only Actors in the current editor World can be duplicated.");
            if (!EditorActorPolicy.CanDuplicate(source))
                throw new InvalidOperationException($"Actor type '{source.GetType().Name}' cannot be duplicated by the editor.");
            if (Attribute.IsDefined(source.GetType(), typeof(SceneTransientAttribute), inherit: true))
                throw new InvalidOperationException($"Transient Actor type '{source.GetType().Name}' cannot be duplicated.");
        }

        var selectedRecords = sourceActors.Select(SceneDocument.CaptureActor).ToArray();
        var newComponentGuids = selectedRecords
            .SelectMany(actor => actor.Components)
            .ToDictionary(component => component.ComponentGuid, _ => Guid.NewGuid());
        var originalSceneComponents = worldActors
            .SelectMany(actor => actor.Components)
            .OfType<SceneComponent>()
            .ToDictionary(component => component.ComponentGuid);
        var clonedSceneComponents = new Dictionary<Guid, SceneComponent>();
        var results = new List<ActorCloneResult>(sourceActors.Length);

        for (var actorIndex = 0; actorIndex < selectedRecords.Length; actorIndex++)
        {
            var sourceRecord = selectedRecords[actorIndex];
            var cloneRecord = new SceneActorDocument
            {
                ActorGuid = Guid.NewGuid(),
                ActorType = sourceRecord.ActorType,
                Name = sourceRecord.Name,
                RootComponentGuid = sourceRecord.RootComponentGuid is { } rootGuid
                    ? newComponentGuids[rootGuid]
                    : null,
            };
            var clone = actorFactory.CreateActor(cloneRecord);
            if (clone.Components.Any())
                throw new InvalidDataException(
                    $"Actor factory for '{sourceRecord.ActorType}' must return an Actor without pre-created components.");

            foreach (var sourceComponent in sourceRecord.Components)
            {
                var cloneComponentRecord = new SceneComponentDocument
                {
                    ComponentGuid = newComponentGuids[sourceComponent.ComponentGuid],
                    ComponentType = sourceComponent.ComponentType,
                };
                var cloneComponent = actorFactory.CreateComponent(cloneComponentRecord);
                cloneComponent.ComponentGuid = cloneComponentRecord.ComponentGuid;
                ScenePropertySerializer.Restore(cloneComponent, sourceComponent.Properties, ResolveAsset);
                clone.AddOwnedComponent(cloneComponent);
                if (cloneComponent is not SceneComponent cloneScene)
                    continue;
                cloneScene.RelativeLocation = sourceComponent.RelativeLocation;
                cloneScene.RelativeRotation = sourceComponent.RelativeRotation;
                cloneScene.RelativeScale = sourceComponent.RelativeScale;
                foreach (var socket in sourceComponent.Sockets)
                    cloneScene.DefineSocket(socket.Key, socket.Value);
                clonedSceneComponents.Add(sourceComponent.ComponentGuid, cloneScene);
            }

            if (sourceRecord.RootComponentGuid is { } sourceRootGuid &&
                clonedSceneComponents.TryGetValue(sourceRootGuid, out var cloneRoot))
                clone.SetRootComponent(cloneRoot);
            results.Add(new ActorCloneResult(sourceActors[actorIndex], clone));
        }

        foreach (var actorRecord in selectedRecords)
        {
            foreach (var componentRecord in actorRecord.Components)
            {
                if (componentRecord.ParentComponentGuid is not { } parentGuid ||
                    !clonedSceneComponents.TryGetValue(componentRecord.ComponentGuid, out var child))
                    continue;
                var parent = clonedSceneComponents.TryGetValue(parentGuid, out var clonedParent)
                    ? clonedParent
                    : originalSceneComponents.TryGetValue(parentGuid, out var externalParent)
                        ? externalParent
                        : throw new InvalidDataException(
                            $"Component '{componentRecord.ComponentGuid}' references missing parent '{parentGuid}'.");
                child.AttachToComponent(
                    parent, AttachmentTransformRules.KeepRelativeTransform, componentRecord.AttachSocketName);
            }
        }

        return results;

        SceneResource ResolveAsset(Guid assetGuid, Type expectedType)
        {
            var resource = assetRegistry.Resolve(assetGuid);
            if (!expectedType.IsInstanceOfType(resource))
                throw new InvalidDataException(
                    $"Asset '{assetGuid}' is {resource.GetType().Name}, expected {expectedType.Name}.");
            return resource;
        }
    }
}
