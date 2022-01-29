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
            TmpAddChildern = new List<GameObject>();
            TmpDelChildern = new List<GameObject>();
            Components = new List<Component>();
            DelComponents = new List<Component>();
            AddComponents = new List<Component>();
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
        private List<Component> Components { get; set; }
        private List<Component> DelComponents { get; set; }
        private List<Component> AddComponents { get; set; }


        public void AddComponent(Component com)
        {
            AddComponents.Add(com);
        }


        public void RemoveComponent(Component com)
        {
            DelComponents.Add(com);
        }


        public delegate void ComponentForeachDelegate(Component obj);

        public void ForeachComponent(ComponentForeachDelegate action)
        {
            foreach (var child in Components)
            {
                if (child != null)
                {
                    action(child);
                }
            }
        }


        // 相对父级旋转
        public Quaternion LocalRotation { get; set; }

        // 相对父级缩放
        public Vector3 LocalScale { get; set; }

        // 子节点
        protected List<GameObject> Childern;
        // 子节点
        protected List<GameObject> TmpAddChildern;
        protected List<GameObject> TmpDelChildern;

        public int ChildernCount { get => Childern.Count; }

        public delegate void GameObjectForeachDelegate(GameObject obj);

        public void Foreach(GameObjectForeachDelegate action)
        {
            foreach(var child in Childern)
            {
                if(child != null)
                {
                    action(child);
                }
            }
        }

        // 父节点
        public GameObject? Parent {
            get => _Parent;
            set {
                if (value == _Parent)
                    return;
                if (_Parent != null)
                {
                    _Parent.TmpDelChildern.Add(this);
                    _Parent = null;
                }
                _Parent = value;
                if (_Parent != null)
                {
                    _Parent.TmpAddChildern.Add(this);
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
            foreach (var addChild in TmpAddChildern)
            {
                Childern.Add(addChild);
            }
            foreach (var delChild in TmpDelChildern)
            {
                Childern.Remove(delChild);
            }
            TmpAddChildern.Clear();
            TmpDelChildern.Clear();

            foreach(var com in Components)
            {
                com.Tick();
            }

            foreach(var com in AddComponents)
            {

                Components.Add(com);
            }
            foreach (var com in DelComponents)
            {
                Components.Remove(com);
            }

            AddComponents.Clear();
            DelComponents.Clear();
        }

    }

}
