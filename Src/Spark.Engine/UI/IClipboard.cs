namespace Spark.Engine.UI;

/// <summary>平台无关的文本剪贴板接口。平台层负责注入系统剪贴板实现。</summary>
public interface IClipboard
{
    string? GetText();

    void SetText(string text);
}

/// <summary>测试和无平台环境使用的内存剪贴板。</summary>
public sealed class MemoryClipboard : IClipboard
{
    private string _text = string.Empty;

    public string? GetText() => _text;

    public void SetText(string text) => _text = text ?? string.Empty;
}
