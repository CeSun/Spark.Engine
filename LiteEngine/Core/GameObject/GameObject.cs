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
        public ulong Id { get; private set; }
        public GameObject()
        {
            Id = Game.Instance.ObjectId++;
            _Childern = new List<GameObject>();
            Game.Instance.AddGameObject.Add(this);
        }
        ~GameObject()
        {
            Game.Instance.DelGameObject.Add(this);
        }

        /// <summary>
        /// Update间隔时间
        /// </summary>
        public static double DeltaTime { get; set; }

        /// <summary>
        /// FixedUpdate间隔时间
        /// </summary>
        public static double FixedDeltaTime { get; set; }

        /// <summary>
        /// 父对象
        /// </summary>
        public GameObject? Parent { get => _Parent; set => value = _Parent; }

        private GameObject? _Parent;

        private List<GameObject> _Childern;

        /// <summary>
        /// 根据序号获取子对象
        /// </summary>
        /// <param name="index">序号</param>
        /// <returns>子对象</returns>
        public GameObject GetChild(int index)
        {
            return _Childern[index];
        }

        /// <summary>
        /// 子对象的数量
        /// </summary>
        public int ChildrenCount { get=>_Childern.Count; }

        /// <summary>
        /// 渲染更新
        /// </summary>
        public void Update()
        {
            OnUpdate();
        }

        /// <summary>
        /// 逻辑更新
        /// </summary>
        public void FixedUpdate()
        {
            OnFixedUpdate();
        }

        /// <summary>
        /// 子类重写: 渲染更新
        /// </summary>
        protected virtual void OnUpdate()
        {

        }
        /// <summary>
        /// 子类重写: 逻辑更新
        /// </summary>
        protected virtual void OnFixedUpdate()
        {

        }
        /// <summary>
        /// 添加子对象
        /// </summary>
        /// <param name="gameObject">子对象</param>
        /// <exception cref="Exception"></exception>
        public virtual void AddChildern(GameObject gameObject)
        {
            if (_Childern.Contains(gameObject))
                throw new Exception("已拥有该对象");
            if (gameObject._Parent != null)
            {
                // 触发事件，被移除
                gameObject._Parent._Childern.Remove(gameObject);
                gameObject._Parent = null;
            }

            // 触发事件，被添加
            _Childern.Add(gameObject);
            gameObject._Parent = this;

        }

        /// <summary>
        /// 移除子对象
        /// </summary>
        /// <param name="gameObject">子对象</param>
        /// <exception cref="Exception"></exception>
        public virtual void RemoveChildern(GameObject gameObject)
        {
            if (!_Childern.Contains(gameObject))
                throw new Exception("未拥有该对象");
            // 触发事件，被移除
            gameObject._Parent = null;
            this._Childern.Remove(gameObject);
        }


        /// <summary>
        /// 增加组件
        /// </summary>
        /// <param name="component"></param>
        public virtual void AddComponent<T>() where T : Component
        {

        }

        /// <summary>
        /// 移除组件
        /// </summary>
        /// <param name="component"></param>
        public virtual void RemoveComponent(Component component)
        {

        }

    }
}
