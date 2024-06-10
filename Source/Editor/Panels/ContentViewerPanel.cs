using Editor.Properties;
using Editor.Subsystem;
using ImGuiNET;
using SharpGLTF.Schema2;
using Spark.Engine;
using Spark.Engine.Assets;
using Spark.Engine.Editor;
using System.Runtime.InteropServices;
using File = System.IO.File;
using Material = Spark.Engine.Assets.Material;



namespace Editor.Panels;

public class ContentViewerPanel : BasePanel
{
    private readonly TextureLdr _folderTextureId;

    private readonly EditorSubsystem _editorSubsystem;
    public ContentViewerPanel(ImGuiSubSystem imGuiSubSystem) : base(imGuiSubSystem)
    {
        _editorSubsystem = Engine.GetSubSystem<EditorSubsystem>()!;


        OnChangeDir += _ => Folders.Clear();

        _folderTextureId = Engine.ImportTextureFromMemory(Resources.Asset_Folder, new TextureImportSetting());
        _folderTextureId.InitRender(Engine.GraphicsApi);

        Engine.OnFileDrop += OnFileDrop;
    }

    public override void OnOpen()
    {
        BuildFolderTree();
    }

    public void OnFileDrop(string[] paths)
    {
        var currentPath = _editorSubsystem.ContentPath;
        if (CurrentViewFolder != null)
        {
            currentPath = CurrentViewFolder.Path;
        }
        List<(AssetBase, string)> assets = [];
        foreach (var path in paths)
        {
            var extension = Path.GetExtension(path);
            if (extension == ".bmp" || extension == ".png" || extension == ".tga")
            {
                using var sr = new StreamReader(path);
                assets.Add((Engine.ImportTextureFromStream(sr, new TextureImportSetting
                {
                    IsGammaSpace = false,
                    FlipVertically = false
                }), path));
            }
            else if (extension == ".hdr")
            {
                using var sr = new StreamReader(path);
                var hdr = Engine.ImportTextureHdrFromStream(sr, new TextureImportSetting
                {
                    IsGammaSpace = false,
                    FlipVertically = false
                });
                assets.Add((hdr, path));
                var textureCube = Engine.GenerateTextureCubeFromTextureHdr(hdr);
                assets.Add((textureCube.RightFace, path)!);
                assets.Add((textureCube.LeftFace, path)!);
                assets.Add((textureCube.UpFace, path)!);
                assets.Add((textureCube.DownFace, path)!);
                assets.Add((textureCube.FrontFace, path)!);
                assets.Add((textureCube.BackFace, path)!);
                assets.Add((textureCube, path));


            }
            else if (extension == ".glb")
            {
               ImGuiSubSystem.NextFrame.Add(() => ImGui.OpenPopup("GLB导入选项#GlbImportOption"));
                ImportPath = path;
                /*
                
                */
            }
        }

        foreach(var (asset, path) in assets)
        { 
            var fileName = Path.GetFileName(path).Split(".")[0];
            var fullFileName = currentPath + "/" + fileName + ".asset";
            if (File.Exists(fullFileName))
            {
                for (var i = 1;; i++)
                {
                    if (File.Exists(currentPath + "/" + fileName + i + ".asset") == false)
                    {
                        fullFileName = currentPath + "/" + fileName + i + ".asset";
                        break;
                    }
                }
            }
            asset.Path = fullFileName.Substring(currentPath.Length + 1, fullFileName.Length - currentPath.Length - 1);
            using var sw = new StreamWriter(fullFileName);
            asset.Serialize(new BinaryWriter(sw.BaseStream), Engine);
        }
        BuildFolderTree();
    }
    Folder? CurrentViewFolder
    {
        get
        {
            var folder = _editorSubsystem.GetValue<Folder>("CurrentViewFolder");
            return folder;
        }
        set => _editorSubsystem.SetValue("CurrentViewFolder", value);
    }

    BaseFile? CurrentSelectFile 
    { 
        get => _editorSubsystem.GetValue<BaseFile>("CurrentSelectFile");
        set => _editorSubsystem.SetValue("CurrentSelectFile", value);
    }

    private Folder? _root;

    public List<Folder> Folders { get; set; } = new List<Folder>();
    private void BuildFolderTree()
    {
        _root = CreateFolder(new DirectoryInfo(_editorSubsystem.CurrentPath + "/Content"));
    }

