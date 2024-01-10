using Editor.Properties;
using Editor.Subsystem;
using ImGuiNET;
using Spark.Engine;
using Spark.Engine.Assets;
using Spark.Engine.GUI;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using static Editor.Panels.ContentViewerPanel;

namespace Editor.Panels;

public class ContentViewerPanel : ImGUIWindow
{

    Texture FolderTextureId;

    EditorSubsystem EditorSubsystem;
    public ContentViewerPanel(Level level) : base(level)
    {
        EditorSubsystem = level.Engine.GetSubSystem<EditorSubsystem>();

        BuildFolderTree();

        OnChangeDir += dir =>
        {
            Folders.Clear();
            if (dir == null)
                return;
         

        };
        FolderTextureId = Texture.LoadFromMemory(Resources.Asset_Folder);
        FolderTextureId.InitRender(level.Engine.Gl);
    }

    Folder? CurrentViewFolder
    {
        get
        {
            var folder = EditorSubsystem.GetValue<Folder>("CurrentViewFolder");
            CurrentViewFolderCache = folder;
            return folder;
        }
        set => EditorSubsystem.SetValue("CurrentViewFolder", value);
    }
    Folder? CurrentViewFolderCache;

    Folder? CurrentSelectFile 
    { 
        get => EditorSubsystem.GetValue<Folder>("CurrentSelectFile");
        set => EditorSubsystem.SetValue("CurrentSelectFile", value);
    }
    Folder Root;

    public List<Folder> Folders { get; set; } = new List<Folder>();
    private void BuildFolderTree()
    {
        Root = CreateFolder(new DirectoryInfo(Directory.GetCurrentDirectory()));
    }

    private Folder CreateFolder(DirectoryInfo dir, bool IgnoreSubDir = false)
    {
        var foler = new Folder
        {
            Path = dir.FullName,
            Name = dir.Name,
        };
        if (IgnoreSubDir == false)
        {
            foreach (var subdir in dir.GetDirectories())
            {
                foler.Children.Add(CreateFolder(subdir));
            }

            foreach(var file in dir.GetFiles())
            {
                foler.Children.Add(CreateAssetFile(file));
            }
        }
        return foler;
    }

    private AssetFile CreateAssetFile(FileInfo File)
    {
        return new AssetFile
        {
            Path = File.FullName,
            Name = File.Name,
        };

        

    }
    public override void Render(double DeltaTime)
    {
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
    public void RenderFileList()
    {
        if (ImGui.BeginChild("#right"))
        {
            if (CurrentViewFolder == null)
                return;
            var width = ImGui.GetContentRegionAvail().X;
            int columnNum = (int)(width / 100f);
            if (columnNum == 0)
                columnNum = 1;
            ImGui.Columns(columnNum, "##", false);
            int i = 0;
            foreach (var file in CurrentViewFolder.Children)
            {
                
                var w = ImGui.GetColumnWidth();
                switch (ImGUICtl.FolderButton(file.Path, file.Name, "123", FolderTextureId.TextureId, (int)w - 2* (ImGui.GetStyle().FramePadding * 2).X, CurrentSelectFile == file))
                {
                    case FileButtonAction.DoubleClick:
                        {
                            if (file is Folder folder)
                            {
                                CurrentViewFolder = folder;
                                CurrentSelectFile = null;
                                OnChangeDir?.Invoke(folder);
                            }
                            break;
                        }
                    case FileButtonAction.Click:
                    {
                            if (file is Folder folder)
                            {
                                CurrentSelectFile = folder;
                            }
                            break;
                    }
                    default:
                        break;
                }
                ImGui.NextColumn();

                i++;
            }
        }

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
                    if (dir is Folder foler)
                    {
                        RenderSubDir(foler);
                    }
                }
            }
            ImGui.EndChild();
        }
    }
    bool FirstChange = false;
    public void RenderSubDir(Folder folder)
    {
        bool rtl = false;
        ImGuiTreeNodeFlags flag = ImGuiTreeNodeFlags.None ;
        if (CurrentViewFolder != null && CurrentViewFolder.IsSubDirOf(folder))
        {
            flag |= ImGuiTreeNodeFlags.DefaultOpen;
        }
        if (CurrentViewFolder != null && CurrentViewFolder.Path ==  folder.Path)
        {
            flag |= ImGuiTreeNodeFlags.Selected;
        }
        if (folder.Children.Count == 0)
        {
            flag |= ImGuiTreeNodeFlags.Leaf;
        }
        bool IsClick = false;
        if (ImGui.TreeNodeEx($"{folder.Name}##{folder.Path}", flag))
        {
            if (ImGui.IsItemClicked())
            {
                if (FirstChange == false)
                {
                    FirstChange = true;
                    if (CurrentViewFolder != folder)
                    {
                        CurrentViewFolder = folder;
                        CurrentSelectFile = null;
                        OnChangeDir?.Invoke(folder);
                    }
                    rtl = true;
                }
            }

            foreach (var directory in folder.Children)
            {
                if (directory is Folder foler)
                {
                    RenderSubDir(foler);
                }
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
                    if (CurrentViewFolder != folder)
                    {
                        CurrentViewFolder = folder;
                        CurrentSelectFile = null;
                        OnChangeDir?.Invoke(folder);
                    }
                }
            }
        }
    }

    public event Action<Folder>? OnChangeDir;


    public class BaseFile
    {
        public string Path = string.Empty;
        public string Name = string.Empty;
        public virtual bool IsDirectory { get;} = false;
        public static bool operator ==(BaseFile? left, BaseFile? right)
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
        public static bool operator !=(BaseFile? left, BaseFile? right)
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

            if (obj is Folder dir)
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

    public class AssetFile : BaseFile
    {
        public override bool IsDirectory => false;
    }
    public class Folder : BaseFile
    {

        public List<BaseFile> Children = new List<BaseFile>()
        {
            
        };

        public override bool IsDirectory => true;

        public bool IsSubDirOf(BaseFile Other)
        {
            if (this.Path.IndexOf(Other.Path) == 0 && Other.Path != this.Path)
                return true;
            return false;
        }
    }
}
