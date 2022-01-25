using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class GameObject
    {
        public GameObject()
        {
            Childern = new List<GameObject>();
            Parent = null;
        }

        // 相对父级位置
        public Vector3 LocalPosition { get; set; }

        // 相对父级旋转
        public Quaternion LocalRotation { get; set; }

        // 相对父级缩放
        public Vector3 LocalScale { get; set; }

        // 子节点
        public List<GameObject> Childern;

        // 父节点
        public GameObject? Parent;

        public Matrix4 Transform { 
            get {
                var rotation = Matrix4.CreateFromQuaternion(LocalRotation);
                var translate = Matrix4.CreateTranslation(LocalPosition);
                var scale = Matrix4.CreateScale(LocalScale);
                var result = rotation * translate * scale;
                if (Parent != null)
                {
                    result = Parent.Transform * result;
                }
                return result;    
            } 
        }

        public virtual void Draw(double delta)
        {

        }

    }

}
