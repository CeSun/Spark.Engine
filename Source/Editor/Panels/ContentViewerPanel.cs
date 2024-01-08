using Editor.Properties;
using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Assets;
using Spark.Engine.GUI;
using System.Numerics;
using System.Runtime.InteropServices;
using static Editor.Panels.ContentViewerPanel;

namespace Editor.Panels;

public class ContentViewerPanel : ImGUIWindow
{

    Texture texture;
    EditorSubsystem EditorSubsystem;
    public ContentViewerPanel(Level level) : base(level)
    {
        EditorSubsystem = level.Engine.GetSubSystem<EditorSubsystem>();

        BuildDirectionTree();

        OnChangeDir += dir =>
        {
            Folders.Clear();
            if (dir == null)
                return;
         

        };
        texture = Texture.LoadFromMemory(Resources.Asset_Folder);
        texture.InitRender(level.Engine.Gl);
    }

    Folder? CurrentViewDir;

    Folder? CurrentSelectDir;
    Folder Root;

    public List<Folder> Folders { get; set; } = new List<Folder>();
    private void BuildDirectionTree()
    {
        Root = CreateFolder(new DirectoryInfo(Directory.GetCurrentDirectory()));
    }

    private Folder CreateFolder(DirectoryInfo dir, bool IgnoreSubDir = false)
    {
        var direction = new Folder
        {
            Path = dir.FullName,
            Name = dir.Name,
        };
        if (IgnoreSubDir == false)
        {
            foreach (var subdir in dir.GetDirectories())
            {
                direction.Children.Add(CreateFolder(subdir));
            }
        }
        return direction;
    }
    public override void Render(double DeltaTime)
    {
        CurrentViewDir = EditorSubsystem.GetValue<Folder>("CurrentViewDir");
        ImGui.Begin("Content Viewer");
        if (ImGui.Button("添加"))
        {
            if (ImGui.BeginMenu("123"))
            {
                ImGui.MenuItem("123");
            }

            
        }
        ImGui.SameLine();
        ImGui.Button("导入");
        ImGui.SameLine();
        ImGui.Button("保存所有");
        ImGui.Columns(2);
        RenderDirTree();
        ImGui.NextColumn();
        RenderFileList();
        ImGui.End();
    }
    double last_click_time = 0.0;
    public void RenderFileList()
    {
        if (ImGui.BeginChild("#right"))
        {
            if (CurrentViewDir == null)
                return;
            var width = ImGui.GetContentRegionAvail().X;
            int columnNum = (int)(width / 100f);
            if (columnNum == 0)
                columnNum = 1;
            ImGui.Columns(columnNum, "##", false);
            int i = 0;
            foreach (var folder in CurrentViewDir.Children)
            {
                var w = ImGui.GetColumnWidth();
                switch (FileButton(folder.Path,folder.Name, texture.TextureId, (int)w - 2* (ImGui.GetStyle().FramePadding * 2).X, CurrentSelectDir == folder))
                {
                    case FileButtonAction.DoubleClick:
                    {

                        EditorSubsystem.SetValue("CurrentViewDir", folder);
                            CurrentSelectDir = null;
                            OnChangeDir?.Invoke(folder);
                        break;
                    }
                    case FileButtonAction.Click:
                    {
                        CurrentSelectDir = folder;
                        Console.WriteLine("Click");
                        break;
                    }
                    default:
                        break;
                }
                /*
                Vector4 vector4 = default;
                unsafe
                {
                    vector4 = *ImGui.GetStyleColorVec4(ImGuiCol.ButtonHovered);
                }
                ;

                double current_time = ImGui.GetTime();
                if (ImGui.ImageButton("##" + folder.Path, (nint)texture.TextureId, new Vector2(w - ImGui.GetStyle().FramePadding.X * 4), Vector2.Zero, Vector2.One))
                {
                    if (current_time - last_click_time <= ImGui.GetIO().MouseDoubleClickTime)
                    {
                        EditorSubsystem.SetValue("CurrentSelectDirection", folder);
                        OnChangeDir?.Invoke(folder);
                    }
                    // 更新上一次点击的时间
                    last_click_time = current_time;
                }
                var textWidth = ImGui.CalcTextSize(folder.Name).X;
                var colum = i % columnNum;
                float centerPos = colum * w + (w - textWidth) * 0.5f;
                ImGui.SetCursorPosX(centerPos);
                ImGui.Text(folder.Name);
                */
                ImGui.NextColumn();

                i++;
            }
        }

    }
    public enum FileButtonAction
    {
        None = 0,
        Click,
        DoubleClick

    }
    public FileButtonAction FileButton(string id, string title, uint textureid, float width, bool IsSelect = false)
    {
        FileButtonAction rtl = FileButtonAction.None;
        var location = ImGui.GetCursorPos();
        var textSize = ImGui.CalcTextSize(title);
        var controlSize = new Vector2(width, width + textSize.Y + ImGui.GetStyle().ItemSpacing.Y * 2);

        ImGui.SetCursorPos(location + ImGui.GetStyle().FramePadding);
        ImGui.Image((nint)textureid, new Vector2(width -ImGui.GetStyle().FramePadding.X * 2, width -  ImGui.GetStyle().FramePadding.Y * 2));
        ImGui.SetCursorPosX(location.X + (width - textSize.X) / 2);

        ImGui.Text(title);

        ImGui.SetCursorPos(location);
        unsafe
        {
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, *ImGui.GetStyleColorVec4(ImGuiCol.Button));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, *ImGui.GetStyleColorVec4(ImGuiCol.Button));
        }
        if (IsSelect == false)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        }
        if (ImGui.Button("##" + id + "_button" + title ,controlSize))
        {
            if (ImGui.GetTime() - last_click_time <= ImGui.GetIO().MouseDoubleClickTime)
            {
                rtl = FileButtonAction.DoubleClick;
                last_click_time = 0;
            }
            else
            {
                last_click_time = ImGui.GetTime();
            }
        }
        if (ImGui.GetTime() - last_click_time > ImGui.GetIO().MouseDoubleClickTime && last_click_time != 0)
        {
            rtl = FileButtonAction.Click;
            last_click_time = 0;
        }
        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
        if (IsSelect == false)
        {
            ImGui.PopStyleColor();
        }


        return rtl;
    }


    public void RenderDirTree()
    {
        if (ImGui.BeginChild("#left"))
        {
            FirstChange = false;
            if (ImGui.CollapsingHeader($"All##all", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var dir in Root.Children)
                {
                    RenderSubDir(dir);
                }
            }
            ImGui.EndChild();
        }
    }
    bool FirstChange = false;
    public void RenderSubDir(Folder dir)
    {
        bool rtl = false;
        ImGuiTreeNodeFlags flag = ImGuiTreeNodeFlags.None ;
        if (CurrentViewDir != null && CurrentViewDir.IsSubDirOf(dir))
        {
            flag |= ImGuiTreeNodeFlags.DefaultOpen;
        }
        if (CurrentViewDir != null && CurrentViewDir.Path ==  dir.Path)
        {
            flag |= ImGuiTreeNodeFlags.Selected;
        }
        if (dir.Children.Count == 0)
        {
            flag |= ImGuiTreeNodeFlags.Leaf;
        }
        bool IsClick = false;
        if (ImGui.TreeNodeEx($"{dir.Name}##{dir.Path}", flag))
        {
            if (ImGui.IsItemClicked())
            {
                if (FirstChange == false)
                {
                    FirstChange = true;
                    if (CurrentViewDir != dir)
                    {
                        EditorSubsystem.SetValue("CurrentViewDir", dir);
                        CurrentSelectDir = null;
                        OnChangeDir?.Invoke(dir);
                    }
                    rtl = true;
                }
            }

            foreach (var Direction in dir.Children)
            {
                RenderSubDir(Direction);
            }
            ImGui.TreePop();
        }
        if (rtl == false)
        {
            if (ImGui.IsItemClicked())
            {
                if (FirstChange == false)
                {
                    FirstChange = true;
                    if (CurrentViewDir != dir)
                    {
                        EditorSubsystem.SetValue("CurrentViewDir", dir);
                        CurrentSelectDir = null;
                        OnChangeDir?.Invoke(dir);
                    }
                }
            }
        }
    }

    public event Action<Folder>? OnChangeDir;

    public class Folder
    {
        public string Path = string.Empty;
        public string Name = string.Empty;

        public List<Folder> Children = new List<Folder>()
        {
            
        };

        public static bool operator ==(Folder? left, Folder? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (ReferenceEquals(left, null))
            {
                return false;
            }
            if (ReferenceEquals(right, null))
            {
                return false;
            }
            return left.Path == right.Path;
        }
        public static bool operator !=(Folder? left, Folder? right)
        {
            return !(left == right);
        }

        public bool IsSubDirOf(Folder Other)
        {
            if (this.Path.IndexOf(Other.Path) == 0 && Other.Path != this.Path)
                return true;
            return false;
        }
        public override bool Equals(object? obj)
        {
            if (obj == null) 
                return false;
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            if ( obj is Folder dir)
            {
                return dir.Path.Equals(this.Path);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }
    }
}
