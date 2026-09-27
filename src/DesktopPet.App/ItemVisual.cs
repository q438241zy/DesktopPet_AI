using System.Windows;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Small, resolution-independent toys and collectible artwork, shared by play, food and the shelf.</summary>
internal sealed class ItemVisual : FrameworkElement
{
    private Collectible item = Collectibles.Get("basketball");
    public Collectible Item { get => item; set { item = value; ToolTip = value.Name; InvalidateVisual(); } }
    protected override void OnRender(DrawingContext dc) => ItemArt.Draw(dc, Item, new Rect(0, 0, ActualWidth, ActualHeight));
}

internal static class ItemArt
{
    private static readonly Dictionary<string, Brush> brushes = [];
    private static readonly Dictionary<string, Geometry> paths = [];
    internal static Brush Brush(string color)
    {
        if (!brushes.TryGetValue(color, out var brush)) { brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); brushes[color] = brush; }
        return brush;
    }
    internal static Geometry Path(string data)
    {
        if (!paths.TryGetValue(data, out var geometry)) { geometry = Geometry.Parse(data); geometry.Freeze(); paths[data] = geometry; }
        return geometry;
    }
    public static void Draw(DrawingContext dc, Collectible item, Rect bounds)
    {
        dc.PushTransform(new TranslateTransform(bounds.X, bounds.Y)); dc.PushTransform(new ScaleTransform(bounds.Width / 36, bounds.Height / 36));
        var ink = new Pen(Brush("#344052"), 1.1) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        void P(string data, string? fill = null, string? stroke = null, double width = 1.3) => dc.DrawGeometry(fill is null ? null : Brush(fill), stroke is null ? null : new Pen(Brush(stroke), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Path(data));
        void C(double x, double y, double rx, double ry, string color, bool outline = false) => dc.DrawEllipse(Brush(color), outline ? ink : null, new Point(x, y), rx, ry);
        switch (item.Id)
        {
            case "basketball":
                C(18, 18, 15, 15, "#EF9853", true); P("M3,18 L33,18 M18,3 L18,33 M7,7 C20,10 20,26 7,29 M29,7 C16,10 16,26 29,29", stroke: "#764327"); break;
            case "baseball":
                C(18, 18, 15, 15, "#FFFDF7", true); P("M8,5 C19,12 19,24 8,31 M28,5 C17,12 17,24 28,31", stroke: "#D96266");
                for (int i = 0; i < 6; i++) { double y = 7 + i * 4.3, x = 9 + 4 * Math.Sin((i + .5) / 6 * Math.PI); dc.DrawLine(new Pen(Brush("#D96266"), 1), new Point(x - 1.7, y - 1), new Point(x + 1.7, y + 1)); dc.DrawLine(new Pen(Brush("#D96266"), 1), new Point(34 - x - 1.7, y + 1), new Point(34 - x + 1.7, y - 1)); } break;
            case "football":
                C(18, 18, 15, 15, "#FAFBFD", true); P("M18,10 L25,15 L22,23 L14,23 L11,15 Z M8,6 L11,10 L6,16 L3,15 M28,6 L25,10 L30,16 L33,15 M9,29 L14,26 L19,33 M29,27 L25,26 L22,33", "#465164"); P("M18,10 L18,3 M25,15 L31,11 M22,23 L27,29 M14,23 L10,30 M11,15 L5,11", stroke: "#758092", width: .8); break;
            case "volleyball":
                C(18, 18, 15, 15, "#FFFDF2", true); P("M18,3 C9,11 11,17 18,18 C27,17 31,11 30,9 M18,18 C21,27 14,32 11,31 M5,12 C7,22 12,26 18,25 M9,6 C8,16 14,21 18,21 M21,5 C16,12 19,15 22,15 M32,18 C25,21 24,28 24,31", stroke: "#D3A35B", width: 1.6); break;
            case "tennis":
                C(18, 18, 15, 15, "#B8CF58", true); P("M7,7 C22,4 14,32 29,28 M4,13 C11,17 8,29 17,33", stroke: "#FFFFEB", width: 2.2); break;
            case "pingpong":
                C(18, 18, 15, 15, "#F7B05D", true); C(12, 10, 5, 3, "#FFE2A5"); P("M27,23 Q24,29 18,29", stroke: "#DB8A40"); break;
            case "badminton":
                P("M4,5 Q17,0 32,6 L23,27 L15,29 Z", "#FBFDFF", "#8FADC4"); P("M7,6 L16,25 M13,4 L18,25 M19,4 L20,25 M25,5 L22,26 M8,13 Q19,10 29,14 M12,22 L25,22", stroke: "#AAC7DA", width: 1); P("M15,25 L24,25 L24,29 Q21,35 17,32 Z", "#D8B98B", "#8C7966"); break;
            case "rugby":
                P("M3,20 C6,2 26,0 33,16 C29,34 10,36 3,20 Z", "#BC754A", "#704932"); P("M8,9 Q15,18 10,28 M27,7 Q20,20 28,26", stroke: "#FFF3DD", width: 2); P("M13,21 L24,13 M15,16 L19,20 M19,13 L23,17", stroke: "#FFF4E0", width: 1.7); break;
            case "golf":
                C(18, 18, 15, 15, "#F8FAFC", true);
                for (int row = 0; row < 5; row++) for (int col = 0; col < 5; col++) { double x = 7 + col * 5 + row % 2 * 2, y = 8 + row * 5; if (Math.Pow(x - 18, 2) + Math.Pow(y - 18, 2) < 150) C(x, y, 1.2, 1, "#C9D4DF"); } break;
            case "bowling":
                C(18, 18, 15, 15, "#7C82BC", true); P("M7,23 Q12,28 19,27", stroke: "#B0B7E2", width: 3); C(18, 9, 2.5, 2.5, "#374363"); C(24, 12, 2.5, 2.5, "#374363"); C(18, 17, 2.6, 2.8, "#374363"); break;
            case "bread":
                P("M7,29 L7,15 C1,11 5,3 12,5 C16,1 23,2 25,5 C33,5 34,12 29,15 L29,29 Q18,33 7,29 Z", "#D79856", "#AF763E"); P("M10,27 L10,14 C6,11 9,6 13,8 C17,5 22,6 24,8 C29,8 30,12 26,15 L26,27 Q17,29 10,27 Z", "#FFE1A4"); break;
            case "rice":
                C(18, 17, 13, 10, "#EFF2F7"); C(11, 14, 6, 5, "#FFFDF8"); C(20, 11, 7, 5, "#FFFDF8"); C(26, 16, 5, 5, "#FFFDF8"); P("M4,18 L32,18 Q30,30 23,31 L13,31 Q5,28 4,18 Z", "#9CC8E4", "#588AA8"); P("M10,22 Q18,28 26,22", stroke: "#F7FCFF", width: 2); break;
            case "apple":
                P("M18,12 C1,2 0,24 13,32 Q18,29 22,32 C35,27 36,4 20,11 Z", "#E88487", "#B25961"); P("M18,12 L20,5", stroke: "#8B6747", width: 2); P("M20,6 Q25,0 30,5 Q25,11 20,6 Z", "#8EB58C"); P("M10,13 Q6,17 8,23", stroke: "#FFCED0", width: 2.5); break;
            case "banana":
                P("M7,8 Q7,26 27,20 L32,13 Q35,34 17,32 Q1,28 3,11 Z", "#F5CF65", "#BB9444"); P("M6,10 L5,5 L9,4 L11,9 Z", "#927044"); P("M8,20 Q15,32 27,24", stroke: "#E7B744"); break;
            case "cookie":
                C(18, 18, 15, 15, "#EAC58A", true); foreach (var (x, y) in new[] { (10, 10), (22, 8), (27, 18), (20, 26), (8, 22), (16, 17) }) C(x, y, 2, 2, "#906245"); break;
            case "milk":
                P("M9,9 L13,3 L26,3 L30,10 L30,32 L9,32 Z", "#F9FDFF", "#7FA9C2"); P("M9,9 L23,9 L23,32 L9,32 Z", "#C7E3F4"); P("M23,9 L26,3 L30,10 M13,3 L23,9", stroke: "#7FA9C2"); P("M15,16 L12,23 Q16,29 20,23 Z", "#FFFFFF"); break;
            case "shell":
                P("M18,30 L4,16 C0,3 9,4 11,6 C15,0 20,1 23,5 C31,0 38,10 31,20 L23,30 Z", "#F1C3B2", "#C58F83"); P("M7,11 L18,28 M13,7 L19,27 M20,5 L20,28 M27,9 L22,28 M32,15 L23,28", stroke: "#D59E8D", width: 1); break;
            case "leaf":
                P("M6,28 C0,9 20,2 31,4 C34,23 22,34 6,28 Z", "#9DC297", "#709571"); P("M5,32 L27,9 M11,25 L8,15 M16,20 L15,10 M17,19 L26,20 M22,14 L29,15", stroke: "#E5EECB", width: 1.5); break;
            case "pencil":
                P("M5,31 L8,20 L25,3 L33,11 L16,28 Z", "#F0C574", "#AE8B4B"); P("M25,3 L28,0 L36,8 L33,11 Z", "#E9A8B3", "#B98291"); P("M8,20 L16,28 L5,31 Z", "#E9D0AC"); P("M5,31 L7,25 L11,29 Z", "#56606B"); P("M13,23 L29,7", stroke: "#FFF1B4", width: 2); break;
            case "key":
                C(11, 11, 8, 8, "#E5C47A", true); C(11, 11, 3, 3, "#FFF7E0"); P("M15,16 L28,31 L33,26 L30,22 L26,24 L23,19 L25,17 L22,14 L19,17 Z", "#E5C47A", "#AE9258"); break;
        }
        dc.Pop(); dc.Pop();
    }
}
