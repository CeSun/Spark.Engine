using Spark.Core;
using Spark.Core.Actors;
using Spark.Core.Components;
using Spark.Util;
using System.Drawing;

namespace HelloSpark;

public class GameInstance : IGame
{
    CameraActor? CameraActor;
    public void BeginPlay(World world)
    {
        CameraActor = new CameraActor(world)
        {
            WorldLocation = new System.Numerics.Vector3(1, 22, 3),
            WorldRotation = System.Numerics.Quaternion.CreateFromYawPitchRoll(30f.DegreeToRadians(), 1, 1),
            ClearColor = Color.Red,
            ClearFlag = CameraClearFlag.Depth | CameraClearFlag.Color,
            Order = 3
        };

        var DecalActor = new DecalActor(world)
        {
            WorldLocation = new System.Numerics.Vector3(1, 22, 3),
        };
        var DirectionLightActor = new DirectionLightActor(world)
        {
            WorldLocation = new System.Numerics.Vector3(13, 22, 3),
        };
        var PointLightActor = new PointLightActor(world)
        {
            WorldLocation = new System.Numerics.Vector3(12, 22, 33),
        };
        var SpotLightActor = new SpotLightActor(world)
        {
            WorldLocation = new System.Numerics.Vector3(21, 22, 23),
            LightStrength = 10,
            Color = Color.DarkBlue,
        };
        var StaticMeshActor = new StaticMeshActor(world)
        {
            WorldLocation = new System.Numerics.Vector3(1, 22, 3),
        };
    }

    public void EndPlay(World world)
    {
    }

    public void Update(World world, double deltaTime)
    {
        if (CameraActor != null)
        {
            CameraActor.WorldLocation += new System.Numerics.Vector3(0.0001f, 0, 0);
        }
    }
}

