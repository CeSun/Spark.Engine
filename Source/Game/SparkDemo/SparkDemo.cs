﻿using SharpGLTF.Schema2;
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
using System.Net.Http.Headers;

namespace SparkDemo
{
    public class SparkDemo
    {
        public static void BeginPlay(Level level)
        {

            var texture = TextureHDR.LoadFromFile("/Texture/newport_loft.hdr", true, true);



            for(int i = 0; i < 16; i ++)
            {
                var PointLightActor = new PointLightActor(level);
                PointLightActor.WorldLocation = new Vector3((-4 + (i % 4)) * 20, 2.5f, (-4 + (i / 4) * 20));
                PointLightActor.Color = Color.White;
                PointLightActor.PointLightComponent.LightStrength = 1;
                PointLightActor.PointLightComponent.AttenuationRadius = 2.5F;
            }

            var SkyBoxActor = new Actor(level, "SkyBox Actor");
            var skybox = new SkyboxComponent(SkyBoxActor);
            SkyBoxActor.RootComponent = skybox;
            skybox.SkyboxHDR = texture;

            var planeActor = new StaticMeshActor(level);
            planeActor.StaticMesh = StaticMesh.LoadFromGLB("/StaticMesh/cube2.glb");
            planeActor.WorldScale =new Vector3(100, 1, 100);
            planeActor.IsStatic = true;

            /*
            var character = new Character(level);
            character.WorldLocation = new Vector3(3, 100, 4);
            */
            var camera = new MovableCamera(level);
            camera.WorldLocation = new Vector3(0, 4, 0);

            

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
                new Vector3(3, 2, 0),
                new Vector3(2, 2, 2),
                new Vector3(-2, 2, 0),
                new Vector3(-2, 2, 2),
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
                List<StaticMeshActor> List = new List<StaticMeshActor>();
                for (int i = 0; i < 10; i ++)
                {
                    StaticMeshActor sma = new StaticMeshActor(level);
                    sma.WorldScale = Scales[index];
                    sma.WorldLocation = Locations[index] + new Vector3(Random.Shared.Next(-30, 30), Random.Shared.Next(-30, 30), Random.Shared.Next(-30, 30));
                    sma.IsStatic = true;
                    List.Add(sma);
                }

                StaticMesh.LoadFromGLBAsync(name).Then(mesh => List.ForEach(sma => sma.StaticMesh = mesh));
                index++;
            }


            DecalActor decalActor = new DecalActor(level);
            decalActor.Material = new Spark.Engine.Assets.Material()
            {
                BaseColor = Texture.LoadFromFile("/Texture/bear.png")
            };
            decalActor.WorldLocation = new Vector3(0, 1, 0);
            decalActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, 90f.DegreeToRadians(), 0);


        }

        public static void EndPlay(Level level) 
        { 

        }

        List<StaticMeshComponent> RenderStaticMeshes = new List<StaticMeshComponent>();
        public void CameraCulling()
        {
            
        }

    }
}
