using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopPet.App;

/// <summary>Native vector counterpart of the approved cloud-pet-v19 SVGs.</summary>
internal static class CloudMascot
{
    private const string Body = "M55 88c-2-20 13-34 33-31 9-17 31-22 47-11 17-5 35 4 40 20 22-1 38 18 33 39 19 13 22 39 7 55-2 25-24 37-46 31-13 15-35 19-51 8-17 11-39 7-50-6-27 6-48-13-44-38-13-20-4-49 17-54 3-5 8-10 14-13Z";
    private const string Ears = "M65 80C52 72 50 52 63 43c11-8 29-5 36 7M146 50c7-14 24-19 37-10 13 9 12 27 1 39";
    private const string Nose = "M114 133c1-3 11-3 12 0 0 3-4 5-6 5s-6-2-6-5Z";
    private static readonly SolidColorBrush Ink = Brush("#695360"), Cheek = Brush("#F1B5C5");
    private static SolidColorBrush Brush(string color)
    { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); return brush; }
    private static Pen Stroke(string color, double width)
    { var pen = new Pen(Brush(color), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; pen.Freeze(); return pen; }
    private static void Path(DrawingContext dc, string geometry, Brush? fill = null, Pen? stroke = null) => dc.DrawGeometry(fill, stroke, Geometry.Parse(geometry));
    private static LinearGradientBrush Fur()
    {
        var brush = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(120, 39), EndPoint = new Point(120, 194) };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFFEFA"), 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FFF9F3"), .56));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FBE8E8"), 1));
        brush.Freeze(); return brush;
    }

    internal static void Draw(DrawingContext dc, double width, double height, bool compact = false)
    {
        // Match the SVG viewBox with uniform scaling; rectangular control slots never stretch the pet.
        var view = compact ? new Rect(13, 29, 214, 190) : new Rect(0, 0, 240, 240);
        var scale = Math.Min(width / view.Width, height / view.Height);
        dc.PushTransform(new TranslateTransform((width - view.Width * scale) / 2, (height - view.Height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushTransform(new TranslateTransform(-view.X, -view.Y));
        if (compact) DrawCompact(dc); else DrawFull(dc);
        dc.Pop(); dc.Pop(); dc.Pop();
    }

    private static void DrawFull(DrawingContext dc)
    {
        var outline = Stroke("#D9AFBB", 3.5);
        dc.PushOpacity(.12); dc.DrawEllipse(Brush("#D29AA8"), null, new Point(120, 218), 61, 6); dc.Pop();
        dc.PushTransform(new TranslateTransform(0, 8));
        Path(dc, "M80 172c-11 6-14 20-7 29 5 7 17 8 26 2l9-18", Brush("#FFF5EF"), outline);
        Path(dc, "M159 172c11 6 14 20 7 29-5 7-17 8-26 2l-9-18", Brush("#FFF5EF"), outline); dc.Pop();
        Path(dc, Ears, Fur(), outline);
        Path(dc, "M66 60c-1-8 9-13 17-7M160 53c9-7 18-1 18 6", null, Stroke("#EFC8D1", 7));
        Path(dc, Body, Fur(), outline);
        dc.PushOpacity(.9); Path(dc, "M78 76c7-11 17-15 28-12", null, Stroke("#FFFFFF", 5)); dc.Pop();
        Path(dc, "M153 175c1-13 6-22 15-24 10-2 15 7 12 16-2 7-9 13-17 15", Brush("#FFF8F2"), outline);
        Path(dc, "M87 175c-1-13-6-22-15-24-10-2-15 7-12 16 2 7 9 13 17 15", Brush("#FFF8F2"), outline);
        Face(dc, false);
        dc.PushOpacity(.8); dc.PushTransform(new TranslateTransform(6, -5));
        Path(dc, "M114 171c-5-9-15-5-12 2 3 6 12 11 12 11s10-5 13-11c3-7-7-11-13-2Z", Brush("#E9A9BA"));
        dc.Pop(); dc.Pop();
    }

    private static void DrawCompact(DrawingContext dc)
    {
        var outline = Stroke("#D5A2B3", 5); var fur = Brush("#FFF8F2");
        Path(dc, "M80 180c-11 6-14 20-7 29 5 7 17 8 26 2l9-18M159 180c11 6 14 20 7 29-5 7-17 8-26 2l-9-18", fur, outline);
        Path(dc, Ears, Brush("#F8DEE5"), outline);
        Path(dc, Body, fur, outline);
        Path(dc, "M153 175c1-13 6-22 15-24 10-2 15 7 12 16M87 175c-1-13-6-22-15-24-10-2-15 7-12 16", fur, outline);
        Face(dc, true);
    }

    private static void Face(DrawingContext dc, bool compact)
    {
        dc.PushOpacity(compact ? .65 : .57);
        dc.DrawEllipse(compact ? Brush("#EFADBF") : Cheek, null, new Point(73, compact ? 138 : 137), 14, 8);
        dc.DrawEllipse(compact ? Brush("#EFADBF") : Cheek, null, new Point(167, compact ? 138 : 137), 14, 8); dc.Pop();
        foreach (var x in new[] { 92d, 148d })
        {
            dc.DrawEllipse(Ink, null, new Point(x, 119), compact ? 9 : 7.5, compact ? 11 : 10);
            dc.DrawEllipse(Brush(compact ? "#FFFDF9" : "#FFFEFC"), null, new Point(x + (compact ? 3 : 2), 115), compact ? 3 : 2.5, compact ? 3 : 2.5);
        }
        Path(dc, Nose, Brush("#B88599"));
        Path(dc, compact ? "M110 143c3 6 9 6 10 0 1 6 7 6 10 0" : "M110 141c3 6 9 6 10 0 1 6 7 6 10 0", null, Stroke("#806471", compact ? 4 : 3));
    }

    internal static ImageSource Image(bool compact = false)
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        { dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 64, 64)); Draw(dc, 64, 64, compact); }
        drawing.Freeze(); return new DrawingImage(drawing);
    }

    internal static byte[] Png(int size)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) Draw(dc, size, size, size <= 48);
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var bytes = new MemoryStream(); png.Save(bytes); return bytes.ToArray();
    }

    internal static void WriteIcon(string path)
    {
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
        var frames = sizes.Select(Png).ToArray();
        using var output = new BinaryWriter(File.Create(path));
        output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
        uint offset = (uint)(6 + 16 * sizes.Length);
        for (var i = 0; i < sizes.Length; i++)
        {
            var size = (byte)(sizes[i] == 256 ? 0 : sizes[i]);
            output.Write(size); output.Write(size); output.Write((byte)0); output.Write((byte)0);
            output.Write((ushort)1); output.Write((ushort)32); output.Write((uint)frames[i].Length); output.Write(offset);
            offset += (uint)frames[i].Length;
        }
        foreach (var frame in frames) output.Write(frame);
    }
}
