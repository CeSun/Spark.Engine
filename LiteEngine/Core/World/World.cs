using LiteEngine.Core.Actors;
using LiteEngine.Core.Components;
using LiteEngine.Core.Render;
using LiteEngine.Sdk;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core;

public class World
{
    private List<Actor> Actors;

    private List<Actor> AddActors;
    private List<Actor> DelActors;

    private Dictionary<RenderLayer, List<RenderableComponent>> RenderLayers;

    IGame GameDll { get => Engine.Instance.GameDll; }

    public Skybox? Skybox;
    public void AddActor(Actor actor)
    {
        AddActors.Add(actor);
    }

    public void AddComponentToLayer(RenderableComponent com, RenderLayer layer)
    {
        RenderLayers[layer].Add(com);
    }

    public void RemoveComponentFromLayer(RenderableComponent com, RenderLayer layer)
    {
        RenderLayers[layer].Remove(com);
    }

    public void ForeachLayer(Action<RenderableComponent> action, RenderLayer renderLayer)
    {
        RenderLayers[renderLayer].ForEach(action); 
    }
    public World ()
    {
        Actors = new List<Actor> ();
        AddActors = new List<Actor> ();
        DelActors = new List<Actor> ();
        RenderLayers = new Dictionary<RenderLayer, List<RenderableComponent>>(); //<List<RenderableComponent>>();
        for(var i = 0; i < (int)RenderLayer.Max; i++)
        {
            RenderLayers.Add((RenderLayer)(1 << i) , new List<RenderableComponent> ());
        }
        LoadLevel("");
    }
    
    public void LoadLevel(string path)
    {
        Skybox = new Skybox(path);
        
    }


    
    public void Init()
    {
        Skybox?.Init();
        GameDll.OnInit();
    }

    public void Fini()
    {
        Skybox?.Fini();
        GameDll.OnFini();

    }


    public void DestoryActor(Actor actor)
    {
        DelActors.Add(actor);
    }

    public void Render()
    {
        CameraComponent.RenderAllCamera();
    }

    public void Update(float deltaTime)
    {
        AddActors.ForEach(actor => Actors.Add(actor));
        DelActors.ForEach(actor => Actors.Remove(actor));
        AddActors.Clear();
        DelActors.Clear();
        Actors.ForEach(actor => actor.Update(deltaTime));
        Skybox?.Update(deltaTime);
        GameDll.OnUpdate(deltaTime);
    }

    public GL gl { get => Engine.Instance.Gl; }
}
