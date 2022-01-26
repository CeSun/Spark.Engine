using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Mathematics;

namespace LiteEngine.Core
{
    public class Camera : GameObject
    {
        public static Camera Current { get => _camera; }
        private static Camera _camera = new Camera();

        public Camera()
        {
            Nearest = 0.01f;
            Furthest = 100.0f;
            Fov = (float)(Math.PI / 180f * 75f);
            Parent = Scene.Current.Root;
        }
        public Matrix4 ViewMat
        {
            get
            {
                var transform = Transform;
                var Up = new Vector4 { X = 0, Y = 1, Z = 0, W = 1 } * transform ;
                var Target =new Vector4 { X = 0, Y = 0, Z = 1, W = 1 } * transform;
                var Eye = new Vector4 { X = 0, Y = 0, Z = 0, W = 1 } * transform;
                return Matrix4.LookAt(new Vector3 { X = Eye.X, Y = Eye.Y, Z = Eye.Z }, new Vector3 { X = Target.X, Y = Target.Y, Z = Target.Z }, new Vector3 { X = Up.X, Y = Up.Y, Z = Up.Z });
            }
        }

        public Matrix4 PerspectiveMat
        {
            get
            {
                return Matrix4.CreatePerspectiveFieldOfView(Fov, 800.0f/600, Nearest, Furthest);
            }
        }


        public float Nearest { get; set; }
        public float Furthest { get; set; }
        public float Fov { get; set; }
    }
}
