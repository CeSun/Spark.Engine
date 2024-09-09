using Silk.NET.OpenGLES;
using Spark.Engine.Assets;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spark.Engine.World;

namespace Spark.Engine.Components;

public class HierarchicalInstancedStaticMeshComponent : InstancedStaticMeshComponent
{
    public int NodeLength = 0;
    public int NodeMinLength = 1000;
    uint Vbo = 0;
    List<ClustreeNode> NodeList { get; set; } = new List<ClustreeNode>();
    List<ClustreeNode> AsyncNodeList { get; set; }  = new List<ClustreeNode> { };
    public ClustreeNode Root = new ClustreeNode()
    {
        Box = new Box
        {
            MaxPoint = new (0, 0, 0),
            MinPoint = new (0, 0, 0),
        },
        FirstInstance = 0,
        LastInstance = 0,
    };

    public HierarchicalInstancedStaticMeshComponent(Actor actor) : base(actor)
    {
    }

    public override async void Build()
    {
        var sw = Stopwatch.StartNew();

        await Task.Run(BuildTree);
        (NodeList, AsyncNodeList) = (AsyncNodeList, NodeList);
        BuildInstances();
        CanRender = true;
        sw.Stop();
        Console.WriteLine("build Tree: " + sw.ElapsedMilliseconds);
    }

    public async void ReBuild()
    {
        Console.WriteLine("begin Rebuild");
        var sw = Stopwatch.StartNew();
        await Task.Run(BuildTree);
        (NodeList, AsyncNodeList) = (AsyncNodeList, NodeList);
        UpdateInstances();
        sw.Stop();
        Console.WriteLine("Rebuild ok:" + sw.ElapsedMilliseconds);
    }
    protected void BuildTree()
    {
        AsyncNodeList.Clear();
        Split(0, PrimitiveComponents.Count - 1);
        NodeLength = AsyncNodeList.Count;
        int left = 0;
        int right = AsyncNodeList.Count - 1;
        int length = 0;
        do
        {
            length = SplitNode(left, right);
            left = right + 1;
            right = left + length;
        } while (length > 1);
        var root = AsyncNodeList.LastOrDefault();
        if (root !=  null)
        {
            Root = root;
        }
    }

    private void Split(int left, int right)
    {
        if (StaticMesh == null)
            return;
        var SortComponents = CollectionsMarshal.AsSpan(PrimitiveComponents).Slice(left, right - left);
        bool IsBoxInit = false;
        var box = new Box(); 
        foreach (var sc in SortComponents)
        {
            var tmpBox = StaticMesh.Box * sc.WorldTransform;
            if (IsBoxInit == false)
            {
                IsBoxInit = true;
                box = tmpBox;
                continue;
            }
            box += tmpBox;
        }

        if (SortComponents.Length <= NodeMinLength)
        {
            var Node = new ClustreeNode();
            Node.Box = box;
            Node.FirstInstance = left;
            Node.LastInstance = right;
            AsyncNodeList.Add(Node);
            return;
        }
        int MainAxis = 0;
        float MaxValue = 0;
        
        for (int i = 0; i < 3; i++)
        {
            var length = box.MaxPoint[i] - box.MinPoint[i];
            if (length > MaxValue)
            {
                MaxValue = length;
                MainAxis = i;
            }
        }
        SortComponents.Sort((LeftComp, RightComp) =>  (StaticMesh.Box * LeftComp.WorldTransform).CompareTo(StaticMesh.Box * RightComp.WorldTransform, MainAxis));
        int middle = SortComponents.Length / 2;
        Split(left, left + middle);
        Split(left + middle + 1, right);
    }

    private int SplitNode(int left, int right)
    {
        var Nodes = CollectionsMarshal.AsSpan(AsyncNodeList).Slice(left, right - left + 1);
        var IsBoxInit = false;
        var box = new Box();
        foreach(var node in Nodes)
        {
            if (IsBoxInit == false)
            {
                box = node.Box;
                IsBoxInit = true;
                continue;
            }
            box += node.Box;
        }

        if (Nodes.Length <= NodeMinLength)
        {
            var Node = new ClustreeNode();
            Node.Box = box;
            Node.FirstChild = left;
            Node.LastChild = right;
            AsyncNodeList.Add(Node);
            return 1;
        }
        int MainAxis = 0;
        float MaxValue = 0;

        for (int i = 0; i < 3; i++)
        {
            var length = box.MaxPoint[i] - box.MinPoint[i];
            if (length > MaxValue)
            {
                MaxValue = length;
                MainAxis = i;
            }
        }
        Nodes.Sort((left, right) => left.Box.CompareTo(right.Box, MainAxis));
        int middle = Nodes.Length / 2;
        return SplitNode(left, left + middle) + SplitNode(left + middle + 1, right); 
    }

