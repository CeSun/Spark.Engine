using ImGuiNET;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteEngine.Core.Edit
{
    public class EditUI :UI
    {
        public EditUI(Texture texture)
        {
            renderTexture = texture;
        }

        Texture renderTexture;
        public override void Init()
        {

        }

        public override void Update()
        {

        }

        private  void VisitSub(GameObject obj)
        {
            if (obj.ChildernCount > 0)
            {
                if (ImGui.TreeNode(obj.Name))
                {
                    obj.Foreach(item => VisitSub(item));
                    ImGui.TreePop();
                }
            }
            else
            {
                if (ImGui.Selectable(obj.Name, selectItem == obj))
                {
                    selectItem = obj;
                }
            }
        }

        private GameObject? selectItem;
        public override void Draw(double deltaTime)
        {
            var width = Game.Instance.Size.X / 4;
            var height = (int)(Game.Instance.Size.Y * 0.7);

            ImGui.Begin("Scene", ImGuiWindowFlags.NoResize| ImGuiWindowFlags.NoMove | ImGuiWindowFlags.UnsavedDocument | ImGuiWindowFlags.NoCollapse);
            ImGui.SetWindowPos(new System.Numerics.Vector2(0, 0));
            ImGui.SetWindowSize(new System.Numerics.Vector2(width, height));
            
            VisitSub(Scene.Current.Root);
            ImGui.End();


            ImGui.Begin("Property", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.UnsavedDocument | ImGuiWindowFlags.NoCollapse);
            ImGui.SetWindowPos(new System.Numerics.Vector2(Game.Instance.Size.X  - width, 0));
            ImGui.SetWindowSize(new System.Numerics.Vector2(width, height));


            if (selectItem != null)
            {
                ImGui.Text("Name: " + selectItem.Name);
                ImGui.Text($"Postion: {selectItem.LocalPosition.X.ToString("f3")},{selectItem.LocalPosition.Y.ToString("f3")},{selectItem.LocalPosition.Z.ToString("f3")}");
                ImGui.Text($"Rotation: {selectItem.LocalRotation.X.ToString("f3")},{selectItem.LocalRotation.Y.ToString("f3")},{selectItem.LocalRotation.Z.ToString("f3")}");
                ImGui.Text($"Scale: {selectItem.LocalScale.X.ToString("f3")},{selectItem.LocalScale.Y.ToString("f3")},{selectItem.LocalScale.Z.ToString("f3")}");
                ImGui.Text($"Layer: {selectItem.Layer}");

                if (selectItem is Camera camera)
                {
                    ImGui.Text($"RenderLayer: {camera.Layers}");
                }

                ImGui.NewLine();
                selectItem.ForeachComponent(com => ImGui.Selectable(com.GetType().Name));
            }

            ImGui.End();


            ImGui.Begin("Resource", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.UnsavedDocument | ImGuiWindowFlags.NoCollapse);
            ImGui.SetWindowPos(new System.Numerics.Vector2(0, height));
            ImGui.SetWindowSize(new System.Numerics.Vector2(Game.Instance.Size.X,  Game.Instance.Size.Y - height));
          
            ImGui.End();

            ImGui.Begin("Game", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.UnsavedDocument | ImGuiWindowFlags.NoCollapse);
            ImGui.SetWindowPos(new System.Numerics.Vector2(width, 0));
            ImGui.SetWindowSize(new System.Numerics.Vector2(Game.Instance.Size.X - 2 * width, height));
            ImGui.Image((IntPtr)renderTexture.Id, new System.Numerics.Vector2(Game.Instance.Size.X - 2 * width - 15, (int)(height - 40)), new System.Numerics.Vector2 (0,1), new System.Numerics.Vector2(1, 0));

            ImGui.End();
            Game.Instance.GameSize = new Vector2i(Game.Instance.Size.X - 2 * width - 15, (int)(height - 40));

        }
    }
}
