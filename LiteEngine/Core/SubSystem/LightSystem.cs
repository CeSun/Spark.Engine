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
        UniformBufferObject DirectionalLightsBuffer;
        UniformBufferObject PointLightsBuffer;
        UniformBufferObject SpotLightsBuffer;
        // 默认光源数量
        private int Size;

        private bool DirectionalLightsDirty = false;

        private bool PointLightsDirty = false;

        private bool SpotLightsDirty = false;
        private List<DirectionalLightComponent> DirectionalLights { get; set; }
        private List<PointLightComponent> PointLights { get; set; } 
        private List<SpotLightComponent> SpotLights { get; set; }


        public unsafe LightSystem()
        {
            Size = 20;
            DirectionalLights = new List<DirectionalLightComponent>();
            PointLights = new List<PointLightComponent>();
            SpotLights = new List<SpotLightComponent>();

            DirectionalLightsBuffer = new UniformBufferObject((uint)(sizeof(DirectionalLightInfo) * Size), 1);
            PointLightsBuffer = new UniformBufferObject((uint)(sizeof(PointLightInfo) * Size), 2);
            SpotLightsBuffer = new UniformBufferObject((uint)(sizeof(SpotLightInfo) * Size), 3);

        }
        public void Init()
        {
        }

        public void Add(DirectionalLightComponent light)
        {
            if (DirectionalLights.Count >= Size)
                throw new("定向光源已经达到上限");
            DirectionalLights.Add(light);
            DirectionalLightsDirty = true;

        }

        public void Add(SpotLightComponent light)
        {
            if (SpotLights.Count >= Size)
                throw new Exception("定向光源已经达到上限");
            SpotLights.Add(light);
            SpotLightsDirty = true;
        }

        public void Add(PointLightComponent light)
        {
            if (SpotLights.Count >= Size)
                throw new Exception("定向光源已经达到上限");
            PointLights.Add(light);
            PointLightsDirty = true;
        }

        public void Remove(PointLightComponent light)
        {
            PointLights.Remove(light);
            PointLightsDirty = true;
        }

        public void Remove(SpotLightComponent light)
        {
            SpotLights.Remove(light);
            SpotLightsDirty = true;
        }


        public void Remove(DirectionalLightComponent light)
        {
            DirectionalLights.Remove(light);
            DirectionalLightsDirty = true;
        }

        public unsafe void Update(float deltaTime)
        {
            // 定向光源
            for(int i =0; i < DirectionalLights.Count; i ++)
            {
                var light = DirectionalLights[i];
                if (!DirectionalLightsDirty && !light.IsNeedUpdate)
                    continue;
                fixed (void* buffer = &light.GetLightRef())
                {
                    DirectionalLightsBuffer.UpdateData(buffer, (nint)(i * sizeof(DirectionalLightInfo)), (uint)((i + 1) * sizeof(DirectionalLightInfo)));
                }
            }
            DirectionalLightsDirty = false;

            // 点光源
            for (int i = 0; i < PointLights.Count; i++)
            {
                var light = PointLights[i];
                if (!PointLightsDirty && !light.IsNeedUpdate)
                    continue;
                fixed (void* buffer = &light.GetLightRef())
                {
                    PointLightsBuffer.UpdateData(buffer, (nint)(i * sizeof(PointLightInfo)), (uint)((i + 1) * sizeof(PointLightInfo)));
                }
            }
            PointLightsDirty = false;

            // 投射光源
            for (int i = 0; i < SpotLights.Count; i++)
            {
                var light = PointLights[i];
                if (!SpotLightsDirty && !light.IsNeedUpdate)
                    continue;
                fixed (void* buffer = &light.GetLightRef())
                {
                    SpotLightsBuffer.UpdateData(buffer, (nint)(i * sizeof(SpotLightInfo)), (uint)((i + 1) * sizeof(SpotLightInfo)));
                }
            }
            SpotLightsDirty = false;

        }

        public void Fini()
        {

        }
    }
}
