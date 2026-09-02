using System.Numerics;
using Spark.Engine.Actors;
using Spark.Engine.Components;
using Spark.Engine.Editor;
using Spark.Engine.Input;
using Xunit;

namespace Spark.Engine.Tests;

public sealed class EditorCameraTests
{
    [Fact]
    public void FlyNavigation_LooksAndMovesWhileRightMouseIsHeld()
    {
        var camera = CreateCamera(new Vector3(0f, 0f, 5f));
        var controller = new EditorCameraController();
        var right = ButtonMask(MouseButton.Right);
        var keys = CreateKeyMask(Key.W);

        controller.Update(camera, Input(new Vector2(10f, 10f), right, right, default, keys), 0.1f, null);
        var firstPosition = camera.WorldTransform.Translation;
        controller.Update(camera, Input(new Vector2(30f, 0f), right, default, default, keys), 0.1f, null);

        Assert.Equal(EditorCameraNavigationMode.Fly, controller.Mode);
        Assert.True(firstPosition.Z < 5f);
        Assert.NotEqual(Quaternion.Identity, camera.RelativeRotation);

        controller.Update(camera, Input(new Vector2(30f, 0f), default, default, right, default), 0.1f, null);
        Assert.Equal(EditorCameraNavigationMode.None, controller.Mode);
    }

    [Fact]
    public void AltLeftOrbit_PreservesDistanceToSelectionPivot()
    {
        var camera = CreateCamera(new Vector3(0f, 0f, 5f));
        var controller = new EditorCameraController();
        var left = ButtonMask(MouseButton.Left);
        var alt = CreateKeyMask(Key.LeftAlt);

        controller.Update(camera, Input(Vector2.Zero, left, left, default, alt), 0.016f, Vector3.Zero);
        controller.Update(camera, Input(new Vector2(100f, 20f), left, default, default, alt), 0.016f, Vector3.Zero);

        var position = camera.WorldTransform.Translation;
        Assert.Equal(EditorCameraNavigationMode.Orbit, controller.Mode);
        Assert.InRange(Vector3.Distance(position, Vector3.Zero), 4.999f, 5.001f);
        Assert.True(MathF.Abs(position.X) > 0.1f);
    }

    [Fact]
    public void Focus_FramesAllSelectedSpatialTargets()
    {
        var camera = CreateCamera(new Vector3(0f, 0f, 5f));
        var first = new SceneComponent { RelativeLocation = new Vector3(-2f, 0f, 0f) };
        var second = new SceneComponent { RelativeLocation = new Vector3(2f, 0f, 0f) };
        var controller = new EditorCameraController();

        Assert.True(controller.Focus(camera, new[] { first, second }));

        var position = camera.WorldTransform.Translation;
        var forward = Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, camera.WorldTransform));
        var expectedDirection = Vector3.Normalize(-position);
        Assert.True(Vector3.Dot(forward, expectedDirection) > 0.999f);
        Assert.True(position.Z > 4f);
    }

    private static CameraComponent CreateCamera(Vector3 position)
        => new() { RelativeLocation = position, RelativeRotation = Quaternion.Identity };

    private static InputState Input(
        Vector2 point,
        MouseButtonMask down,
        MouseButtonMask pressed,
        MouseButtonMask released,
        KeyMask keys)
        => new(point, Vector2.Zero, 0f, down, pressed, released, keys, default, default, string.Empty);

    private static MouseButtonMask ButtonMask(MouseButton button)
    {
        var result = default(MouseButtonMask);
        result.Set(button, true);
        return result;
    }

    private static KeyMask CreateKeyMask(Key key)
    {
        var result = default(KeyMask);
        result.Set(key, true);
        return result;
    }
}
