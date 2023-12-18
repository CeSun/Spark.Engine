using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Physics;
using System.Numerics;

namespace Spark.Engine.Components;

public class StaticMeshComponent : PrimitiveComponent
{
    protected override bool ReceieveUpdate => true;

    private StaticMesh? _StaticMesh;
    public BoundingBox? BoundingBox;

    public override BaseBounding? Bounding => BoundingBox;
    [Property]
    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set
        {
            _StaticMesh = value;
            BoundingBox = null;
            if (_StaticMesh != null)
            {
                var worldTransform = WorldTransform;
                Box box = default;
                for(int i = 0; i < 8; i ++)
                {
                    if (i == 0)
                    {
                        box.MinPoint = Vector3.Transform(_StaticMesh.Box[i], worldTransform);
                        box.MaxPoint = box.MinPoint;
                    }
                    else
                    {
                        box += Vector3.Transform(_StaticMesh.Box[i], worldTransform);
                    }
                }
                BoundingBox = new BoundingBox(this);
                BoundingBox.Box.MaxPoint = box.MaxPoint;
                BoundingBox.Box.MinPoint = box.MinPoint;
            }
            Engine.NextFrame.Add(InitRender);
        }
    }
    public StaticMeshComponent(Actor actor) : base(actor)
    {

    }

 
    public override void OnUpdate(double DeltaTime)
    {
        base.OnUpdate(DeltaTime);
        if (BoundingBox != null && StaticMesh != null)
        {
            var worldTransform = WorldTransform;

            Box box = default;
            for (int i = 0; i < 8; i++)
            {
                if (i == 0)
                {
                    box.MinPoint = Vector3.Transform(StaticMesh.Box[i], worldTransform);
                    box.MaxPoint = box.MinPoint;
                }
                else
                {
                    box += Vector3.Transform(StaticMesh.Box[i], worldTransform);
                }
            }

            BoundingBox.Box.MaxPoint = box.MaxPoint;
            BoundingBox.Box.MinPoint = box.MinPoint;

            UpdateOctree();
        }

    }
    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
        if (StaticMesh != null)
        {
            int index = 0;
            gl.PushGroup("Render Static Mesh:" + StaticMesh.Path);
            foreach (var element in StaticMesh.Elements)
            {
                if (element.VertexArrayObjectIndex == 0)
                    continue;
                for (int i = 0; i < element.Material.Textures.Count(); i++)
                {
                    var texture = element.Material.Textures[i];
                    gl.ActiveTexture(GLEnum.Texture0 + i);
                    if (texture != null)
                    {
                        gl.BindTexture(GLEnum.Texture2D, texture.TextureId);
                    }
                    else
                    {
                        gl.BindTexture(GLEnum.Texture2D, 0);
                    }
                }
                gl.BindVertexArray(element.VertexArrayObjectIndex);
                unsafe
                {
                    gl.DrawElements(GLEnum.Triangles, (uint)element.IndicesLen, GLEnum.UnsignedInt, (void*)0);
                }
                index++;
            }
            gl.PopGroup();
        }
    }

    public void InitRender()
    {
        if (StaticMesh == null)
            return;
        StaticMesh.InitRender(gl);
        foreach (var element in StaticMesh.Elements)
        {
            foreach (var texture in
            element.Material.Textures)
            {
                if (texture != null)
                    texture.InitRender(gl);
            }
        }
    }
}
