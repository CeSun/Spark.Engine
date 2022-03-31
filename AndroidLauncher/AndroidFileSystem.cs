using Android.Content.Res;
using LiteEngine.Core.SubSystem;

namespace AndroidLauncher;

public class AndroidFileSystem : IPlatFile
{
    internal AndroidFileSystem(AssetManager assetManager)
    {
        _AssetManager = assetManager;
    }

    AssetManager _AssetManager;

    const int maxReadSize = 256 * 1024;
    public byte[] LoadFile(string path)
    {
        using (var br = new BinaryReader(_AssetManager.Open(path)))
        {
            return br.ReadBytes(maxReadSize);
        }
    }
    
    public async Task<byte[]> LoadFileAsync(string path)
    {
        await Task.Delay(0);
        using (var br = new BinaryReader(_AssetManager.Open(path)))
        {
            return br.ReadBytes(maxReadSize);
        }
    }

    public string LoadFileString(string path)
    {
        using (var sr = new StreamReader(_AssetManager.Open(path)))
        {
            return sr.ReadToEnd();
        }
    }

    public async Task<string> LoadFileStringAsync(string path)
    {
        using (var sr = new StreamReader(_AssetManager.Open(path)))
        {
            return await sr.ReadToEndAsync();
        }
    }
}
