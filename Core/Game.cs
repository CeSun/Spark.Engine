using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core
{
    public class Game
    {
        public static Game Instance { get=>_Instance; }

        public static Game _Instance = new Game();
        public Vector2i Size { get; set; }

        public int GameFboId;

        public Vector2i GameSize;
        public void Init()
        {

        }
        public void Draw(double deltaTime)
        {
            Scene.Current.Draw(deltaTime);
        }

        public void Tick()
        {
            Scene.Current.Tick();
        }
        public void Update()
        {

        }

        public void Fini()
        {

        }



    }
}
