using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Components;
using Spark.Util;
using System.Numerics;
using static Spark.Engine.StaticEngine;

namespace Spark.Engine.GUI;

public class ImGuiWarp
{
    Level CurrentLevel { get; set; }

    public ImGuiWarp(Level level)
    {
        CurrentLevel = level;
    }


    ImGuiController? Controller;
    public void Init()
    {
        Controller = new ImGuiController(gl, Engine.Instance.View, Engine.Instance.Input);
    }


    int num = 0;
    public void Render(double DeltaTime)
    {
        Controller?.Update((float)DeltaTime);
        renderDebugPanel();
        renderActorList();
        renderDetail();
        Controller?.Render();

    }

    public void renderActorList()
    {
        ImGui.Begin("Tree:");

        
        ImGui.BeginListBox("");
        foreach (var actor in CurrentLevel.Actors)
        {
            if(ImGui.Button(actor.Name))
            {
                SelectedActor = actor;
            }
        }
        ImGui.EndListBox();
        ImGui.End();
    }

    private Actor SelectedActor;


    
    public void renderDetail()
    {
        ImGui.Begin("Detail");
        if (SelectedActor == null || CurrentLevel.Actors.Contains(SelectedActor) == false)
        {
            ImGui.Text("Please Select Actor");
        }
        else
        {
            ImGui.Text(SelectedActor.Name);
            ImGui.Text("Location:");
            ImGui.SameLine();
            Vector3 Location = SelectedActor.WorldLocation;
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("lx", ref Location.X);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("ly", ref Location.Y);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("lz", ref Location.Z);
            SelectedActor.WorldLocation = Location;

            ImGui.Text("Rotation:");
            ImGui.SameLine();
            Vector3 Rotation = SelectedActor.WorldRotation.ToEuler();
            Rotation.X = Rotation.X.RadiansToDegree();
            Rotation.Y = Rotation.Y.RadiansToDegree();
            Rotation.Z = Rotation.Z.RadiansToDegree();
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("rx", ref Rotation.X);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("ry", ref Rotation.Y);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("rz", ref Rotation.Z);
            
            SelectedActor.WorldRotation = Quaternion.CreateFromYawPitchRoll(Rotation.Y.DegreeToRadians(), Rotation.Z.DegreeToRadians(), Rotation.X.DegreeToRadians());

            ImGui.Text("Scale:");
            ImGui.SameLine();
            Vector3 Scale = SelectedActor.WorldScale;
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("sx", ref Scale.X);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("sy", ref Scale.Y);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            ImGui.InputFloat("sz", ref Scale.Z);
            SelectedActor.WorldScale = Scale;




        }
        ImGui.End();
    }
    public void renderDebugPanel()
    {
        // ImGuiNET.ImGui.ShowDemoWindow();
        ImGui.Begin("Debug Panel ");
        ImGui.Text("Instance Num:");
        ImGui.SameLine();
        ImGui.InputInt("", ref num);
        if (num < 0)
            num = 0;

        if (ImGui.Button("HISM"))
        {
            if (num > 0)
            {
                CreateHISM();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("ISM"))
        {
            if (num > 0)
            {
                CreateISM();
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            foreach (var item in tmpList)
            {
                item.Destory();
            }
            tmpList.Clear();
        }

        ImGui.NewLine();

        if (ImGui.Button("Create Physics Cube"))
        {
            CreateCubes();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear All"))
        {
            foreach (var cube in cubeList)
            {
                cube.Destory();
            }
            cubeList.Clear();
        }

        ImGui.End();
    }
    List<Actor> tmpList = new List<Actor>();
    List<Actor> cubeList = new List<Actor>();
    async void CreateHISM()
    {
        await Console.Out.WriteLineAsync("[HISM]正在生成:" + num);
        int len = (int)Math.Sqrt(100000);

        var task1 = InitHISM("/StaticMesh/flower.glb", num, new Vector2((-len / 2) * 15f, (len / 2) * 15f));
        var task2 = InitHISM("/StaticMesh/grass.glb", num, new Vector2((-len / 2) * 15f, (len / 2) * 15f));

        await Task.WhenAll(task1, task2);

        tmpList.Add(task1.Result);
        tmpList.Add(task2.Result);
    }

    void CreateCubes()
    {
        var SM = new StaticMesh("/StaticMesh/cube2.glb");
        for (int i = 0; i < 20; i++)
        {

            var CubeActor2 = new Actor(this.CurrentLevel, "CubeActor" + cubeList.Count);
            var CubeMeshComp2 = new StaticMeshComponent(CubeActor2);
            CubeActor2.RootComponent = CubeMeshComp2;
            CubeMeshComp2.StaticMesh = SM;
            CubeMeshComp2.IsStatic = false;
            var scale = (float)Random.Shared.NextDouble();
            CubeMeshComp2.WorldScale = new Vector3(scale, scale, scale);
            CubeMeshComp2.WorldRotation = Quaternion.CreateFromYawPitchRoll(Random.Shared.Next(0, 360), Random.Shared.Next(0, 360), Random.Shared.Next(0, 360));
            CubeMeshComp2.WorldLocation = new Vector3(Random.Shared.Next(-10, 10), Random.Shared.Next(50, 60), 0);
            cubeList.Add(CubeActor2);

        }
    }

    async void CreateISM()
    {
        await Console.Out.WriteLineAsync("[ISM]正在生成:" + num);
        int len = (int)Math.Sqrt(100000);
        var task1 = InitISM("/StaticMesh/flower.glb", num, new Vector2((-len / 2) * 15f, (len / 2) * 15f));
        var task2 = InitISM("/StaticMesh/grass.glb", num, new Vector2((-len / 2) * 15f, (len / 2) * 15f));

        await Task.WhenAll(task1, task2);
        tmpList.Add(task1.Result);
        tmpList.Add(task2.Result);
    }


    async Task<Actor> InitISM(string model, int num, Vector2 area, float scale = 1)
    {
        int grassLen = num;
        int len = (int)Math.Sqrt(grassLen);
        var ismactor = new Actor(this.CurrentLevel, "IsmActor");
        var ismcomponent = new InstancedStaticMeshComponent(ismactor);
        ismcomponent.StaticMesh = new StaticMesh(model);

        ismcomponent.WorldLocation = new Vector3(0, 0, 0);
        for (int i = 0; i < grassLen; i++)
        {
            if (i % 10 == 0)
                await Task.Yield();
            var GrassComponent = new SubInstancedStaticMeshComponent(ismactor);
            ismcomponent.AddComponent(GrassComponent);
            GrassComponent.ParentComponent = ismcomponent;
            var x = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var y = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var yaw = Random.Shared.Next(0, 180);
            GrassComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
            GrassComponent.WorldLocation = new Vector3(x, -3, y);
            GrassComponent.WorldScale = new Vector3(scale, scale, scale);
        }

        ismcomponent.Build();
        return ismactor;
    }
    private async Task<Actor> InitHISM(string model, int num, Vector2 area, float scale = 1)
    {
        int grassLen = num;
        int len = (int)Math.Sqrt(grassLen);
        var hismactor = new Actor(CurrentLevel, "HISM Actor");
        var hismcomponent = new HierarchicalInstancedStaticMeshComponent(hismactor);
        hismactor.RootComponent = hismcomponent;
        hismcomponent.StaticMesh = new StaticMesh(model);

        hismcomponent.WorldLocation = new Vector3(0, 0, 0);
        for (int i = 0; i < grassLen; i++)
        {
            var GrassComponent = new SubInstancedStaticMeshComponent(hismactor);
            hismcomponent.AddComponent(GrassComponent);
            GrassComponent.ParentComponent = hismcomponent;
            var x = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var y = Random.Shared.Next((int)(area.X), (int)(area.Y));
            var yaw = Random.Shared.Next(0, 180);
            GrassComponent.WorldRotation = Quaternion.CreateFromYawPitchRoll(yaw, 0, 0);
            GrassComponent.WorldLocation = new Vector3(x, -3, y);
            GrassComponent.WorldScale = new Vector3(scale, scale, scale);
        }

        hismcomponent.Build();


        return hismactor;
    }


    public void Fini()
    {
        Controller?.Dispose();
    }
}
