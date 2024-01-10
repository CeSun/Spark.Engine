using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Actors;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.Components;
using Spark.Engine.GUI;
using Spark.Engine.Platform;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static Editor.Panels.ContentViewerPanel;

namespace Editor.Panels
{
    public class DetailsPanel : ImGUIWindow
    {
        EditorSubsystem EditorSubsystem;

        public bool IsFirst = true;
        public DetailsPanel(Level level) : base(level)
        {
            var system = level.Engine.GetSubSystem<EditorSubsystem>();
            if (system != null)
                EditorSubsystem = system;
            else
                throw new Exception("no editor subsystem");
           
        }
        public override void Render(double deltaTime)
        {

            ImGui.Begin("Details##details");
            if (EditorSubsystem.SelectedActor != null)
            {
                var Actor = EditorSubsystem.SelectedActor;
               
               

                var ContentWidth = ImGui.GetContentRegionAvail().X;
                bool Modify = false;
                ImGui.Columns(2);
                var leftWidth = ContentWidth * 0.3;
                if (leftWidth < 100)
                    leftWidth = 100;
                
                if (IsFirst == true)
                {
                    ImGui.SetColumnWidth(0, (float)leftWidth);
                }
                var leftwidth = ImGui.GetColumnWidth() - ImGui.GetStyle().FramePadding.X * 2;
                

                ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize("Name: ").X);
                ImGui.Text("Name: ");
                ImGui.NextColumn();
                var Name = Actor.Name;
                var width = ImGui.GetContentRegionAvail().X;
                var labelWidth = width / 3 * 0.3f;
                var InputWidth = width / 3 * 0.7f;
                var location = Actor.WorldLocation;
                ImGui.SetNextItemWidth(width);
                ImGui.InputText("##Name", ref Name, 32);


                ImGui.NextColumn();
                ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize("Location：").X);
                ImGui.Text("Location：");
                ImGui.NextColumn();
                ImGui.Text("X");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##locationX", ref location.X);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    Modify = true;
                }
                ImGui.SameLine(); ;
                ImGui.Text("Y");
                ImGui.SameLine(labelWidth * 2 + InputWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##locationY", ref location.Y);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    Modify = true;
                }

