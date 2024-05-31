using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Attributes;
using Spark.Engine.GUI;
using Spark.Util;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Editor.Panels;

public class PlaceActorsPanel : ImGUIWindow
{
    readonly List<Type> _actorTypes = [];

    readonly EditorSubsystem _editorSubsystem;
    public PlaceActorsPanel(Level level) : base(level)
    {
        RefreshActors();
        _editorSubsystem = level.Engine.GetSubSystem<EditorSubsystem>();
    }


    public void RefreshActors()
    {
        foreach(var type in AssemblyHelper.GetAllType())
        {
            if (type.IsSubclassOf(typeof(Actor)))
            {
                var att = type.GetCustomAttribute<ActorInfoAttribute>();
                if (att != null)
                {
                    if (att.DisplayOnEditor == false)
                        continue;
                    if (Groups.Contains(att.Group) == false)
                    {
                        Groups.Add(att.Group);

                        ActorsTypeMap.Add(att.Group, new List<Type>());
                    }
                    var list = ActorsTypeMap[att.Group];
                    list.Add(type);

                }

                _actorTypes.Add(type);
            }
        }
        Groups.Add("All Classes");
        ActorsTypeMap.Add("All Classes", _actorTypes);

    }

    private int SelectGroup = 0;

    public List<string> Groups = new List<string>();

    public Dictionary<string, List<Type>> ActorsTypeMap = new Dictionary<string, List<Type>>();

    public bool IsFirst = true;
    public override void Render(double DeltaTime)
    {
        ImGui.Begin("Place Actors##placeactors");


        ImGui.Columns(2);
        if (IsFirst == true)
        {
            ImGui.SetColumnWidth(0, 100);
        }
        ImGui.BeginChild("123"); 
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
        float buttonWidth = ImGui.GetContentRegionAvail().X;
        Vector4 HoveredColor = default;

        unsafe
        {
            Vector4* color = ImGui.GetStyleColorVec4(ImGuiCol.ButtonHovered);

            HoveredColor = *color;
        }

        for (int i = 0; i < Groups.Count; i++)
        {
            if (SelectGroup == i)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, HoveredColor);
            }
            if(ImGui.Button(Groups[i], new Vector2(buttonWidth, 0)))
            {
                SelectGroup = i;
            }

            if (SelectGroup == i)
            {
                ImGui.PopStyleColor();
            }
        }

        ImGui.PopStyleVar();
        ImGui.EndChild();
        ImGui.NextColumn();

        for (int i = 0; i <= Groups.Count; i++) { 
            if (SelectGroup == i)
            {
                ImGui.BeginChild("##Group" + i);
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                buttonWidth = ImGui.GetContentRegionAvail().X;
                foreach (var item in ActorsTypeMap[Groups[i]])
                {
                    ImGui.PushID(item.Name);
                    ImGui.Button(item.Name, new Vector2(buttonWidth, 0));
                    if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                    {
                        var gcHandle = GCHandle.Alloc(item, GCHandleType.Weak);
                        IntPtr ptr = 0;
                        unsafe
                        {
                            ptr  = (IntPtr)(&gcHandle);
                            ImGui.SetDragDropPayload("PLACE_ACTOR_TYPE", ptr, (uint)sizeof(GCHandle));
                        }
                        ImGui.Text(item.Name);
                        ImGui.EndDragDropSource();
                    }
                    ImGui.PopID();
                }
                ImGui.PopStyleVar();
                ImGui.EndChild();

            }
        }

        ImGui.End();

        if (IsFirst == true)
            IsFirst = false;
    }
}
