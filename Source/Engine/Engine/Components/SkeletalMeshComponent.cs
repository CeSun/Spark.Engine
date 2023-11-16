using Silk.NET.OpenGLES;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using System.Numerics;

namespace Spark.Engine.Components;

public class SkeletalMeshComponent : PrimitiveComponent
{
    public SkeletalMeshComponent(Actor actor) : base(actor)
    {
        for (var i = 0; i < 100; i++)
        {
            AnimBuffer.Add(Matrix4x4.Identity);
        }
    }

    public List<Matrix4x4> AnimBuffer = new List<Matrix4x4>(100);
    protected override bool ReceieveUpdate => true;
    public SkeletalMesh? SkeletalMesh 
    { 
        get => _SkeletalMesh;
        set
        {
            _SkeletalMesh = value;
            Engine.NextFrame.Add(InitRender);
        }
    }

    private SkeletalMesh? _SkeletalMesh;
    public AnimSequence? AnimSequence 
    { 
        get 
        {
            if (AnimSampler == null)
            {
                return null;
            }
            return AnimSampler.Sequence;
        }
        set
        {
            if (value == null)
            {
                AnimSampler = null;
                return;
            }
            AnimSampler = new AnimSampler(value);
        }
    }
    public AnimSampler? AnimSampler { get; private set; }
    public override void Render(double DeltaTime)
    {
        base.Render(DeltaTime);
        if (SkeletalMesh != null)
        {
            int index = 0;
            gl.PushGroup("Render Skeletal Mesh");
           
            foreach (var element in SkeletalMesh.Elements)
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
        if (SkeletalMesh == null)
            return;
        SkeletalMesh.InitRender(gl);
        foreach(var element in SkeletalMesh.Elements)
        {
            foreach(var texture in 
            element.Material.Textures)
            {
                if (texture != null)
                    texture.InitRender(gl);
            }
        }
    }
    public override void OnUpdate(double DeltaTime)
    {
        if (AnimSampler != null && SkeletalMesh != null && SkeletalMesh.Skeleton != null)
        {
            AnimSampler.Update(DeltaTime);
            ProcessNode(AnimSampler.Skeleton.Root);

            foreach (var bone in SkeletalMesh.Skeleton.BoneList)
            {
                AnimBuffer[bone.BoneId] = AnimBuffer[bone.BoneId] * SkeletalMesh.Skeleton.RootParentMatrix;
            }
        }
    }

    protected override Matrix4x4 GetSocketWorldTransform(string socket)
    {
        if (SkeletalMesh == null)
            return base.GetSocketWorldTransform(socket);
        if (SkeletalMesh.Skeleton == null)
            return base.GetSocketWorldTransform(socket);
        if (SkeletalMesh.Skeleton.BonesMap.TryGetValue(socket, out var bone) == false)
            return base.GetSocketWorldTransform(socket);
        return AnimBuffer[bone.BoneId]  * WorldTransform;
    }
    private void ProcessNode(BoneNode node)
    {
        if (AnimSampler == null)
            return;
        Matrix4x4 ParentTransform = Matrix4x4.Identity;
        if (node.Parent != null)
        {
            AnimBuffer[node.BoneId] = AnimSampler.TransfomrBuffer[node.BoneId] * AnimBuffer[node.Parent.BoneId];
        }
        else
        {
            AnimBuffer[node.BoneId] = AnimSampler.TransfomrBuffer[node.BoneId];
        }
        foreach (var child in node.ChildrenBone)
        {
            ProcessNode(child);
        }
    }
}
