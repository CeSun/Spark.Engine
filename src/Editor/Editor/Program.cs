// See https://aka.ms/new-console-template for more information
using Spark.Engine.Util.Importers;

Console.WriteLine("Hello, World!");
using (var stream = new StreamReader("C:\\Users\\kingsoft\\Downloads\\untitled.glb"))
{
    var staticMesh = StaticMeshImporter.ImportFromGLTF(stream.BaseStream);
}