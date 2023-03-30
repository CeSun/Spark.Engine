using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using Spark.Engine.Core.Actors;
using Spark.Engine.Core.Assets;
using Spark.Engine.Core.Components;
using Spark.Util;
using Silk.NET.Input;

using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Core;

public partial class Level
{

    public World CurrentWorld { private set; get; }
    public Level(World world)
    {
        CurrentWorld = world;
    }

    Actor? StaticMeshActor;
    Actor? CameraActor;

    Vector2 MoveData = default;
    Vector2 LastPosition;

    public void OnMouseMove(IMouse mouse,  Vector2 position)
    {
        if (MainMouse.IsButtonPressed(MouseButton.Left))
        {
            if (CameraActor == null)
                return;
            var moveable = position - LastPosition;
            LastPosition = position;

            MoveData += (moveable * 0.1f);
            var rotation = Quaternion.CreateFromYawPitchRoll(-1 * MoveData.X.DegreeToRadians(), -1 * MoveData.Y.DegreeToRadians(), 0);
            CameraActor.WorldRotation = rotation;

        }
    }

    public void OnMouseKeyDown(IMouse mouse, MouseButton key)
    {
        if (key == MouseButton.Left)
        {
            LastPosition = mouse.Position;
        }
    }
    public void BeginPlay()
    {
        MainMouse.MouseMove += OnMouseMove;
        MainMouse.MouseDown += OnMouseKeyDown;


        var CameraActor = new Actor(this);
        var CameraComponent = new CameraComponent(CameraActor);
        CameraActor.RootComponent = CameraComponent;
        CameraActor.WorldLocation += CameraComponent.UpVector * 10;
        CameraComponent.NearPlaneDistance = 1;
        CameraComponent.FarPlaneDistance =  100f;
        CameraComponent.ProjectionType = ProjectionType.Perspective;
        this.CameraActor = CameraActor;
        CameraComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, -90f.DegreeToRadians(), 0);

        var CubeActor = new Actor(this);
        var CubeMeshComp = new StaticMeshComponent(CubeActor);
        CubeMeshComp.StaticMesh = new StaticMesh("/StaticMesh/cube2.glb");
        CubeActor.RootComponent = CubeMeshComp;
        CubeMeshComp.WorldScale = new Vector3(10, 1, 10);

        /*
        for(int i = 0; i < 15; i ++)
        {
            var DirectionActor = new Actor(this);
            var DirectionComp = new DirectionLightComponent(DirectionActor);
            DirectionActor.RootComponent = DirectionComp;
            DirectionComp.Color = Color.Pink;
            DirectionComp.WorldRotation = Quaternion.CreateFromYawPitchRoll(90f.DegreeToRadians(), -30f.DegreeToRadians(), 0f);
            DirectionComp.LightStrength = 0.5f;



            var PointLight = new Actor(this);
            var PointLightComp = new PointLightComponent(PointLight);
            PointLight.RootComponent = PointLightComp;
            PointLightComp.Color = Color.Green;
            PointLightComp.WorldLocation += PointLightComp.UpVector * 20 - PointLightComp.RightVector * 5; ;


            var PointLight2 = new Actor(this);
            var PointLightComp2 = new PointLightComponent(PointLight2);
            PointLight2.RootComponent = PointLightComp2;
            PointLightComp2.Color = Color.Red;
            PointLightComp2.WorldLocation += PointLightComp2.UpVector * 20 + PointLightComp2.RightVector * 5; ;
        }
        */

        var PointLight = new Actor(this);
        var PointLightComp = new PointLightComponent(PointLight);
        PointLight.RootComponent = PointLightComp;
        PointLightComp.Color = Color.Green;
        PointLightComp.WorldLocation += PointLightComp.UpVector * 20 - PointLightComp.RightVector * 10; ;

        var SpotLightActor = new Actor(this);
        var SpotLightComponent = new SpotLightComponent(SpotLightActor);
        SpotLightActor.RootComponent = SpotLightComponent;
        SpotLightComponent.Color = Color.Red;
        SpotLightComponent.WorldLocation += SpotLightComponent.UpVector * 5 ;
        SpotLightComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(0, -70f.DegreeToRadians(), 0);
        StaticMeshActor = CubeActor;
    }

    public void Destory() 
    { 
    }
    float a = 0;
    public void Update(double DeltaTime)
    {
        CameraMove(DeltaTime);
        ActorUpdate(DeltaTime);
    }

    private void CameraMove(double DeltaTime)
    {
        if (CameraActor == null)
            return;
        Vector3 MoveDirection = Vector3.Zero;
        if (MainKeyBoard.IsKeyPressed(Key.W))
        {
            MoveDirection.Z = -1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.S))
        {
            MoveDirection.Z = 1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.A))
        {
            MoveDirection.X = -1;
        }
        if (MainKeyBoard.IsKeyPressed(Key.D))
        {
            MoveDirection.X = 1;
        }
        if (MoveDirection.Length() != 0)
        {
            MoveDirection = Vector3.Normalize(MoveDirection);
            MoveDirection = Vector3.Transform(MoveDirection, CameraActor.WorldRotation);
            CameraActor.WorldLocation += MoveDirection * 10 * (float)DeltaTime;
        }
    }


    public void Render(double DeltaTime)
    {
        if (StaticMeshActor != null)
        {
            a += ((float)(DeltaTime) * 20);
            // StaticMeshActor.WorldRotation = Quaternion.CreateFromYawPitchRoll((float)(a.DegreeToRadians()), a.DegreeToRadians(), 0);
        }
        foreach (var camera in CameraComponents)
        {
            camera.RenderScene(DeltaTime);
        }
    }
}