    public async void UpdateInstances()
    {
        List<Matrix4x4> WorldTransforms = new List<Matrix4x4>();
        await Task.Run(() =>
        {
            foreach (var component in PrimitiveComponents.ToList())
            {
                WorldTransforms.Add(component.WorldTransform);
                WorldTransforms.Add(component.NormalTransform);
            }
        });
        gl.BindBuffer(GLEnum.ArrayBuffer, Vbo);
       
        unsafe
        {
            fixed (void* p = CollectionsMarshal.AsSpan(WorldTransforms))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(Matrix4x4) * WorldTransforms.Count), p, BufferUsageARB.DynamicDraw);
            }
        }
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }
    public async void BuildInstances()
    {
        if (StaticMesh == null)
            return;
        List<Matrix4x4> WorldTransforms = new List<Matrix4x4>();
        await Task.Run(() =>
        {
            foreach (var component in PrimitiveComponents.ToList())
            {
                WorldTransforms.Add(component.WorldTransform);
                WorldTransforms.Add(component.NormalTransform);
            }
        });
        Vbo = gl.GenBuffer();
        gl.BindBuffer(GLEnum.ArrayBuffer, Vbo);
        unsafe
        {

            fixed (void* p = CollectionsMarshal.AsSpan(WorldTransforms))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(sizeof(Matrix4x4) * WorldTransforms.Count), p, BufferUsageARB.DynamicDraw);
            }

            foreach(var element in StaticMesh.Elements)
            {
                gl.BindVertexArray(element.VertexArrayObjectIndex);
                gl.EnableVertexAttribArray(6);
                gl.VertexAttribPointer(6, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)0);
                gl.EnableVertexAttribArray(7);
                gl.VertexAttribPointer(7, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)sizeof(Vector4));
                gl.EnableVertexAttribArray(8);
                gl.VertexAttribPointer(8, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 2));
                gl.EnableVertexAttribArray(9);
                gl.VertexAttribPointer(9, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 3));

                gl.EnableVertexAttribArray(10);
                gl.VertexAttribPointer(10, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 4));
                gl.EnableVertexAttribArray(11);
                gl.VertexAttribPointer(11, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 5));
                gl.EnableVertexAttribArray(12);
                gl.VertexAttribPointer(12, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 6));
                gl.EnableVertexAttribArray(13);
                gl.VertexAttribPointer(13, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 7));



                gl.VertexAttribDivisor(6, 1);
                gl.VertexAttribDivisor(7, 1);
                gl.VertexAttribDivisor(8, 1);
                gl.VertexAttribDivisor(9, 1);

                gl.VertexAttribDivisor(10, 1);
                gl.VertexAttribDivisor(11, 1);
                gl.VertexAttribDivisor(12, 1);
                gl.VertexAttribDivisor(13, 1);
            }

        }
        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }

    List<ClustreeNode> ClustreeNodes { get; set; } = new List<ClustreeNode>();
    ClustreeNode Clustree { get; set; } = new ClustreeNode();

    List<ClustreeNode> RenderList = new List<ClustreeNode>();


    public void CameraCulling(Plane[] planes)
    {
        RenderList.Clear();
        if (NodeList.Count == 0)
            return;
        TestBox(NodeList, planes, Root);

    }

    public void TestBox(List<ClustreeNode> List ,Plane[] Planes, ClustreeNode Node)
    {
        if (Node.Box.TestPlanes(Planes) == false)
            return;
        if (Node.IsLeftNode == true)
        {
            RenderList.Add(Node);
            return;
        }
        for(var i = Node.FirstChild; i <= Node.LastChild; i ++ )
        {
            TestBox(List, Planes, List[i]);
        }
    }
    public override unsafe void RenderISM(CameraComponent? cameraComponent, double DeltaTime)
    {
        if (CanRender == false)
            return;
        if (StaticMesh == null)
            return;
        var d = 0;
        if (cameraComponent != null)
        {
            var distance = 0f;
            distance = cameraComponent.FarPlaneDistance - cameraComponent.NearPlaneDistance;
            d = (int)(distance / StaticMesh.Elements.Count);
        }



        foreach (var node in RenderList)
        {
            int level = 0;
            
            if (cameraComponent != null)
            {
                var Len = (int)node.Box.GetDistance(cameraComponent.WorldLocation);
                level = Len / d;
                if (level >= StaticMesh.Elements.Count)
                {
                    level = StaticMesh.Elements.Count - 1;
                }
            }
            for (int i = 0; i < StaticMesh.Elements[0].Material.Textures.Count(); i++)
            {
                var texture = StaticMesh.Elements[0].Material.Textures[i];
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
            gl.BindVertexArray(StaticMesh.Elements[level].VertexArrayObjectIndex);
         /*
            if (ExtBaseInstance != null)
            {
                ExtBaseInstance.DrawElementsInstancedBaseInstance((Silk.NET.OpenGLES.Extensions.EXT.EXT)GLEnum.Triangles, (uint)StaticMesh.Elements[level].Indices.Count, (Silk.NET.OpenGLES.Extensions.EXT.EXT)GLEnum.UnsignedInt, (void*)0, (uint)(node.LastInstance - node.FirstInstance) + 1, (uint)node.FirstInstance);
            }
         */
        }
        gl.BindVertexArray(0);
    }


}


public class ClustreeNode
{
    public int FirstInstance = -1;

    public int LastInstance = -1;

    public int FirstChild = -1;

    public int LastChild = -1;

    public Box Box;

    public bool IsLeftNode => FirstChild == -1 && LastChild == -1 && FirstInstance != -1 && LastInstance != -1;
}

