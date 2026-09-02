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

    [Fact]
    public void Bookmark_RestoresWorldPoseAndProjectionSettings()
    {
        var camera = CreateCamera(new Vector3(1f, 2f, 5f));
        camera.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0.4f, -0.2f, 0f);
        camera.FieldOfView = 75f;
        camera.NearPlane = 0.25f;
        camera.FarPlane = 2500f;
        var expectedWorld = camera.WorldTransform;
        var controller = new EditorCameraController();

        controller.SetBookmark(3, camera);
        camera.RelativeLocation = new Vector3(-8f, 4f, 2f);
        camera.RelativeRotation = Quaternion.Identity;
        camera.FieldOfView = 40f;
        camera.NearPlane = 1f;
        camera.FarPlane = 100f;

        Assert.True(controller.HasBookmark(3));
        Assert.True(controller.RecallBookmark(3, camera));
        AssertMatrixNear(expectedWorld, camera.WorldTransform);
        Assert.Equal(75f, camera.FieldOfView);
        Assert.Equal(0.25f, camera.NearPlane);
        Assert.Equal(2500f, camera.FarPlane);
    }

    [Fact]
    public void Bookmark_RestoresWorldPoseForAttachedCamera()
    {
        var parent = new SceneComponent
        {
            RelativeLocation = new Vector3(10f, 0f, 0f),
            RelativeRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f),
        };
        var camera = CreateCamera(new Vector3(1f, 2f, 3f));
        camera.SetupAttachment(parent);
        var expectedWorld = camera.WorldTransform;
        var controller = new EditorCameraController();
        controller.SetBookmark(0, camera);
        camera.RelativeLocation = new Vector3(9f, 8f, 7f);
        camera.RelativeRotation = Quaternion.Identity;

        Assert.True(controller.RecallBookmark(0, camera));

        AssertMatrixNear(expectedWorld, camera.WorldTransform);
    }

    [Fact]
    public void Bookmark_EmptyAndInvalidSlotsAreReported()
    {
        var camera = CreateCamera(Vector3.Zero);
        var controller = new EditorCameraController();

        Assert.False(controller.HasBookmark(5));
        Assert.False(controller.RecallBookmark(5, camera));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.SetBookmark(-1, camera));
        Assert.Throws<ArgumentOutOfRangeException>(() => controller.HasBookmark(EditorCameraController.BookmarkCount));
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

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual, float tolerance = 0.001f)
    {
        for (var row = 0; row < 4; row++)
        for (var column = 0; column < 4; column++)
            Assert.InRange(MathF.Abs(expected[row, column] - actual[row, column]), 0f, tolerance);
    }
}