public partial class Level
{
    private List<Actor> _Actors = new List<Actor>();
    private List<Actor> _DelActors = new List<Actor>();
    private List<Actor> _AddActors = new List<Actor>();
    public IReadOnlyList<Actor> Actors => _Actors;

    public void RegistActor(Actor actor)
    {
        if (_Actors.Contains(actor))
            return;
        if (_AddActors.Contains(actor))
            return;
        if (!_DelActors.Contains(actor))
            return;
        _AddActors.Add(actor);
    }

    public void UnregistActor(Actor actor)
    {
        if (!_Actors.Contains(actor))
            return;
        if (!_AddActors.Contains(actor))
            return;
        if (_DelActors.Contains(actor))
            return;
        _DelActors.Add(actor);

    }

    public void ActorUpdate(double DeltaTime)
    {
        foreach (Actor actor in _Actors)
        {
            actor.Update(DeltaTime);
        }
        _Actors.AddRange(_AddActors);
        _AddActors.Clear();
        _DelActors.ForEach(actor => _Actors.Remove(actor));
        _DelActors.Clear();
    }

}

public partial class Level
{
    private List<PrimitiveComponent> _PrimitiveComponents = new List<PrimitiveComponent>();
    private List<CameraComponent> _CameraComponents = new List<CameraComponent>();
    private List<DirectionLightComponent> _DirectionLightComponents = new List<DirectionLightComponent>();
    private List<PointLightComponent> _PointLightComponents = new List<PointLightComponent>();
    private List<SpotLightComponent> _SpotLightComponents = new List<SpotLightComponent>();
    public IReadOnlyList<CameraComponent> CameraComponents => _CameraComponents;
    public IReadOnlyList<PrimitiveComponent> PrimitiveComponents => _PrimitiveComponents;
    public IReadOnlyList<DirectionLightComponent> DirectionLightComponents => _DirectionLightComponents;
    public IReadOnlyList<PointLightComponent> PointLightComponents => _PointLightComponents;
    public IReadOnlyList<SpotLightComponent> SpotLightComponents => _SpotLightComponents;
    public void RegistComponent(PrimitiveComponent component)
    {
        if (PrimitiveComponents.Contains(component))
        {
            return;
        }
        _PrimitiveComponents.Add(component);
        if (component is CameraComponent cameraComponent)
        {
            if (!_CameraComponents.Contains(cameraComponent))
            {
                _CameraComponents.Add(cameraComponent);
                _CameraComponents.Order();
            }
        }
        else if (component is DirectionLightComponent directionLightComponent)
        {
            if (!_DirectionLightComponents.Contains(directionLightComponent))
            {
                _DirectionLightComponents.Add(directionLightComponent);
            }
        }
        else if (component is PointLightComponent pointLightComponent)
        {
            if (!_PointLightComponents.Contains(pointLightComponent))
            {
                _PointLightComponents.Add(pointLightComponent);
            }
        }
        else if (component is SpotLightComponent spotLightComponent)
        {
            if (!_SpotLightComponents.Contains(spotLightComponent))
            {
                _SpotLightComponents.Add(spotLightComponent);
            }
        }
    }

    public void UnregistComponent(PrimitiveComponent component)
    {
        if (!PrimitiveComponents.Contains(component))
        {
            return;
        }
        _PrimitiveComponents.Remove(component);
        if (component is CameraComponent cameraComponent)
        {
            if (_CameraComponents.Contains(cameraComponent))
                _CameraComponents.Remove(cameraComponent);
        }
        else if (component is DirectionLightComponent directionLightComponent)
        {
            if (_DirectionLightComponents.Contains(directionLightComponent))
            {
                _DirectionLightComponents.Remove(directionLightComponent);
            }
        }
        else if (component is PointLightComponent pointLightComponent)
        {
            if (_PointLightComponents.Contains(pointLightComponent))
            {
                _PointLightComponents.Remove(pointLightComponent);
            }
        }
        else if (component is SpotLightComponent spotLightComponent)
        {
            if (_SpotLightComponents.Contains(spotLightComponent))
            {
                _SpotLightComponents.Remove(spotLightComponent);
            }
        }
    }
}