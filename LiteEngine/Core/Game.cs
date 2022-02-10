using LiteEngine.Core.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class Game
    {
        internal ulong ObjectId = 0;
        internal Dictionary<ulong, GameObject> GameObjectPool = new Dictionary<ulong, GameObject>();
        internal List<GameObject> AddGameObject = new List<GameObject> ();
        internal List<GameObject> DelGameObject = new List<GameObject>();
        private static  Game? _Instance;
        public static Game Instance 
        { 
            get { 
                if (_Instance == null)
                    _Instance = new Game();
                
                return _Instance; 
            } 
        }

        public void Load()
        {
        }

        public void Update()
        {
            foreach (var (_, gameObject) in GameObjectPool)
            {
                gameObject.Update();
            }
            foreach(var gameObject in AddGameObject)
            {
                GameObjectPool.Add(gameObject.Id, gameObject);
            }
            AddGameObject.Clear();
            foreach(var gameObject in DelGameObject)
            {
                GameObjectPool.Remove(gameObject.Id);
            }
            DelGameObject.Clear();
        }


        public void FixedUpdate()
        {
            foreach (var (_, gameObject) in GameObjectPool)
            {
                gameObject.Update();
            }
        }

        public void UnLoad()
        {

        }
    }
}
