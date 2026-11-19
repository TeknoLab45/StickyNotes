using System.Collections.Generic;
using System.Windows.Media;

namespace StickyNotes.App;

public static class NoteTheme
{
    private static readonly Dictionary<string, (string Body, string Strip)> Map = new()
    {
        ["yellow"] = ("#FFF4B8", "#F5D95A"),
        ["green"] = ("#D4F5C0", "#A9DE8A"),
        ["pink"] = ("#FFD1E3", "#F2A0C2"),
        ["purple"] = ("#E3D4FA", "#C3A6EE"),
        ["blue"] = ("#CFE6FB", "#9CC6F0"),
        ["gray"] = ("#DDDDDD", "#BDBDBD"),
        ["white"] = ("#FFFFFF", "#E4E4E4"),
    };

    private static SolidColorBrush Brush(string hex)
    {
        var b = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public static SolidColorBrush Body(string color) => Brush(Map.TryGetValue(color, out var v) ? v.Body : Map["yellow"].Body);
    public static SolidColorBrush Strip(string color) => Brush(Map.TryGetValue(color, out var v) ? v.Strip : Map["yellow"].Strip);

    public static string Label(string color) => color switch
    {
        "yellow" => "Sarı",
        "green" => "Yeşil",
        "pink" => "Pembe",
        "purple" => "Mor",
        "blue" => "Mavi",
        "gray" => "Gri",
        "white" => "Beyaz",
        _ => color
    };
}
