using Noesis;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Spark.Engine.StaticEngine;

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
            gl.PushDebugGroup("Render Skeletal Mesh");
           
            foreach (var mesh in SkeletalMesh.Meshes)
            {
                SkeletalMesh.Materials[index].Use();
                gl.BindVertexArray(SkeletalMesh.VertexArrayObjectIndexes[index]);
                unsafe
                {
                    gl.DrawElements(GLEnum.Triangles, (uint)SkeletalMesh.IndicesList[index].Count, GLEnum.UnsignedInt, (void*)0);
                }
                index++;
            }
            gl.PopDebugGroup();
        }
    }

    public override void Update(double DeltaTime)
    {
        if (AnimSampler != null && SkeletalMesh != null)
        {
            AnimSampler.Update(DeltaTime);
            ProcessNode(AnimSampler.Skeleton.Root);

            foreach(var bone in SkeletalMesh.Skeleton.BoneList)
            {
                AnimBuffer[bone.BoneId] = bone.WorldToLocalTransform * AnimBuffer[bone.BoneId];
                Vector3 v = new Vector3(1, 2, 3);
                v = Vector3.Transform(v, AnimBuffer[bone.BoneId]);

            }
        }
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
