using Spark.Engine.GameLevel;
using Spark.Engine.Render.Proxy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Engine.Components;

public class CameraComponent : PrimitiveComponent
{
    CameraProxy CameraProxy;
    public CameraComponent(Level level) : base(level)
    {
        CameraProxy = (CameraProxy)PrimitiveProxy;
        NearPlaneDistance = 10;
        FarPlaneDistance = 100;
        FieldOfView = 90f;
    }

    protected override PrimitiveProxy CreateProxy()
    {
        return new CameraProxy() 
        {
            NearPlaneDistance = NearPlaneDistance,
            FarPlaneDistance = FarPlaneDistance,
            FieldOfView = FieldOfView,
        };
    }

    public float NearPlaneDistance
    {
        get => NearPlaneDistance;
        set
        {
            _NearPlaneDistance = value;
            RenderThread.AddCommand(rt =>
            {
                CameraProxy.NearPlaneDistance = value;
            });
        }
    }

    public float _NearPlaneDistance;

    public float FarPlaneDistance
    {
        get => _FarPlaneDistance;
        set 
        { 
            _FarPlaneDistance = value;
            RenderThread.AddCommand(rt =>
            {
                CameraProxy.FarPlaneDistance = value;
            });
        }
    }

    public float _FarPlaneDistance;


    public float FieldOfView
    {
        get => _FieldOfView;
        set
        {
            _FieldOfView = value;
            RenderThread.AddCommand(rt =>
            {
                CameraProxy.FieldOfView = value;
            });
        }
    }

    public float _FieldOfView;
}
