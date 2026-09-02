using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Resources;
using Spark.Engine.Worlds;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class SceneHierarchyTests
{
    [Fact]
    public void RelativeTransformsComposeThroughParentChain()
    {
        var parent = new SceneComponent { RelativeLocation = new Vector3(10, 0, 0) };
        var child = new SceneComponent { RelativeLocation = new Vector3(2, 0, 0) };
        var grandChild = new SceneComponent { RelativeLocation = new Vector3(3, 0, 0) };

        child.SetupAttachment(parent);
        grandChild.SetupAttachment(child);

        Assert.Equal(new Vector3(15, 0, 0), grandChild.WorldTransform.Translation);
        Assert.Contains(child, parent.AttachChildren);
        Assert.Same(parent, child.AttachParent);
    }

    [Fact]
    public void ParentRotationAffectsChildWorldPosition()
    {
        var parent = new SceneComponent { RelativeRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f) };
        var child = new SceneComponent { RelativeLocation = Vector3.UnitX };
        child.SetupAttachment(parent);

        Assert.True(Vector3.Distance(child.WorldTransform.Translation, new Vector3(0, 0, -1)) < 0.0001f);
    }

    [Fact]
    public void SocketIsIncludedInWorldTransform()
    {
        var parent = new SceneComponent { RelativeLocation = new Vector3(10, 0, 0) };
        parent.DefineSocket("Grip", Matrix4x4.CreateTranslation(5, 0, 0));
        var child = new SceneComponent { RelativeLocation = new Vector3(2, 0, 0) };

        child.SetupAttachment(parent, "Grip");

        Assert.Equal(new Vector3(17, 0, 0), child.WorldTransform.Translation);
        Assert.Equal(new Vector3(15, 0, 0), parent.GetSocketTransform("Grip").Translation);
    }

    [Fact]
    public void KeepWorldAndSnapRulesHaveExpectedPositions()
    {
        var parent = new SceneComponent { RelativeLocation = new Vector3(10, 0, 0) };
        var child = new SceneComponent { RelativeLocation = new Vector3(2, 0, 0) };

        Assert.True(child.AttachToComponent(parent, AttachmentTransformRules.KeepWorldTransform));
        Assert.Equal(new Vector3(2, 0, 0), child.WorldTransform.Translation);
        Assert.Equal(new Vector3(-8, 0, 0), child.RelativeLocation);

        Assert.True(child.AttachToComponent(parent, AttachmentTransformRules.SnapToTargetIncludingScale));
        Assert.Equal(Vector3.Zero, child.RelativeLocation);
        Assert.Equal(new Vector3(10, 0, 0), child.WorldTransform.Translation);
    }

    [Fact]
    public void InvalidAttachmentsDoNotChangeExistingParent()
    {
        var first = new SceneComponent();
        var second = new SceneComponent();
        var child = new SceneComponent();
        child.SetupAttachment(first);

        Assert.Throws<InvalidOperationException>(() => first.AttachToComponent(child, AttachmentTransformRules.KeepRelativeTransform));
        Assert.Throws<KeyNotFoundException>(() => child.AttachToComponent(second, AttachmentTransformRules.KeepRelativeTransform, "Missing"));
        Assert.Same(first, child.AttachParent);
        Assert.Contains(child, first.AttachChildren);
        Assert.DoesNotContain(child, second.AttachChildren);
    }

    [Fact]
    public void ActorsHaveRootComponentAndRejectCrossWorldAttachment()
    {
        var firstWorld = new World(new ResourceManager());
        var secondWorld = new World(new ResourceManager());
        var firstActor = new Actor();
        var secondActor = new Actor();
        var firstComponent = new SceneComponent();
        var secondComponent = new SceneComponent();
        firstActor.AddOwnedComponent(firstComponent);
        secondActor.AddOwnedComponent(secondComponent);
        firstWorld.AddActor(firstActor);
        secondWorld.AddActor(secondActor);
        firstWorld.Update(0.016f);
        secondWorld.Update(0.016f);

        Assert.Same(firstComponent, firstActor.RootComponent);
        Assert.Throws<InvalidOperationException>(() => firstComponent.AttachToComponent(secondComponent, AttachmentTransformRules.KeepRelativeTransform));
    }

    [Fact]
    public void EnteringWorldRejectsAttachmentCreatedBeforeWorldAssignment()
    {
        var firstWorld = new World(new ResourceManager());
        var secondWorld = new World(new ResourceManager());
        var parentActor = new Actor();
        var childActor = new Actor();
        var parent = new SceneComponent();
        var child = new SceneComponent();
        parentActor.AddOwnedComponent(parent);
        childActor.AddOwnedComponent(child);
        child.SetupAttachment(parent);

        firstWorld.AddActor(parentActor);
        firstWorld.Update(0.016f);
        Assert.Throws<InvalidOperationException>(() => secondWorld.AddActor(childActor));
        Assert.Empty(secondWorld.Actors);
        Assert.Null(childActor.World);
    }

    [Fact]
    public void TransformChangesPropagateDirtyToDescendants()
    {
        var parent = new SceneComponent();
        var child = new SceneComponent();
        child.SetupAttachment(parent);
        parent.ClearTransformDirty();
        child.ClearTransformDirty();

        parent.RelativeLocation = new Vector3(1, 0, 0);

        Assert.True(parent.IsTransformDirty);
        Assert.True(child.IsTransformDirty);
    }

    [Fact]
    public void RemovingParentActorDetachesExternalChildAndKeepsWorldTransform()
    {
        using var world = new World(new ResourceManager());
        var parentActor = new Actor();
        var parent = new SceneComponent { RelativeLocation = new Vector3(10f, 0f, 0f) };
        parentActor.AddOwnedComponent(parent);
        var childActor = new Actor();
        var child = new SceneComponent { RelativeLocation = new Vector3(2f, 0f, 0f) };
        childActor.AddOwnedComponent(child);
        child.SetupAttachment(parent);
        world.AddActor(parentActor);
        world.AddActor(childActor);
        world.Update(0.016f, tickActors: false);

        world.RemoveActor(parentActor);

        Assert.Null(child.AttachParent);
        Assert.DoesNotContain(child, parent.AttachChildren);
        Assert.Equal(new Vector3(12f, 0f, 0f), child.WorldTransform.Translation);
        Assert.Same(world, childActor.World);
    }

    [Fact]
    public void CancellingParentActorRemovalRestoresExternalAttachment()
    {
        using var world = new World(new ResourceManager());
        var parentActor = new Actor();
        var parent = new SceneComponent { RelativeLocation = new Vector3(10f, 0f, 0f) };
        parent.DefineSocket("Mount", Matrix4x4.CreateTranslation(3f, 0f, 0f));
        parentActor.AddOwnedComponent(parent);
        var childActor = new Actor();
        var child = new SceneComponent { RelativeLocation = new Vector3(2f, 0f, 0f) };
        childActor.AddOwnedComponent(child);
        child.SetupAttachment(parent, "Mount");
        world.AddActor(parentActor);
        world.AddActor(childActor);
        world.Update(0.016f, tickActors: false);

        world.RemoveActor(parentActor);
        world.AddActor(parentActor);

        Assert.Same(parent, child.AttachParent);
        Assert.Equal("Mount", child.AttachSocketName);
        Assert.Equal(new Vector3(2f, 0f, 0f), child.RelativeLocation);
        Assert.Equal(new Vector3(15f, 0f, 0f), child.WorldTransform.Translation);
    }

    [Fact]
    public void RemovingChildActorClearsExternalParentChildren()
    {
        using var world = new World(new ResourceManager());
        var parentActor = new Actor();
        var parent = new SceneComponent();
        parentActor.AddOwnedComponent(parent);
        var childActor = new Actor();
        var child = new SceneComponent();
        childActor.AddOwnedComponent(child);
        child.SetupAttachment(parent);
        world.AddActor(parentActor);
        world.AddActor(childActor);
        world.Update(0.016f, tickActors: false);

        world.RemoveActor(childActor);

        Assert.Empty(parent.AttachChildren);
        Assert.Null(child.AttachParent);
    }
}
