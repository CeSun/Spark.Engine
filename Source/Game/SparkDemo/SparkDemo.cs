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
            /*
            var lightc = new PointLightComponent(lightActor);
            lightc.LightStrength = 1F;
            lightc.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, -45f.DegreeToRadians(), 0);
            lightActor.RootComponent = lightc;
            lightc.Color = Color.White;
            lightc.WorldLocation = new Vector3(1, 4, 0);


            var lightc2 = new DirectionLightComponent(lightActor);
            lightc2.LightStrength = 1F;
            lightc2.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, -45f.DegreeToRadians(), 0);
            lightActor.RootComponent = lightc2;
            lightc2.Color = Color.White;
            lightc2.WorldLocation = new Vector3(1, 4, 0);
            

           for(int i = 0; i < 4; i++)
            {
                var lightc3 = new PointLightComponent(lightActor);
                lightc3.LightStrength = 1F;
                lightc3.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, -90f.DegreeToRadians(), 0);
                lightActor.RootComponent = lightc3;
                lightc3.Color = Color.White;
                lightc3.WorldLocation = new Vector3(2 + i, 3, -2 + i);
            }
            */
            var lightc1 = new DirectionLightComponent(lightActor);
            lightc1.LightStrength = 1.5F;
            lightc1.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, -45f.DegreeToRadians(), 0);
            lightActor.RootComponent = lightc1;
            lightc1.Color = Color.White;

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
            camera.WorldLocation = new Vector3(2, 3, -2);
        
            StaticMeshActor sma = new StaticMeshActor(level);
            sma.WorldScale = new Vector3(1, 1, 1);
            sma.WorldLocation = new Vector3(2, 2, 0);
            sma.IsStatic = true;
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
