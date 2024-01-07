using Editor.Properties;
using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Assets;
using Spark.Engine.GUI;
using System.Numerics;
using System.Runtime.InteropServices;

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
        CurrentSelectDir = EditorSubsystem.GetValue<Folder>("CurrentSelectDirection");
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
            if (CurrentSelectDir == null)
                return;
            var width = ImGui.GetContentRegionAvail().X;
            int columnNum = (int)(width / 100f);

            ImGui.Columns(columnNum, "##", false);

            foreach (var folder in CurrentSelectDir.Children)
            {
                Vector4 vector4 = default;
                unsafe
                {
                    vector4 = *ImGui.GetStyleColorVec4(ImGuiCol.ButtonHovered);
                }
                double current_time = ImGui.GetTime();
                if (ImGui.ImageButton("##" + folder.Path, (nint)texture.TextureId, new Vector2(80f)))
                {
                    if (current_time - last_click_time <= ImGui.GetIO().MouseDoubleClickTime)
                    {
                        Console.WriteLine(ImGui.GetIO().MouseDoubleClickTime);
                        Console.WriteLine(current_time - last_click_time);
                        EditorSubsystem.SetValue("CurrentSelectDirection", folder);
                        OnChangeDir?.Invoke(folder);
                    }
                    // 更新上一次点击的时间
                    last_click_time = current_time;

                }

                var ButtonWidth = ImGui.GetItemRectSize().x;

                var textWidth = ImGui.CalcTextSize(folder.Name).X;

                float centerPos = (ButtonWidth - textWidth) * 0.5f;

                ImGui.SetCursorPosX(centerPos);
                ImGui.Text(folder.Name);
                ImGui.NextColumn();
            }
        }

    }

    public void RenderDirTree()
    {
        FirstChange = false;
        if (ImGui.CollapsingHeader($"All##all", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var dir in Root.Children)
            {
                RenderSubDir(dir);
            }
        }
    }
    bool FirstChange = false;
    public void RenderSubDir(Folder dir)
    {
        bool rtl = false;
        ImGuiTreeNodeFlags flag = ImGuiTreeNodeFlags.None ;
        if (CurrentSelectDir != null && CurrentSelectDir.Path ==  dir.Path)
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
                    if (CurrentSelectDir != dir)
                    {
                        EditorSubsystem.SetValue("CurrentSelectDirection", dir);
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
                    if (CurrentSelectDir != dir)
                    {
                        EditorSubsystem.SetValue("CurrentSelectDirection", dir);
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
