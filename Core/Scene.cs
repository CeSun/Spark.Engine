using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class Scene
    {
        public Scene()
        {
            Root = new GameObject("Root");
            UI = new UI();
        }

        // 场景根节点
        public GameObject Root { get; set; }

        public UI UI
        {
            get;
            set;
        }


        // 默认场景
        public static Scene Default
        {
            get => _Default;
        }

        // 当前场景
        public static Scene Current
        {
            get => _Current == null? Default : _Current;
            set
            {
                if (value == null)
                    throw new Exception("不能将当前场景设置为空场景");
                _Current = value;
            }
        }

        private static Scene? _Current;

        private static Scene _Default = new Scene();


        public void Draw(double delta)
        {
            Draw(Root, delta);
            UI?.Draw(delta);
        }

        private static void Draw(GameObject obj, double delta)
        {
            if (obj == null)
                return;
            obj.Draw(delta);
            foreach(var o in obj.Childern)
            {
                Draw(o, delta);
            }
        }

    }
}
