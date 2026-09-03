using System.Text.Json;

namespace Spark.Engine.Editor;

/// <summary>编辑器项目目录布局；路径均为项目根目录下的稳定子目录。</summary>
public sealed class EditorProject
{
    public const string DescriptorExtension = ".project";

    public string RootDirectory { get; }
    public string DescriptorPath { get; }
    public string ContentDirectory => Path.Combine(RootDirectory, "Content");
    public string ConfigDirectory => Path.Combine(RootDirectory, "Config");
    public string SavedDirectory => Path.Combine(RootDirectory, "Saved");
    public string IntermediateDirectory => Path.Combine(RootDirectory, "Intermediate");
    public string BuildDirectory => Path.Combine(RootDirectory, "Build");

    private EditorProject(string rootDirectory, string? descriptorPath = null)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        DescriptorPath = descriptorPath == null
            ? Path.Combine(RootDirectory, Path.GetFileName(RootDirectory) + DescriptorExtension)
            : Path.GetFullPath(descriptorPath);
    }

    public static EditorProject Open(string rootDirectory, bool createDirectories = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var fullRoot = Path.GetFullPath(rootDirectory);
        var descriptors = Directory.Exists(fullRoot)
            ? Directory.EnumerateFiles(fullRoot, "*" + DescriptorExtension, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();
        if (descriptors.Length > 1)
            throw new InvalidDataException(
                $"Project directory '{fullRoot}' contains multiple project descriptors.");
        var descriptorPath = descriptors.SingleOrDefault();
        var project = new EditorProject(fullRoot, descriptorPath);
        if (createDirectories)
        {
            Directory.CreateDirectory(project.ContentDirectory);
            Directory.CreateDirectory(project.ConfigDirectory);
            Directory.CreateDirectory(project.SavedDirectory);
            Directory.CreateDirectory(project.IntermediateDirectory);
            Directory.CreateDirectory(project.BuildDirectory);
        }
        return project;
    }

    public static EditorProject? TryFind(string? startDirectory = null)
    {
        var directory = Path.GetFullPath(string.IsNullOrWhiteSpace(startDirectory)
            ? Directory.GetCurrentDirectory()
            : startDirectory);
        var descriptors = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*" + DescriptorExtension, SearchOption.TopDirectoryOnly).ToArray()
            : Array.Empty<string>();
        return descriptors.Length switch
        {
            0 => null,
            1 => Open(directory, createDirectories: false),
            _ => throw new InvalidDataException(
                $"Project directory '{directory}' contains multiple project descriptors."),
        };
    }

    /// <summary>创建缺失的项目描述文件；目录本身由 <see cref="Open"/> 创建。</summary>
    public void EnsureDescriptor(string? name = null)
    {
        if (File.Exists(DescriptorPath))
            return;
        var descriptor = new ProjectDescriptor
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(RootDirectory) : name,
            Content = "Content",
            Config = "Config",
            Saved = "Saved",
            Intermediate = "Intermediate",
            Build = "Build",
        };
        var json = JsonSerializer.Serialize(descriptor, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(DescriptorPath, json);
    }

    private sealed class ProjectDescriptor
    {
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Config { get; set; } = string.Empty;
        public string Saved { get; set; } = string.Empty;
        public string Intermediate { get; set; } = string.Empty;
        public string Build { get; set; } = string.Empty;
    }
}
