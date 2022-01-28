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

        Dictionary<int, Camera> mpCamera = new Dictionary<int, Camera>();
        public void Draw(double delta)
        {
            mpCamera.Clear();
            GetCamera(Root);
            var mp = mpCamera.OrderBy(p => p.Key).ToDictionary(p => p.Key, o => o.Value);
            foreach (var (index, camera) in mp)
            {
                camera.DrawScene(delta);
            }
            UI.Draw(delta);
        }

        private void GetCamera(GameObject obj)
        {
            if (obj is Camera)
            {
                var camera = (Camera)obj;
                mpCamera.Add(camera.Index, camera);
            }
            foreach (var o in obj.Childern)
            {
                GetCamera(o);
            }
        }

    }
}
