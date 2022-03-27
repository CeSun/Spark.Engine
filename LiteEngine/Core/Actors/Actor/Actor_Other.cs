using LiteEngine;
using Silk.NET.OpenGL;
using System.Numerics;


namespace LiteEngine.Core.Actors;

public partial class Actor
{
    public Engine EngineInstance { get => Engine.Instance; }
    public World World { get { if (EngineInstance.World == null) throw new Exception("世界尚未创建");  return EngineInstance.World; } }

    protected GL gl { get => Engine.Instance.Gl; }
    public virtual void Update(float deltaTime)
    {
        var scaleMat4 = Matrix4x4.CreateScale(WorldScale);
        var rotationMat4 = Matrix4x4.CreateFromQuaternion(WorldRotation);
        var translateMat4 = Matrix4x4.CreateTranslation(WorldLocation);
        WorldTransform = scaleMat4 * rotationMat4 * translateMat4;
        RootComponent.Update(deltaTime);
    }
}