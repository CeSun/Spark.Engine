using ImGuiNET;
using Spark.Engine.GUI;
using Spark.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Editor.Panels;
using System.Runtime.Loader;
using System.Reflection;

namespace Editor
{
    public class EditorImguiContext : ImGUIContext
    {

        List<IPanel> Panels = new List<IPanel>();
        public EditorImguiContext(Level level) : base(level)
        {
            ref var flags = ref ImGui.GetIO().ConfigFlags;
            flags |= ImGuiConfigFlags.DockingEnable;

            AddAllPanel();



        }


        public void AddAllPanel()
        {
            foreach (var ctx in AssemblyLoadContext.All)
            {
                foreach (var assembly in ctx.Assemblies)
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        var att = type.GetCustomAttribute<AddPanelToEditorAttribute>();

                        if (att != null)
                        {
                            var obj = (IPanel)Activator.CreateInstance(type);
                            if (obj != null)
                            {
                                Panels.Add(obj);
                            }

                        }
                    }
                }
            }
        }
        public override void Render(double deltaTime)
        {
            Panels.ForEach(panel => panel.Renderer(deltaTime));
        }
    }

}
