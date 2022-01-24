using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GlmSharp;

namespace Core
{
    public class GameObject
    {
        public Transform Transform;

        public List<GameObject> Childern = new List<GameObject>();

        public GameObject? Parent;
    }

    public struct Transform
    {
        public vec3 Position { get; set; }
        public quat Rotation { get; set; }
        public vec3 Scale { get; set; }
    }
}
