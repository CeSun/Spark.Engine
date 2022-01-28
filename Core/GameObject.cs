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

        public string Name { get; set; }
        static int objIdIter = 0;
        public GameObject(string name)
        {
            var id = objIdIter++;
            Childern = new List<GameObject>();
            Components = new List<Component>();
            LocalScale = Vector3.One;
            LocalRotation = Quaternion.FromEulerAngles(0, 0, 0);
            Parent = null;
            Name =  $"{name}(obj{id})";
            Layer = RenderLayer.Layer1;
        }

        public RenderLayer Layer { get; set; }


        // 相对父级位置
        public Vector3 LocalPosition { get; set; }

        // 组件们
        public List<Component> Components { get; set; }

        // 相对父级旋转
        public Quaternion LocalRotation { get; set; }

        // 相对父级缩放
        public Vector3 LocalScale { get; set; }

        // 子节点
        public List<GameObject> Childern;

        // 父节点
        public GameObject? Parent {
            get => _Parent;
            set {
                if (value == _Parent)
                    return;
                if (_Parent != null)
                {
                    _Parent.Childern.Remove(this);
                    _Parent = null;
                }
                _Parent = value;
                if (_Parent != null)
                {
                    _Parent.Childern.Add(this);
                }
            }
        }


        public GameObject? _Parent;

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
            Components.ForEach(com => { 
                if (com is RenderComponent component)
                {
                    component.Draw(delta);
                }
            });
        }

        public virtual void Tick()
        {
            Components.ForEach(com => com.Tick());
        }

    }

}
