using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using SkiaSharp;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Decodes only the active character's sheets; a bounded cache prevents roster-size memory growth.</summary>
public sealed class ArtCache
{
    private readonly Dictionary<string, BitmapSource[]> cache = [];
    private sealed record Geometry(double Ground, double Height, double AnchorX);
    private readonly Dictionary<BitmapSource, Geometry> geometry = [];
    private readonly Dictionary<BitmapSource, double> sheetHeight = [];
    private readonly Queue<string> order = new();
    public BitmapSource Frame(Character character, Sprite sprite, int index)
    {
        string key = $"{character.Root}|{sprite.File}|{sprite.Columns}|{sprite.Rows}|{string.Join(';', sprite.Cells?.Select(c => $"{c.X},{c.Y},{c.Width},{c.Height}") ?? [])}";
        if (!cache.TryGetValue(key, out var frames))
        {
            string path = Character.SafeFile(character.Root, sprite.File);
            using var decoded = Decode(path);
            int cell = sprite.Cells is null ? Math.Min(320, decoded.Width / sprite.Columns) : decoded.Width / sprite.Columns;
            int width = sprite.Cells is null ? cell * sprite.Columns : decoded.Width;
            int height = (int)Math.Round((double)decoded.Height * width / decoded.Width);
            using var bitmap = decoded.Resize(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear))
                ?? throw new InvalidDataException($"无法缩放图片：{sprite.File}");
            var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Pbgra32, null, bitmap.GetPixels(), bitmap.ByteCount, bitmap.RowBytes);
            source.Freeze();
            int w = source.PixelWidth / sprite.Columns, h = source.PixelHeight / sprite.Rows;
            frames = Enumerable.Range(0, sprite.Columns * sprite.Rows).Select(i =>
            {
                var region = sprite.Cells?[i];
                var crop = region is null ? new Int32Rect(i % sprite.Columns * w, i / sprite.Columns * h, w, h)
                    : new Int32Rect(region.X, region.Y, region.Width, region.Height);
                BitmapSource frame = new CroppedBitmap(source, crop);
                if (region is not null && crop.Width > 320) frame = new TransformedBitmap(frame, new ScaleTransform(320d / crop.Width, 320d / crop.Width));
                frame.Freeze();
                int fw = frame.PixelWidth, fh = frame.PixelHeight;
                byte[] pixels = new byte[fw * fh * 4]; frame.CopyPixels(pixels, fw * 4, 0);
                int top = fh, bottom = 0;
                for (int y = 0; y < fh; y++)
                    for (int x = 0; x < fw; x++)
                        if (pixels[(y * fw + x) * 4 + 3] >= 48) { top = Math.Min(top, y); bottom = y + 1; }
                if (bottom == 0) { top = 0; bottom = fh; }
                // The head is a steadier pivot than swinging arms, legs, hair tips or a dress train.
                double mass = 0, weightedX = 0;
                for (int y = top; y < top + (bottom - top) * .3; y++)
                    for (int x = 0; x < fw; x++)
                    {
                        int alpha = pixels[(y * fw + x) * 4 + 3];
                        if (alpha >= 48) { mass += alpha; weightedX += (x + .5) * alpha; }
                    }
                double extent = Math.Max(fw, fh), anchor = mass > 0 ? weightedX / mass : fw / 2d;
                geometry[frame] = new(.5 + (bottom - fh / 2d) / extent, (bottom - top) / extent, .5 + (anchor - fw / 2d) / extent);
                return (BitmapSource)frame;
            }).ToArray();
            double maximumHeight = frames.Max(f => geometry[f].Height);
            foreach (var frame in frames) sheetHeight[frame] = maximumHeight;
            while (order.Count >= 16)
                if (cache.Remove(order.Dequeue(), out var expired)) foreach (var old in expired) { geometry.Remove(old); sheetHeight.Remove(old); }
            order.Enqueue(key); cache[key] = frames;
        }
        return frames[Math.Clamp(index, 0, frames.Length - 1)];
    }
    public double GroundLine(BitmapSource frame) => geometry[frame].Ground;
    public double VisibleHeight(BitmapSource frame) => geometry[frame].Height;
    public double SheetHeight(BitmapSource frame) => sheetHeight[frame];
    public double HorizontalAnchor(BitmapSource frame) => geometry[frame].AnchorX;
    private static SKBitmap Decode(string path)
    {
        using var stream = File.OpenRead(path);
        using var codec = SKCodec.Create(stream) ?? throw new InvalidDataException($"无法读取图片：{Path.GetFileName(path)}");
        if (codec.Info.Width < 1 || codec.Info.Height < 1 || codec.Info.Width > 6144 || codec.Info.Height > 6144)
            throw new InvalidDataException("图片边长必须在 1–6144 像素以内。");
        return SKBitmap.Decode(codec, new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul))
            ?? throw new InvalidDataException("图片像素损坏。");
    }
    public static void Validate(Character character, Sprite sprite)
    {
        using var bitmap = Decode(Character.SafeFile(character.Root, sprite.File));
        if (sprite.Cells is { } cells)
        {
            if (cells.Any(c => (long)c.X + c.Width > bitmap.Width || (long)c.Y + c.Height > bitmap.Height))
                throw new InvalidDataException($"动作裁帧超出图片：{sprite.File}");
        }
        else if (bitmap.Width % sprite.Columns != 0 || bitmap.Height % sprite.Rows != 0)
            throw new InvalidDataException($"图集不能均分：{sprite.File}");
    }
    public void Clear() { cache.Clear(); geometry.Clear(); sheetHeight.Clear(); order.Clear(); }
}
