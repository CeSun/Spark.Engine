using SharpGLTF.Schema2;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Spark.Util;
using System.Drawing;
using Silk.NET.Input;

namespace SparkDemo
{
    public class SparkDemo
    {
        public static void BeginPlay(Level level)
        {
            float angle = 0f;
            var lightActor = new Actor(level);

            var lightc = new DirectionLightComponent(lightActor);
            lightc.LightStrength = 1F;
            lightc.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, -45f.DegreeToRadians(), 0);
            lightActor.RootComponent = lightc;
            lightc.Color = Color.White;

            var SkyBoxActor = new Actor(level, "SkyBox Actor");
            var skybox = new SkyboxComponent(SkyBoxActor);
            SkyBoxActor.RootComponent = skybox;
            skybox.SkyboxCube = TextureCube.Load("/Skybox/pm");


            var planeActor = new Actor(level);
            var planeComp = new StaticMeshComponent(planeActor);
            StaticMesh.LoadFromGLBAsync("/StaticMesh/cube2.glb").Then(mesh => planeComp.StaticMesh = mesh);
            planeComp.WorldScale =new Vector3(10, 1, 10);
            planeComp.IsStatic = true;

            var character = new Character(level);
            character.WorldLocation = new Vector3(0, 1, 0);
            var camera = new CameraActor(level);

            level.Engine.MainKeyBoard.KeyDown += (keyboard, key, _) =>
            {
                if (key == Key.Up)
                {
                    angle++;
                    lightc.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, angle.DegreeToRadians(), 0);
                }
                if (key == Key.Down)
                {
                    angle--;
                    lightc.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, angle.DegreeToRadians(), 0);
                }
            };

            StaticMeshActor sma = new StaticMeshActor(level);
            sma.WorldScale = new Vector3(1, 1, 1);
            sma.WorldLocation = new Vector3(0, 2, 0);
            StaticMesh.LoadFromGLBAsync("/StaticMesh/sphere.glb").Then(mesh =>
            {
                sma.StaticMesh = mesh;
            });
        }


        public static void EndPlay(Level level) 
        { 

        }



    }
}
