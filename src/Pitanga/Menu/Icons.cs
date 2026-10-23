using System.Windows.Media;

namespace Pitanga.Menu;

/// <summary>Ícones de traço (24×24), no espírito dos SF Symbols.</summary>
internal static class Icons
{
    static readonly Dictionary<string, Geometry> Map = new()
    {
        ["crop"] = Parse("M6,2 V18 H22 M2,6 H18 V22"),
        ["compress"] = Parse("M4,14 H10 V20 M20,10 H14 V4 M14,10 L21,3 M3,21 L10,14"),
        ["metadata"] = Parse("M12,2.5 A9.5,9.5 0 1 1 11.99,2.5 Z M12,11 V17 M12,7.3 V7.7"),
        ["edit"] = Parse("M4,7 H20 M4,17 H20 M9,4 V10 M15,14 V20"),
        ["annotate"] = Parse("M4,20 L4.8,16 L15.5,5.3 A2.2,2.2 0 0 1 18.7,8.5 L8,19.2 Z M13.5,7.3 L16.7,10.5"),
        ["addbg"] = Parse("M5,3 H19 A2,2 0 0 1 21,5 V19 A2,2 0 0 1 19,21 H5 A2,2 0 0 1 3,19 V5 A2,2 0 0 1 5,3 Z M8,8 H16 V16 H8 Z"),
        ["redact"] = Parse("M2,12 C5,6.5 19,6.5 22,12 C19,17.5 5,17.5 2,12 Z M9.5,12 A2.5,2.5 0 1 0 14.5,12 A2.5,2.5 0 1 0 9.5,12 M4,4 L20,20"),
        ["merge"] = Parse("M6,3 V8 C6,12 12,12 12,16 V21 M18,3 V8 C18,12 12,12 12,16 M9,18 L12,21 L15,18"),
    };

    public static bool TryGet(string key, out Geometry geometry) => Map.TryGetValue(key, out geometry!);

    static Geometry Parse(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }
}
