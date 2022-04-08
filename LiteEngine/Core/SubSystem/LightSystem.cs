using LiteEngine.Core.Components;
using LiteEngine.Core.Render.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.SubSystem
{
    public class LightSystem
    {
        UniformBufferObject DLUbo;
        private int Size;
        private bool IsNeedUpdate = false;
        private List<DirectionalLightComponent> DirectionalLights { get; set; }
       
        public unsafe LightSystem()
        {
            Size = 20;
            DirectionalLights = new List<DirectionalLightComponent>();
            DLUbo = new UniformBufferObject((uint)(sizeof(DirectionalLightInfo) * Size) + sizeof(int), 1);

        }
        public void Init()
        {
        }

        public void Add(DirectionalLightComponent light)
        {
            if (DirectionalLights.Count >= Size)
                throw new("定向光源已经达到上限");
            DirectionalLights.Add(light);
            IsNeedUpdate = true;

        }

        public void Remove(DirectionalLightComponent light)
        {
            DirectionalLights.Remove(light);
            IsNeedUpdate = true;
        }

        public unsafe void Update(float deltaTime)
        {
            if (IsNeedUpdate)
            {
                int length = DirectionalLights.Count;
                void* buffer = &length;
                DLUbo.UpdateData(buffer, 0, sizeof(int));
            }
            uint offset = sizeof(int);
            for(int i =0; i < DirectionalLights.Count; i ++)
            {
                var light = DirectionalLights[i];
                if (!IsNeedUpdate && !light.IsNeedUpdate)
                    continue;
                fixed (void* buffer = &light.GetLightRef())
                {
                    DLUbo.UpdateData(buffer, (nint)(offset + i * sizeof(DirectionalLightInfo)), (uint)(offset + (i + 1) * sizeof(DirectionalLightInfo)));
                }
            }
            IsNeedUpdate = false;
        }

        public void Fini()
        {

        }
    }
}
