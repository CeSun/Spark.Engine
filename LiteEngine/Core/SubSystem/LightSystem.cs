using LiteEngine.Core.Components;
using LiteEngine.Core.Render.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.SubSystem
{
    public class LightSystem
    {
        UniformBufferObject DLUbo;
        public List<DirectionalLightInfo> DirectionalLights { get; private set; }
        public unsafe LightSystem()
        {
            DirectionalLights = new List<DirectionalLightInfo>();
            DLUbo = new UniformBufferObject((uint)(sizeof(DirectionalLightInfo) * 20));

        }
        public void Init()
        {
        }

        public void Add<T>(T t) where T : struct
        {
            if (t is DirectionalLightInfo info)
            {
                DirectionalLights.Add(info);
            }


        }

        public void Remove<T>(T t) where T : struct
        {
            if (t is DirectionalLightInfo info)
            {
                DirectionalLights.Remove(info);
            }
        }
        public void UpdateLight()
        {

        }
        public void Fini()
        {

        }
    }
}
