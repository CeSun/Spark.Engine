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
            var lightActor = new Actor(level);


             List<PointLightActor> PointLightActors = new List<PointLightActor>();
             for (int i = 0; i < 2; i++)
             {
                 var PointLight = new PointLightActor(level);
                 PointLight.LightStrength = 2F;
                 PointLight.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, -90f.DegreeToRadians(), 0);
                 PointLight.Color = Color.White;
                 PointLight.WorldLocation = new Vector3(2 + i * 2F, 4,  i * 2F);
                 PointLightActors.Add(PointLight);
             }

             var DirectionLightActor = new DirectionLightActor(level);
             DirectionLightActor.LightStrength = 1F;
             DirectionLightActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, -135f.DegreeToRadians(), 0);
             DirectionLightActor.Color = Color.White;
           
            var SkyBoxActor = new Actor(level, "SkyBox Actor");
            var skybox = new SkyboxComponent(SkyBoxActor);
            SkyBoxActor.RootComponent = skybox;
            TextureCube.LoadAsync("/Skybox/p").Then(texture => skybox.SkyboxCube = texture);

            var planeActor = new StaticMeshActor(level);
            StaticMesh.LoadFromGLBAsync("/StaticMesh/cube2.glb").Then(mesh => planeActor.StaticMesh = mesh);
            planeActor.WorldScale =new Vector3(10, 1, 10);
            planeActor.IsStatic = true;


            var character = new Character(level);
            character.WorldLocation = new Vector3(0, 1, 0);

            var camera = new MovableCamera(level);
            camera.WorldLocation = new Vector3(2, 2, 2);


            StaticMeshActor sma = new StaticMeshActor(level);
            sma.WorldScale = new Vector3(1, 1, 1);
            sma.WorldLocation = new Vector3(2, 2, 0);
            sma.IsStatic = true;
            StaticMesh.LoadFromGLBAsync("/StaticMesh/sphere.glb").Then(mesh => sma.StaticMesh = mesh);
        }


        public static void EndPlay(Level level) 
        { 

        }



    }
}
