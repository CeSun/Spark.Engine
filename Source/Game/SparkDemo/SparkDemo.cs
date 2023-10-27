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

namespace SparkDemo
{
    public class SparkDemo
    {
        public static void BeginPlay(Level level)
        {
            var lightActor = new Actor(level);

            var lightc = new DirectionLightComponent(lightActor);
            lightc.LightStrength = 1;
            lightc.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, -90, 0);
            lightActor.RootComponent = lightc;
            lightc.Color = Color.White;

            var SkyBoxActor = new Actor(level, "SkyBox Actor");
            var skybox = new SkyboxComponent(SkyBoxActor);
            SkyBoxActor.RootComponent = skybox;
            TextureCube.LoadAsync("/Skybox/pm").Then(res => {
                skybox.SkyboxCube = res;
            });

            var character = new Character(level);

        }


        public static void EndPlay(Level level) 
        { 

        }



    }
}