                ImGui.SameLine();
                ImGui.SetNextItemWidth(labelWidth);
                ImGui.Text("Z");
                ImGui.SameLine(labelWidth * 3 + InputWidth * 2);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##locationZ", ref location.Z);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    Modify = true;
                }
                if (Modify == true)
                {
                    Actor.WorldLocation = location;
                }



                ImGui.NextColumn();
                ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize("Rotation：").X);
                ImGui.Text("Rotation：");
                ImGui.NextColumn();



                float Yaw = 0, Pitch = 0, Roll = 0;
                ImGui.Text("Yaw");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##Yaw", ref Yaw);
                ImGui.SameLine();

                ImGui.Text("Pitch");
                ImGui.SameLine(labelWidth * 2 + InputWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##Pitch", ref Pitch);
                ImGui.SameLine();

                ImGui.Text("Roll");
                ImGui.SameLine(labelWidth * 3 + InputWidth * 2);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##Roll", ref Roll);


                ImGui.NextColumn();
                ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize("Scale：").X);
                ImGui.Text("Scale："); 
                ImGui.NextColumn();
                

                
               

                Modify = false;
                var scale = Actor.WorldScale;
                
                ImGui.Text("X");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##scaleX", ref scale.X); 
                if (ImGui.IsItemDeactivatedAfterEdit())
                    Modify = true;
                ImGui.SameLine();
                
                ImGui.Text("Y");
                ImGui.SameLine(labelWidth * 2 + InputWidth);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##scaleY", ref scale.Y);
                if (ImGui.IsItemDeactivatedAfterEdit())
                    Modify = true;
                ImGui.SameLine();
                
                ImGui.Text("Z");
                ImGui.SameLine(labelWidth * 3 + InputWidth * 2);
                ImGui.SetNextItemWidth(InputWidth);
                ImGui.InputFloat("##scaleZ", ref scale.Z);
                if (ImGui.IsItemDeactivatedAfterEdit())
                    Modify = true;
                if (Modify)
                    Actor.WorldScale = scale;

                RenderObject(Actor);


                foreach(var comp in Actor.PrimitiveComponents)
                {
                    RenderObject(comp);
                }
               

            }

            ImGui.End();


            if (IsFirst == true)
                IsFirst = false;

        }

        public void RenderObject(Object obj)
        {
            List<(PropertyAttribute, PropertyInfo)> properties = new List<(PropertyAttribute, PropertyInfo)>();
            var type = obj.GetType();
            foreach (PropertyInfo property in type.GetProperties())
            {
                var att = property.GetCustomAttribute<PropertyAttribute>();
                if (att != null && att.IsDispaly == true)
                {
                    properties.Add((att, property));
                }
            }
            if (properties.Count > 0)
            {
                ImGui.Columns(1);
                var width = ImGui.GetContentRegionAvail().X;
                ImGui.SetNextItemWidth(width);
                // ImGui.Text(type.Name + "");

                if(ImGui.CollapsingHeader(type.Name, ImGuiTreeNodeFlags.DefaultOpen))
                {

                    ImGui.Columns(2);
                    foreach (var (att, property) in properties)
                    {
                        RenderProperty(att, property, obj);
                    }
                }
            }
        }
        private Vector3 tempColor;
        public void RenderProperty(PropertyAttribute attr, PropertyInfo property, Object obj)
        {
            var leftwidth = ImGui.GetColumnWidth() - ImGui.GetStyle().FramePadding.X * 2;
            var Name = attr.DisplayName;
            if (string.IsNullOrEmpty(Name))
                Name = property.Name;
            ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize(Name + "：").X);
            ImGui.Text(Name + "：");
            ImGui.NextColumn();

            bool isReadOnly = attr.IsReadOnly;
            if (property.SetMethod == null)
                isReadOnly = true;

            var width = ImGui.GetContentRegionAvail().X;
            ImGui.SetNextItemWidth(width);

            var flag = ImGuiInputTextFlags.None;
            if (isReadOnly)
                flag = ImGuiInputTextFlags.ReadOnly;
            
            if (property.PropertyType == typeof(int))
            {
                var data = (int)property.GetValue(obj);
                ImGui.InputInt("##" + obj.GetHashCode() + Name, ref data, 0, 0, flag);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }

            }
            else if (property.PropertyType == typeof(uint))
            {
                var data = (uint)property.GetValue(obj);
                unsafe
                {
                    ImGui.InputScalar("##" + obj.GetHashCode() + Name, ImGuiDataType.U32, (nint)(&data), 0, 0, "", flag);
                }
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }
            }
            if (property.PropertyType == typeof(long))
            {
                var data = (long)property.GetValue(obj);
                unsafe
                {
                    ImGui.InputScalar("##" + obj.GetHashCode() + Name, ImGuiDataType.S64, (nint)(&data), 0, 0, "", flag);
                }
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }

            }
            else if (property.PropertyType == typeof(ulong))
            {
                var data = (ulong)property.GetValue(obj);
                unsafe
                {
                    ImGui.InputScalar("##" + obj.GetHashCode() + Name, ImGuiDataType.U64, (nint)(&data), 0, 0, "", flag);
                }
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }
            }
            else if (property.PropertyType == typeof(float))
            {
                var data = (float)property.GetValue(obj);
                ImGui.InputFloat("##" + obj.GetHashCode() + Name, ref data, 0,0, null, flag);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }
            }
            else if (property.PropertyType == typeof(double))
            {
                var data = (double)property.GetValue(obj);
                ImGui.InputDouble("##" + obj.GetHashCode() + Name, ref data, 0, 0, null, flag);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }
            }
            else if (property.PropertyType == typeof(Vector2))
            {
            }
            else if (property.PropertyType == typeof(Vector3))
            {
            }
            else if (property.PropertyType == typeof(Vector4))
            {
            }
            else if (property.PropertyType == typeof(Color))
            {
                var data = (Color)property.GetValue(obj);
                tempColor = new Vector3(data.R / 255f, data.G / 255f, data.B / 255f);
                ImGui.ColorEdit3("##" + obj.GetHashCode() + Name,  ref tempColor);
                if (ImGui.IsItemEdited() && isReadOnly == false)
                {
                    property.SetValue(obj, Color.FromArgb(255, (int)(tempColor.X * 255), (int)(tempColor.Y * 255), (int)(tempColor.Z * 255)));
                    data = (Color)property.GetValue(obj);
                    tempColor = new Vector3(data.R / 255f, data.G / 255f, data.B / 255f);
                }
            }
            else if (property.PropertyType == typeof(string))
            {
                var data = property.GetValue(obj);
                string str = "";
                if (data != null)
                    str = (string)data;
                ImGui.InputText("##" + obj.GetHashCode() + Name, ref str, 256, flag);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, str);
                }
            }
            else if (property.PropertyType.IsSubclassOf(typeof(AssetBase)))
            {
                AssetBase? asset = null;

                var data = property.GetValue(obj);
                if (data != null)
                {
                    asset = (AssetBase)data;
                }

                var path = "";
                if (asset != null)
                {
                    path = asset.Path;
                }

                bool IsModifyInput = false;
                ImGui.PushFont(level.ImGuiWarp.Fonts["forkawesome"]);
                var ButtonWidth = ImGui.CalcTextSize([(char)0x00f060, (char)0x00f002]).X + ImGui.GetStyle().ItemSpacing.X * 3 + ImGui.GetStyle().FramePadding.X * 4;
                ImGui.PopFont();
                ImGui.SetNextItemWidth(width - ButtonWidth);
                ImGui.InputText("##"  + obj.GetHashCode() + Name, ref path, 256, flag);
                ImGui.SameLine();
               

                ImGui.PushFont(level.ImGuiWarp.Fonts["forkawesome"]);
                if(ImGui.Button(new string([(char)0x00f060]) + "##set_" + property.DeclaringType.FullName + "_"+property.Name))
                {
                    var file = EditorSubsystem.GetValue<AssetFile>("CurrentSelectFile");
                    if (file != null)
                    {
                        var MyPath = file.Path.Replace("\\", "/");
                        MyPath = MyPath.Substring(EditorSubsystem.CurrentPath.Length + 1, MyPath.Length - EditorSubsystem.CurrentPath.Length - 1);
                        path = MyPath;
                        IsModifyInput = true;
                    }
                    // todo： 当前选中的资源设置到输入框中
                }
                ImGui.SameLine();
                if(ImGui.Button(new string([(char)0x00f002]) + "##set_" + property.DeclaringType.FullName + "_" + property.Name))
                {
                    // todo: 在文件浏览器里查看输入框里的资源
                }
                ImGui.PopFont();
               
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    IsModifyInput = true;
                    
                }
                if (IsModifyInput && FileSystem.Instance.FileExits(path))
                {
                    if (path == "")
                    {
                        property.SetValue(obj, null);
                    }
                    else
                    {
                        try
                        {
                            asset = this.level.Engine.AssetMgr.Load(property.PropertyType, path);
                            property.SetValue(obj, asset);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                            Console.WriteLine(e.StackTrace);
                        }
                    }
                }
            }
            else if (property.PropertyType.IsEnum)
            {
                var data = property.GetValue(obj);
                var names = Enum.GetNames(property.PropertyType);
                var values = Enum.GetValues(property.PropertyType);
                int current = 0;
                for(int i = 0; i < values.Length; i++)
                {
                    if (values.GetValue(i).Equals(data))
                    {
                        current = i;
                        break;
                    }
                }
                if(ImGui.Combo("##" + obj.GetHashCode() + Name, ref current, names, names.Length))
                {
                    if (isReadOnly == false)
                    {
                        property.SetValue(obj, values.GetValue(current));
                    }
                }

            }

            ImGui.NextColumn();
        }





    }
}
