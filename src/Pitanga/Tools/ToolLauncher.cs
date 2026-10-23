using Pitanga.UI;

namespace Pitanga.Tools;

public static class ToolLauncher
{
    static readonly Dictionary<string, Func<string, MacWindow>> Factories = new()
    {
        ["CROP"] = path => new CropWindow(path),
        ["ADDBG"] = path => new AddBackgroundWindow(path),
        ["METADATA"] = path => new MetadataWindow(path),
        ["EDIT"] = path => new EditWindow(path),
        ["ANNOTATE"] = path => new AnnotateWindow(path, AnnotateMode.Annotate),
        ["REDACT"] = path => new AnnotateWindow(path, AnnotateMode.Redact),
    };

    /// <summary>COMPRESS não tem janela: roda direto como uma conversão.</summary>
    public static bool HasWindow(string id) => Factories.ContainsKey(id);

    public static MacWindow Open(string id, string path)
    {
        if (!Factories.TryGetValue(id, out var factory)) throw new NotSupportedException($"Unknown tool {id}.");
        var window = factory(path);
        window.ShowAndFocus();
        return window;
    }
}