    private Folder CreateFolder(DirectoryInfo dir, bool ignoreSubDir = false)
    {
        var folder = new Folder
        {
            Path = dir.FullName,
            Name = dir.Name,
        };
        if (ignoreSubDir == false)
        {
            foreach (var directoryInfo in dir.GetDirectories())
            {
                folder.ChildFolders.Add(CreateFolder(directoryInfo));
            }

            foreach(var file in dir.GetFiles())
            {
                folder.ChildAssetFiles.Add(CreateAssetFile(file));
            }
        }
        if (CurrentViewFolder != null)
        {
            if (ReferenceEquals(CurrentViewFolder, folder) == false && CurrentViewFolder == folder )
            {
                CurrentViewFolder = folder;
            }
        }
        return folder;
    }

    private AssetFile CreateAssetFile(FileInfo file)
    {
        var assetFile = new AssetFile
        {
            Path = file.FullName,
            Name = file.Name,
        };
        using var sr = new StreamReader(file.FullName);
        var br = new BinaryReader(sr.BaseStream);

        var magicCode  = br.ReadInt32();

        var assetType = br.ReadInt32();

        if (magicCode != MagicCode.Asset)
        {
            assetFile.AssetType = -1;
        }
        else
        {
            assetFile.AssetType = assetType;
        }

        return assetFile;


    }
    public override void Render(double deltaTime)
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
        RenderImportDialog();
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
            foreach (var file in CurrentViewFolder.ChildFolders)
            {
                var w = ImGui.GetColumnWidth();
                switch (ImGUICtl.FolderButton(file.Path, file.Name, "", _folderTextureId.TextureId, (int)w - 2* (ImGui.GetStyle().FramePadding * 2).X, CurrentSelectFile == file))
                {
                    case FileButtonAction.DoubleClick:
                        {
                            CurrentViewFolder = file;
                            CurrentSelectFile = null;
                            OnChangeDir?.Invoke(file);
                            break;
                        }
                    case FileButtonAction.Click:
                        {
                            CurrentSelectFile = file;
                            break;
                        }
                }
                ImGui.NextColumn();
            }
            foreach (var file in CurrentViewFolder.ChildAssetFiles)
            {
                var w = ImGui.GetColumnWidth();
                switch (ImGUICtl.FolderButton(file.Path, file.Name, MagicCode.GetName(file.AssetType), _folderTextureId.TextureId, (int)w - 2 * (ImGui.GetStyle().FramePadding * 2).X, CurrentSelectFile == file))
                {
                    case FileButtonAction.DoubleClick:
                        {
                            // todo openasset Panel
                            break;
                        }
                    case FileButtonAction.Click:
                        {
                            CurrentSelectFile = file;
                            break;
                        }
                }

                if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                {
                    var gcHandle = GCHandle.Alloc(file.Path, GCHandleType.Weak);
                    IntPtr ptr;
                    unsafe
                    {
                        ptr = (IntPtr)(&gcHandle);
                        ImGui.SetDragDropPayload("FILE_ASSET", ptr, (uint)sizeof(GCHandle));
                    }
                    ImGui.Text(file.Name);
                    ImGui.EndDragDropSource();
                }
                ImGui.NextColumn();
            }

        }

    }
 
   


    public void RenderDirTree()
    {
        if (_root == null)
            return;
        if (ImGui.BeginChild("#left"))
        {
            _firstChange = false;
            if (ImGui.CollapsingHeader($"All##all", ImGuiTreeNodeFlags.DefaultOpen))
            {
                RenderSubDir(_root);
            }
            ImGui.EndChild();
        }
    }

    private bool _firstChange;
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
        if (folder.ChildFolders.Count == 0)
        {
            flag |= ImGuiTreeNodeFlags.Leaf;
        }

        if (ImGui.TreeNodeEx($"{folder.Name}##{folder.Path}", flag))
        {
            if (ImGui.IsItemClicked())
            {
                if (_firstChange == false)
                {
                    _firstChange = true;
                    if (CurrentViewFolder != folder)
                    {
                        CurrentViewFolder = folder;
                        CurrentSelectFile = null;
                        OnChangeDir?.Invoke(folder);
                    }
                    rtl = true;
                }
            }

            foreach (var subFolder in folder.ChildFolders)
            {
                RenderSubDir(subFolder);
            }
            ImGui.TreePop();
        }
        if (rtl == false)
        {
            if (ImGui.IsItemClicked())
            {
                if (_firstChange == false)
                {
                    _firstChange = true;
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

    bool IsSkeletalMesh = false;
    bool ImportPhysicsAsset = false;
    string SkeletonAssetPath = string.Empty;
    string ImportPath = string.Empty;
    public void RenderImportDialog()
    {
        if (ImGui.BeginPopupModal("GLB导入选项#GlbImportOption"))
        {
            ImGui.Checkbox("骨骼网格体#SkeletalMesh", ref IsSkeletalMesh);

            if (IsSkeletalMesh == false)
                ImGui.Checkbox("导入物理资产#ImportPhysicsAsset", ref ImportPhysicsAsset);
            if (IsSkeletalMesh == true)
            {
                ImGui.InputText("骨骼网格体#Skeleton", ref SkeletonAssetPath, 256);
            }

            if (ImGui.Button("导入"))
            {
                if (IsSkeletalMesh)
                {
                    importGlbSkeletalMesh();
                }
                else
                {
                    importGlbStaticMesh();
                }
                BuildFolderTree();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("取消"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void importGlbStaticMesh()
    {
        List<TextureLdr> textures = [];
        List<Material> materials = [];
        using var sr = new StreamReader(ImportPath);
        Engine.ImporterStaticMeshFromGlbStream(sr, new StaticMeshImportSetting { ImporterPhysicsAsset = ImportPhysicsAsset }, textures, materials, out var staticMesh);

        var fileInfo = new FileInfo(ImportPath);
        var fileName = fileInfo.Name;

        fileName = fileName.Replace(fileInfo.Extension, "");

        Dictionary<AssetBase, string> assets = [];

        var currentPath = _editorSubsystem.ContentPath;
        int i = 0;
        foreach (var material in materials)
        {
            if (material.BaseColor != null)
            {
                assets.Add(material.BaseColor, fileName + "_Texture_BaseColor");
            }
            if (material.Normal != null)
            {
                assets.Add(material.Normal, fileName + "_Texture_Normal");
            }
            if (material.Arm != null)
            {
                assets.Add(material.Arm, fileName + "_Texture_Arm");
            }
            if (material.Parallax != null)
            {
                assets.Add(material.Parallax, fileName + "_Texture_Parallax");
            }

            assets.Add(material, fileName + "_Material");
        }

        assets.Add(staticMesh, fileName);

        foreach (var (asset, path) in assets)
        {
            var newPath = path;
            int index = 0;
            while (File.Exists(currentPath + "/" + newPath + ".asset") == true)
            {
                newPath = path + (++index);
            }
            asset.Path = "/" + newPath + ".asset";
            using var sw = new StreamWriter(currentPath + "/" + newPath + ".asset");
            asset.Serialize(new BinaryWriter(sw.BaseStream), _editorSubsystem.CurrentEngine);

        }

    }
    private void importGlbSkeletalMesh()
    {
        List<TextureLdr> textures = [];
        List<Material> materials = [];
        List<AnimSequence> animSequences = [];
        using var sr = new StreamReader(ImportPath);
        Engine.ImporterSkeletalMeshFromGlbStream(sr, new SkeletalMeshImportSetting { SkeletonAssetPath = SkeletonAssetPath }, textures, materials, animSequences, out var skeleton, out var skeletalMesh);

        var fileInfo = new FileInfo(ImportPath);
        var fileName = fileInfo.Name;

        fileName = fileName.Replace(fileInfo.Extension, "");

        Dictionary<AssetBase, string> assets = [];

        var currentPath = _editorSubsystem.ContentPath;
        int i = 0;
        foreach (var material in materials)
        {
            if (material.BaseColor != null)
            {
                assets.Add(material.BaseColor, fileName + "_Texture_BaseColor");
            }
            if (material.Normal != null)
            {
                assets.Add(material.Normal, fileName + "_Texture_Normal");
            }
            if (material.Arm != null)
            {
                assets.Add(material.Arm, fileName + "_Texture_Arm");
            }
            if (material.Parallax != null)
            {
                assets.Add(material.Parallax, fileName + "_Texture_Parallax");
            }

            assets.Add(material, fileName + "_Material");
        }

        assets.Add(skeleton, fileName + "_Skeleton");
        assets.Add(skeletalMesh, fileName);

        foreach (var animSequence in animSequences)
        {
            assets.Add(animSequence, fileName + "_AnimSequemce_" + animSequence.AnimName);
        }


        foreach (var (asset, path) in assets)
        {
            var newPath = path;
            int index = 0;
            while (File.Exists(currentPath + "/" + newPath + ".asset") == true)
            {
                newPath = path + (++index);
            }
            asset.Path = "/" + newPath + ".asset";
            using var sw = new StreamWriter(currentPath + "/" + newPath + ".asset");
            asset.Serialize(new BinaryWriter(sw.BaseStream), _editorSubsystem.CurrentEngine);

        }
    }

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
        public int AssetType;
        public override bool IsDirectory => false;
    }
    public class Folder : BaseFile
    {
        public List<Folder> ChildFolders = new List<Folder>();

        public List<AssetFile> ChildAssetFiles = new List<AssetFile>(); 
        public override bool IsDirectory => true;

        public bool IsSubDirOf(BaseFile other)
        {
            return Path.IndexOf(other.Path, StringComparison.Ordinal) == 0 && other.Path != Path;
        }
    }
}
