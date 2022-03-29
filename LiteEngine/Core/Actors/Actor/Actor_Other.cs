using LiteEngine;
using Silk.NET.OpenGL;
using System.Numerics;


namespace LiteEngine.Core.Actors;

public partial class Actor
{
    public Engine EngineInstance { get => Engine.Instance; }
    public World World { get { if (EngineInstance.World == null) throw new Exception("世界尚未创建");  return EngineInstance.World; } }

    public Vector3 Up { get; private set; }
    public Vector3 Right { get; private set; }
    public Vector3 Foward { get; private set; }

    protected GL gl { get => Engine.Instance.Gl; }
    public virtual void Update(float deltaTime)
    {
        var scaleMat4 = Matrix4x4.CreateScale(WorldScale);
        var rotationMat4 = Matrix4x4.CreateFromQuaternion(WorldRotation);
        var translateMat4 = Matrix4x4.CreateTranslation(WorldLocation);
        WorldTransform = translateMat4 * rotationMat4* scaleMat4;

        Up = Vector3.Transform(new Vector3(0, 1, 0), WorldTransform);
        Right = Vector3.Transform(new Vector3(-1, 0, 0), WorldTransform);
        Foward = Vector3.Transform(new Vector3(0, 0, 1), WorldTransform);

        RootComponent.Update(deltaTime);
    }
}