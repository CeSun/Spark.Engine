using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Physics;
using System.Numerics;
using System.Runtime.InteropServices;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.Components;

public class HierarchicalInstancedStaticMeshComponent : PrimitiveComponent
{
    public StaticMesh? StaticMesh 
    {
        get => _StaticMesh;
        set
        {
            if (value == null)
            {
                _StaticMesh = null;
                return;
            }
            if (_StaticMesh != value)
            {
                if (value.VertexArrayObjectIndexes.Count != 1)
                    throw new Exception();
                _StaticMesh = value;
            }
        }
    }
    public StaticMesh? _StaticMesh;

    public int NodeLength = 0;
    public int NodeMinLength = 1000;
    List<ClustreeNode> NodeList { get; set; } = new List<ClustreeNode>();
    List<PrimitiveComponent> PrimitiveComponents;
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
        PrimitiveComponents = new List<PrimitiveComponent>();
    }

    public void AddComponent(PrimitiveComponent primitivce)
    {
        if (primitivce == null)
            return;
        PrimitiveComponents.Add(primitivce);
    }

    protected override void OnBeginGame()
    {
        base.OnBeginGame();
    }

    public void RefreshTree()
    {
        BuildTree();
        BuildInstances();
    }
    public void BuildTree()
    {

        Split(0, PrimitiveComponents.Count - 1);
        NodeLength = NodeList.Count;
        int left = 0;
        int right = NodeList.Count - 1;
        int length = 0;
        do
        {
            left = right + 1;
            right = left + length;
            length = SplitNode(left, right);
        } while (length > 1);
        var root = NodeList.LastOrDefault();
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
            NodeList.Add(Node);
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
        SortComponents.Sort((LeftComp, RightComp) =>  (StaticMesh.Box * LeftComp.WorldTransform).ComperaTo(StaticMesh.Box * RightComp.WorldTransform, MainAxis));
        int middle = SortComponents.Length / 2;
        Split(left, left + middle);
        Split(left + middle + 1, right);
    }

    private int SplitNode(int left, int right)
    {
        var Nodes = CollectionsMarshal.AsSpan(NodeList).Slice(left, right - left);
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
            NodeList.Add(Node);
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
        Nodes.Sort((left, right) => left.Box.ComperaTo(right.Box, MainAxis));
        int middle = Nodes.Length / 2;
        return SplitNode(left, left + middle) + SplitNode(left + middle + 1, right); 
    }

    public unsafe void BuildInstances()
    {
        if (StaticMesh == null)
            return;
        List<Matrix4x4> WorldTransforms = new List<Matrix4x4> ();
        foreach(var component in PrimitiveComponents)
        {
            WorldTransforms.Add(component.WorldTransform);
            WorldTransforms.Add(component.NormalTransform);
        }
        var vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        
        fixed(void* p = CollectionsMarshal.AsSpan(WorldTransforms))
        {
            gl.BufferData((GLEnum)BufferTargetARB.ArrayBuffer, (nuint)(sizeof(Matrix4x4) * WorldTransforms.Count), p, BufferUsageARB.StaticDraw);
        }
        gl.BindVertexArray(StaticMesh.VertexArrayObjectIndexes.FirstOrDefault());
        gl.EnableVertexAttribArray(6);
        gl.VertexAttribPointer(6, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)0);
        gl.EnableVertexAttribArray(7);
        gl.VertexAttribPointer(7, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)sizeof(Vector4));
        gl.EnableVertexAttribArray(8);
        gl.VertexAttribPointer(8, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)(sizeof(Vector4) * 2));
        gl.EnableVertexAttribArray(9);
        gl.VertexAttribPointer(9, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4), (void*)(sizeof(Vector4) * 3));

        gl.EnableVertexAttribArray(10);
        gl.VertexAttribPointer(10, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 4));
        gl.EnableVertexAttribArray(11);
        gl.VertexAttribPointer(11, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 5));
        gl.EnableVertexAttribArray(12);
        gl.VertexAttribPointer(12, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 6));
        gl.EnableVertexAttribArray(13);
        gl.VertexAttribPointer(13, 4, GLEnum.Float, false, (uint)sizeof(Matrix4x4) * 2, (void*)(sizeof(Vector4) * 7));
        gl.BindVertexArray(0);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
    }


    List<ClustreeNode> ClustreeNodes { get; set; } = new List<ClustreeNode>();
    ClustreeNode Clustree { get; set; } = new ClustreeNode();

    List<ClustreeNode> RenderList = new List<ClustreeNode>();


    public void CameraCulling(CameraComponent camera)
    {
        RenderList.Clear();

        foreach(var node in CollectionsMarshal.AsSpan(NodeList).Slice(0, NodeLength))
        {
            RenderList.Add(node);
        }

    }
    public void RenderHISM(double DeltaTime)
    {
        if (StaticMesh == null)
            return;
        StaticMesh.Materials.FirstOrDefault()?.Use();
        foreach (var node in RenderList)
        {
            gl.BindVertexArray(StaticMesh.VertexArrayObjectIndexes.FirstOrDefault());
            gl.DrawElementsInstanced(GLEnum.Triangles, (uint)StaticMesh.IndicesList.First().Count, GLEnum.UnsignedInt, (uint)node.FirstInstance, (uint)(node.LastInstance - node.FirstInstance) + 1);
            gl.BindVertexArray(0);
        }
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

public class SubHierarchicalInstancedStaticMeshComponent : PrimitiveComponent
{
    public SubHierarchicalInstancedStaticMeshComponent(Actor actor) : base(actor)
    {
    }
}