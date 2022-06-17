using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spark.Core.SubSystem;

public class FileSystem
{
    IPlatFile platFile;
    public FileSystem(IPlatFile platFile)
    {
        this.platFile = platFile;
    }
    public Task<byte[]> LoadFileAsync(string path) => platFile.LoadFileAsync(path);
    public byte[] LoadFile(string path) => platFile.LoadFile(path);

    public Task<string> LoadFileStringAsync(string path)=> platFile.LoadFileStringAsync(path);

    public string LoadFileString(string path) => platFile.LoadFileString(path);


}

public interface IPlatFile
{
    Task<byte[]> LoadFileAsync(string path);
    byte[] LoadFile(string path);
    Task<string> LoadFileStringAsync(string path);
    string LoadFileString(string path);

}
