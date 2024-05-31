using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Assets;
using Spark.Engine.Attributes;
using Spark.Engine.GUI;
using Spark.Engine.Platform;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using Spark.Util;
using static Editor.Panels.ContentViewerPanel;
using System.Runtime.InteropServices;

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
                var actor = EditorSubsystem.SelectedActor;
               
                var contentWidth = ImGui.GetContentRegionAvail().X;
                bool modify = false;
                ImGui.Columns(2);
                var leftWidth = contentWidth * 0.3;
                if (leftWidth < 100)
                    leftWidth = 100;
                
                if (IsFirst)
                {
                    ImGui.SetColumnWidth(0, (float)leftWidth);
                }
                var leftwidth = ImGui.GetColumnWidth() - ImGui.GetStyle().FramePadding.X * 2;
                

                ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize("Name: ").X);
                ImGui.Text("Name: ");
                ImGui.NextColumn();
                var name = actor.Name;
                var width = ImGui.GetContentRegionAvail().X;
                var labelWidth = width / 3 * 0.3f;
                var inputWidth = width / 3 * 0.7f;
                var location = actor.WorldLocation;
                ImGui.SetNextItemWidth(width);
                ImGui.InputText("##Name", ref name, 32);


                ImGui.NextColumn();
                ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize("Location：").X);
                ImGui.Text("Location：");
                ImGui.NextColumn();
                ImGui.Text("X");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##locationX", ref location.X);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    modify = true;
                }
                ImGui.SameLine();
                ImGui.Text("Y");
                ImGui.SameLine(labelWidth * 2 + inputWidth);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##locationY", ref location.Y);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    modify = true;
                }

                ImGui.SameLine();
                ImGui.SetNextItemWidth(labelWidth);
                ImGui.Text("Z");
                ImGui.SameLine(labelWidth * 3 + inputWidth * 2);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##locationZ", ref location.Z);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    modify = true;
                }
                if (modify)
                {
                    actor.WorldLocation = location;
                }



                ImGui.NextColumn();
                ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize("Rotation：").X);
                ImGui.Text("Rotation：");
                ImGui.NextColumn();


                modify = false;
                var euler = actor.WorldRotation.ToEuler();
                var yaw = euler.Y.RadiansToDegree();
                var pitch = euler.X.RadiansToDegree();
                var roll = euler.Z.RadiansToDegree();
                ImGui.Text("Yaw");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##Yaw", ref yaw);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    modify = true;
                }
                ImGui.SameLine();

                ImGui.Text("Pitch");
                ImGui.SameLine(labelWidth * 2 + inputWidth);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##Pitch", ref pitch);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    modify = true;
                }
                ImGui.SameLine();

                ImGui.Text("Roll");
                ImGui.SameLine(labelWidth * 3 + inputWidth * 2);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##Roll", ref roll);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    modify = true;
                }

                if (modify)
                {
                    actor.WorldRotation = Quaternion.CreateFromYawPitchRoll(yaw.DegreeToRadians(), pitch.DegreeToRadians(), roll.DegreeToRadians());
                }
                ImGui.NextColumn();
                ImGui.SetCursorPosX(leftwidth - ImGui.CalcTextSize("Scale：").X);
                ImGui.Text("Scale："); 
                ImGui.NextColumn();
                

                
               

                modify = false;
                var scale = actor.WorldScale;
                
                ImGui.Text("X");
                ImGui.SameLine(labelWidth);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##scaleX", ref scale.X); 
                if (ImGui.IsItemDeactivatedAfterEdit())
                    modify = true;
                ImGui.SameLine();
                
                ImGui.Text("Y");
                ImGui.SameLine(labelWidth * 2 + inputWidth);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##scaleY", ref scale.Y);
                if (ImGui.IsItemDeactivatedAfterEdit())
                    modify = true;
                ImGui.SameLine();
                
                ImGui.Text("Z");
                ImGui.SameLine(labelWidth * 3 + inputWidth * 2);
                ImGui.SetNextItemWidth(inputWidth);
                ImGui.InputFloat("##scaleZ", ref scale.Z);
                if (ImGui.IsItemDeactivatedAfterEdit())
                    modify = true;
                if (modify)
                    actor.WorldScale = scale;

                RenderObject(actor);


                foreach(var comp in actor.PrimitiveComponents)
                {
                    RenderObject(comp);
                }
               

            }

            ImGui.End();


            if (IsFirst)
                IsFirst = false;

        }

        public void RenderObject(Object obj)
        {
            List<(PropertyAttribute, PropertyInfo)> properties = new List<(PropertyAttribute, PropertyInfo)>();
            var type = obj.GetType();
            foreach (PropertyInfo property in type.GetProperties())
            {
                var att = property.GetCustomAttribute<PropertyAttribute>();
                if (att != null && att.IsDisplay)
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
        private Vector3 _tempColor;
        public void RenderProperty(PropertyAttribute attr, PropertyInfo property, Object obj)
        {
            var leftWidth = ImGui.GetColumnWidth() - ImGui.GetStyle().FramePadding.X * 2;
            var name = attr.DisplayName;
            if (string.IsNullOrEmpty(name))
                name = property.Name;
            ImGui.SetCursorPosX(leftWidth - ImGui.CalcTextSize(name + "：").X);
            ImGui.Text(name + "：");
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
                var data = (int)(property.GetValue(obj) ?? 0);
                ImGui.InputInt("##" + obj.GetHashCode() + name, ref data, 0, 0, flag);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }

            }
            else if (property.PropertyType == typeof(uint))
            {
                var data = (uint)(property.GetValue(obj) ?? 0);
                unsafe
                {
                    ImGui.InputScalar("##" + obj.GetHashCode() + name, ImGuiDataType.U32, (nint)(&data), 0, 0, "", flag);
                }
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }
            }
            if (property.PropertyType == typeof(long))
            {
                var data = (long)(property.GetValue(obj) ?? 0);
                unsafe
                {
                    ImGui.InputScalar("##" + obj.GetHashCode() + name, ImGuiDataType.S64, (nint)(&data), 0, 0, "", flag);
                }
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }

            }
            else if (property.PropertyType == typeof(ulong))
            {
                var data = (ulong)(property.GetValue(obj) ?? 0);
                unsafe
                {
                    ImGui.InputScalar("##" + obj.GetHashCode() + name, ImGuiDataType.U64, (nint)(&data), 0, 0, "", flag);
                }
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }
            }
            else if (property.PropertyType == typeof(float))
            {
                var data = (float)(property.GetValue(obj) ?? 0f);
                ImGui.InputFloat("##" + obj.GetHashCode() + name, ref data, 0,0, null, flag);
                if (ImGui.IsItemDeactivatedAfterEdit())
                {
                    property.SetValue(obj, data);
                }
            }
            else if (property.PropertyType == typeof(double))
            {
                var data = (double)(property.GetValue(obj) ?? 0.0);
                ImGui.InputDouble("##" + obj.GetHashCode() + name, ref data, 0, 0, null, flag);
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
                var data = (Color)(property.GetValue(obj) ?? Color.White);
                _tempColor = new Vector3(data.R / 255f, data.G / 255f, data.B / 255f);
                ImGui.ColorEdit3("##" + obj.GetHashCode() + name,  ref _tempColor);
                if (ImGui.IsItemEdited() && isReadOnly == false)
                {
                    property.SetValue(obj, Color.FromArgb(255, (int)(_tempColor.X * 255), (int)(_tempColor.Y * 255), (int)(_tempColor.Z * 255)));
                    data = (Color)(property.GetValue(obj) ?? Color.White);
                    _tempColor = new Vector3(data.R / 255f, data.G / 255f, data.B / 255f);
                }
            }
            else if (property.PropertyType == typeof(string))
            {
                var data = property.GetValue(obj);
                string str = "";
                if (data != null)
                    str = (string)data;
                ImGui.InputText("##" + obj.GetHashCode() + name, ref str, 256, flag);
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

                bool isModifyInput = false;
                ImGui.PushFont(Level.ImGuiWarp.Fonts["forkawesome"]);
                var buttonWidth = ImGui.CalcTextSize([(char)0x00f060, (char)0x00f002]).X + ImGui.GetStyle().ItemSpacing.X * 3 + ImGui.GetStyle().FramePadding.X * 4;
                ImGui.PopFont();
                ImGui.SetNextItemWidth(width - buttonWidth);
                ImGui.InputText("##"  + obj.GetHashCode() + name, ref path, 256, flag);
                var assetMagicCodeProperty = property.PropertyType.GetProperty("AssetMagicCode");
                if (assetMagicCodeProperty != null && assetMagicCodeProperty.GetMethod != null)
                {
                    var magicCode = (int)(assetMagicCodeProperty.GetMethod.Invoke(null, null) ?? 0);
                    if (magicCode != 0)
                    {
                        if (ImGui.BeginDragDropTarget())
                        {
                            var payLoad = ImGui.AcceptDragDropPayload("FILE_" + MagicCode.GetName(magicCode).ToUpper());
                            unsafe
                            {
                                if (payLoad.NativePtr != null)
                                {
                                    var gcHandle = Marshal.PtrToStructure<GCHandle>(payLoad.Data);

                                    if (gcHandle.Target != null)
                                    {
                                        var filePath = (string)gcHandle.Target;
                                        var myPath = filePath.Replace("\\", "/");
                                        myPath = myPath.Substring(EditorSubsystem.CurrentPath.Length + 1, myPath.Length - EditorSubsystem.CurrentPath.Length - 1);
                                        path = myPath;
                                        isModifyInput = true;
                                    }
                                }
                            }
                            ImGui.EndDragDropTarget();
                        }
                    }
                }
                

                ImGui.SameLine();
               

                ImGui.PushFont(Level.ImGuiWarp.Fonts["forkawesome"]);
                if(ImGui.Button(new string([(char)0x00f060]) + "##set_" + property.DeclaringType!.FullName + "_"+property.Name))
                {
                    var file = EditorSubsystem.GetValue<AssetFile>("CurrentSelectFile");
                    if (file != null)
                    {
                        var myPath = file.Path.Replace("\\", "/");
                        myPath = myPath.Substring(EditorSubsystem.CurrentPath.Length + 1, myPath.Length - EditorSubsystem.CurrentPath.Length - 1);
                        path = myPath;
                        isModifyInput = true;
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
                    isModifyInput = true;
                    
                }
                if (isModifyInput && IFileSystem.Instance.FileExits(path))
                {
                    if (path == "")
                    {
                        property.SetValue(obj, null);
                    }
                    else
                    {
                        try
                        {
                            asset = this.Level.Engine.AssetMgr.Load(property.PropertyType, path);
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
                    if (values.GetValue(i)!.Equals(data))
                    {
                        current = i;
                        break;
                    }
                }
                if(ImGui.Combo("##" + obj.GetHashCode() + name, ref current, names, names.Length))
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
