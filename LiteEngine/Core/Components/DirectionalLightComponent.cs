using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Components;

public class DirectionalLightComponent : LightComponent
{
    public DirectionalLightComponent(Component parent, string name = "DirectionalLight") : base(parent, name)
    {
        Engine.Instance.World.LightSystem.Add(Info);
    }
    DirectionalLightInfo Info;

    public Color Color
    {
        get => Color.FromArgb((int)(Info.Color.X * 255), (int)(Info.Color.Y * 255), (int)(Info.Color.Z * 255));
        set => Info.Color = new Vector3(value.R / 255f, value.G/ 255f, value.B / 255f);
    }

    public override void Destory()
    {
        base.Destory();
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

    }
    public static void UpdateLights()
    {

    }
}



[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DirectionalLightInfo
{
    public Vector3 Direction;
    public Vector3 Color;
}
