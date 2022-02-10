using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteEngine.Core.Components;

namespace LiteEngine.Core.GameObjects
{
    public class GameObject
    {
        public GameObject()
        {
            _Childern = new List<GameObject>();
        }
        public static double DeltaTime { get; set; }

        public GameObject? Parent { get => _Parent; set => value = _Parent; }

        private GameObject? _Parent;
        private List<GameObject> _Childern;


        public GameObject GetChild(int index)
        {
            return _Childern[index];
        }

        public int ChildrenCount { get=>_Childern.Count; }


        public void Update()
        {
            OnUpdate();
        }

        public void FixedUpdate()
        {
            OnFixedUpdate();
        }

        protected virtual void OnUpdate()
        {

        }

        protected virtual void OnFixedUpdate()
        {

        }
        public virtual void AddChildern(GameObject gameObject)
        {
            if (_Childern.Contains(gameObject))
                throw new Exception("已拥有该对象");
            if (gameObject._Parent != null)
            {
                // 触发事件，被移除
                _Parent._Childern.Remove(gameObject);
                gameObject.Parent = null;
            }

            // 触发事件，被添加
            _Childern.Add(gameObject);
            gameObject._Parent = this;

        }


        public virtual void RemoveChildern(GameObject gameObject)
        {
            if (!_Childern.Contains(gameObject))
                throw new Exception("未拥有该对象");
            // 触发事件，被移除
            gameObject._Parent = null;
            this._Childern.Remove(gameObject);
        }

        public virtual void AddComponent(Component component)
        {

        }

        public virtual void RemoveComponent(Component component)
        {

        }

    }
}
