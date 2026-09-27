using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace DesktopPet.App;

/// <summary>Vector clouds and a shared sky palette keep small desktop controls legible at any DPI.</summary>
internal static class CloudTheme
{
    public static readonly Brush Ink = Brush("#1D1D1F"), Muted = Brush("#7B7D85"), Blue = Brush("#007AFF"), Line = Brush("#E5E5EA"), Pale = Brush("#EAF2FF");
    public static readonly Geometry Cloud = Geometry.Parse("M 14,47 C 6,47 3,42 3,36 C 3,28 9,23 17,24 C 18,13 27,8 36,12 C 42,14 46,19 46,26 C 54,24 61,30 61,37 C 61,43 57,47 50,47 Z");
    public static SolidColorBrush Brush(string color)
    { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
    public static LinearGradientBrush Sky(string top = "#F6F8FC", string bottom = "#FFFFFF")
    { var brush = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(top), (Color)ColorConverter.ConvertFromString(bottom), 90); brush.Freeze(); return brush; }
    public static DropShadowEffect Shadow(double depth = 6, double opacity = .1) => new() { Color = Color.FromRgb(35, 42, 60), BlurRadius = 28, ShadowDepth = depth, Opacity = opacity };
    public static LineIcon Icon(string glyph, double size = 22) => new() { Glyph = glyph, Width = size, Height = size, IsHitTestVisible = false, Focusable = false };
    public static string CategoryName(string category) => category switch { "3d" => "3D版", "adult" => "真人版", _ => "Q版" };
    public static Border Badge(string text, Brush? background = null) => new() { Background = background ?? Pale, CornerRadius = new CornerRadius(11), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 7, 0), Child = new TextBlock { Text = text, Foreground = Blue, FontSize = 11 } };

    public static ImageSource AppIcon
    {
        get
        {
            var drawing = new DrawingGroup();
            using (var context = drawing.Open()) CloudIcon.Draw(context, "smile");
            drawing.Freeze(); return new DrawingImage(drawing);
        }
    }

    /// <summary>The ICO contains a PNG so the executable, taskbar and tray use the same vector source.</summary>
    public static void WriteIcon(string path)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) { dc.PushTransform(new ScaleTransform(4, 4)); CloudIcon.Draw(dc, "smile"); dc.Pop(); }
        var bitmap = new RenderTargetBitmap(256, 256, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var bytes = new MemoryStream(); png.Save(bytes);
        using var output = new BinaryWriter(File.Create(path));
        output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)1);
        output.Write((byte)0); output.Write((byte)0); output.Write((byte)0); output.Write((byte)0);
        output.Write((ushort)1); output.Write((ushort)32); output.Write((uint)bytes.Length); output.Write(22u); output.Write(bytes.ToArray());
    }
}

internal sealed class CloudIcon : FrameworkElement
{
    public string Glyph { get; init; } = "smile";
    protected override void OnRender(DrawingContext dc)
    { dc.PushTransform(new ScaleTransform(ActualWidth / 64, ActualHeight / 64)); Draw(dc, Glyph); dc.Pop(); }
    internal static void Draw(DrawingContext dc, string glyph)
    {
        var outline = new Pen(CloudTheme.Brush("#A6CAE9"), 1.7) { LineJoin = PenLineJoin.Round };
        dc.DrawGeometry(CloudTheme.Sky("#FFFFFF", "#E1F0FF"), outline, CloudTheme.Cloud);
        var pen = new Pen(CloudTheme.Blue, 2.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        string? data = glyph switch
        {
            "heart" => "M32,41 C29,39 23,35 23,31 C23,25 29,25 32,29 C35,25 41,25 41,31 C41,35 35,39 32,41",
            "sun" => "M32,21 L32,24 M32,40 L32,43 M21,32 L24,32 M40,32 L43,32 M24,24 L26,26 M38,38 L40,40 M24,40 L26,38 M38,26 L40,24",
            "brush" => "M25,36 L37,23 L42,28 L29,40 Z M24,38 Q24,43 19,42 Q24,44 29,40",
            "settings" => "M23,24 L23,42 M32,22 L32,42 M41,24 L41,42 M20,29 L26,29 M29,37 L35,37 M38,30 L44,30",
            "cube" => "M32,21 L43,27 L43,38 L32,44 L21,38 L21,27 Z M21,27 L32,33 L43,27 M32,33 L32,44",
            "person" => "M23,43 C23,33 41,33 41,43 M32,22 C24,22 24,33 32,33 C40,33 40,22 32,22",
            "dress" => "M26,22 L23,29 L27,32 L22,43 L42,43 L37,32 L41,29 L38,22 L34,26 L30,26 Z",
            "walk" => "M23,40 L30,33 L32,27 L39,32 L44,32 M31,33 L37,40 L37,44 M25,30 L31,27",
            "moon" => "M36,22 C26,23 25,36 37,38 C33,45 21,40 22,32 C22,25 29,21 36,22",
            "ball" => "M22,32 A10,10 0 1 0 42,32 A10,10 0 1 0 22,32 M26,24 Q36,32 27,41 M40,27 Q30,29 23,37",
            "blocks" => "M22,32 L31,32 L31,42 L22,42 Z M33,32 L43,32 L43,42 L33,42 Z M27,21 L37,21 L37,30 L27,30 Z",
            "food" => "M23,32 L41,32 Q40,43 32,43 Q24,43 23,32 M27,27 Q24,24 28,21 M35,27 Q32,24 36,21",
            "letter" => "M21,25 L43,25 L43,41 L21,41 Z M21,25 L32,34 L43,25",
            "chat" => "M23,25 L42,25 L42,38 L32,38 L26,43 L26,38 L23,38 Z M28,31 L28,32 M33,31 L33,32 M38,31 L38,32",
            "check" => "M23,33 L30,40 L43,25",
            "back" => "M37,23 L27,33 L37,43",
            "spark" => "M32,21 L35,29 L43,32 L35,35 L32,43 L29,35 L21,32 L29,29 Z",
            _ => null
        };
        if (data is not null) dc.DrawGeometry(null, pen, Geometry.Parse(data));
        else
        {
            dc.DrawEllipse(CloudTheme.Blue, null, new Point(25, 32), 1.6, 2);
            dc.DrawEllipse(CloudTheme.Blue, null, new Point(40, 32), 1.6, 2);
            dc.DrawGeometry(null, pen, Geometry.Parse("M29,38 Q32,42 36,38"));
            dc.DrawEllipse(CloudTheme.Brush("#F4CBCD"), null, new Point(21, 37), 3, 1.5);
            dc.DrawEllipse(CloudTheme.Brush("#F4CBCD"), null, new Point(44, 37), 3, 1.5);
        }
        if (glyph == "sun") dc.DrawEllipse(null, pen, new Point(32, 32), 5, 5);
        if (glyph == "walk") dc.DrawEllipse(CloudTheme.Blue, null, new Point(34, 22), 2.8, 2.8);
    }
}

/// <summary>Noninteractive scenery drawn as vectors, without a background animation timer.</summary>
internal sealed class CloudScenery : FrameworkElement
{
    public CloudScenery() { IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext dc)
    {
        var glow = new RadialGradientBrush(Color.FromArgb(80, 220, 231, 247), Colors.Transparent);
        dc.DrawEllipse(glow, null, new Point(ActualWidth / 2, ActualHeight * .54), ActualWidth * .48, ActualHeight * .48);
    }
}
