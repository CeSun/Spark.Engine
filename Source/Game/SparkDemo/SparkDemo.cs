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
using Texture = Spark.Engine.Assets.Texture;
using System.Xml.Linq;
using System.Collections;
using System.Reflection.Emit;
using System.Runtime.Intrinsics.X86;

namespace SparkDemo
{
    public class SparkDemo
    {
        public static void BeginPlay(Level level)
        {

            var texture = TextureHDR.LoadFromFile("/Texture/newport_loft.hdr");


            var lightActor = new Actor(level);

            var DirectionLightActor = new DirectionLightActor(level);
            DirectionLightActor.LightStrength = 1F;
            DirectionLightActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, -45f.DegreeToRadians(), 0);
            DirectionLightActor.Color = Color.White;
           
            var SkyBoxActor = new Actor(level, "SkyBox Actor");
            var skybox = new SkyboxComponent(SkyBoxActor);
            SkyBoxActor.RootComponent = skybox;
            skybox.SkyboxHDR = texture;

            var planeActor = new StaticMeshActor(level);
            StaticMesh.LoadFromGLBAsync("/StaticMesh/cube2.glb").Then(mesh => planeActor.StaticMesh = mesh);
            planeActor.WorldScale =new Vector3(10, 1, 10);
            planeActor.IsStatic = true;


            var character = new Character(level);
            character.WorldLocation = new Vector3(0, 100, 0);

            var camera = new MovableCamera(level);
            camera.WorldLocation = new Vector3(2, 4, 1);

            

            List<string> Models = new List<string>
            {
                "/StaticMesh/sphere.glb",
                "/StaticMesh/sofa.glb",
                "/StaticMesh/chair.glb",
                "/StaticMesh/barrel_stove.glb",
                "/StaticMesh/concrete_cat_statue.glb",
                "/StaticMesh/old_tyre.glb",
                "/StaticMesh/cardboard_box.glb",

            };
            List<Vector3> Locations = new List<Vector3>
            {
                new Vector3(0, 2, 0),
                new Vector3(3, 1, 0),
                new Vector3(2, 1, 2),
                new Vector3(-2, 1, 0),
                new Vector3(-2, 1, 2),
                new Vector3(4, 2, 2),
                new Vector3(0, 2, 4),
            };

            List<Vector3> Scales = new List<Vector3>
            {
                new Vector3(1, 1, 1),
                new Vector3(1, 1, 1),
                new Vector3(1, 1, 1),
                new Vector3(2, 2, 2),
                new Vector3(4, 4, 4),
                new Vector3(2, 2, 2),
                new Vector3(2, 2, 2),
            };
            int index = 0;
            foreach (var name in Models)
            {
                StaticMeshActor sma = new StaticMeshActor(level);
                sma.WorldScale = Scales[index];
                sma.WorldLocation = Locations[index++] ;
                sma.IsStatic = false;
                StaticMesh.LoadFromGLBAsync(name).Then(mesh => sma.StaticMesh = mesh);

            }
            

        }

        public static void EndPlay(Level level) 
        { 

        }



    }
}
