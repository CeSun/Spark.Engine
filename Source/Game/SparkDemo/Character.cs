using SharpGLTF.Schema2;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Components;
using Spark.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SparkDemo
{
    public class Character : Actor
    {
        public StaticMeshComponent Mesh { get; set; }

        public CameraComponent Camera { get; set; }
        public Character(Level level, string Name = "") : base(level, Name)
        {
            Mesh = new StaticMeshComponent(this);
            Mesh.StaticMesh = StaticMesh.LoadFromGLB("/StaticMesh/Soldier.glb");
            Mesh.IsStatic = true;
            RootComponent = Mesh;


            Camera = new CameraComponent(this);

            Camera.RelativeLocation = Vector3.Zero + Camera.UpVector * 10;

            Camera.RelativeRotation = Quaternion.CreateFromYawPitchRoll(0, -90F.DegreeToRadians(), 0);
            Camera.ParentComponent = Mesh;
        }


    }
}
