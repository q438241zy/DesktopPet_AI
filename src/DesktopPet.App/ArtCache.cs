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
    private readonly Dictionary<BitmapSource, double> poseScale = [];
    private readonly LinkedList<string> order = new();
    public BitmapSource Frame(Character character, Sprite sprite, int index)
    {
        string key = $"{character.Root}|{sprite.File}|{sprite.Columns}|{sprite.Rows}|{sprite.IsolateCells}|{string.Join(';', sprite.Cells?.Select(c => $"{c.X},{c.Y},{c.Width},{c.Height}") ?? [])}";
        if (!cache.TryGetValue(key, out var frames))
        {
            string path = Character.SafeFile(character.Root, sprite.File);
            using var decoded = Decode(path);
            // Keep the source pixels. Downsampling to 320 here and enlarging again on a
            // high-DPI desktop discarded hair, face and lace detail before rendering.
            var source = BitmapSource.Create(decoded.Width, decoded.Height, 96, 96, PixelFormats.Pbgra32, null, decoded.GetPixels(), decoded.ByteCount, decoded.RowBytes);
            source.Freeze();
            var owners = sprite.IsolateCells && sprite.Cells is not null ? CellOwners(source, sprite.Cells) : null;
            int w = source.PixelWidth / sprite.Columns, h = source.PixelHeight / sprite.Rows;
            frames = Enumerable.Range(0, sprite.Columns * sprite.Rows).Select(i =>
            {
                var region = sprite.Cells?[i];
                var crop = region is null ? new Int32Rect(i % sprite.Columns * w, i / sprite.Columns * h, w, h)
                    : new Int32Rect(region.X, region.Y, region.Width, region.Height);
                BitmapSource frame = new CroppedBitmap(source, crop);
                frame.Freeze();
                int fw = frame.PixelWidth, fh = frame.PixelHeight;
                byte[] pixels = new byte[fw * fh * 4]; frame.CopyPixels(pixels, fw * 4, 0);
                if (owners is not null)
                {
                    // Bounding boxes can overlap even when the sprites themselves do
                    // not. Keep this cell's complete connected figure and detached props;
                    // exclude only pixels belonging to neighbouring cells. Source art is untouched.
                    for (int y = 0; y < fh; y++)
                        for (int x = 0; x < fw; x++)
                            if (owners[(crop.Y + y) * source.PixelWidth + crop.X + x] != i + 1)
                                Array.Clear(pixels, (y * fw + x) * 4, 4);
                    frame = BitmapSource.Create(fw,fh,96,96,PixelFormats.Pbgra32,null,pixels,fw*4); frame.Freeze();
                }
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
            double maximumPixels = frames.Max(f => geometry[f].Height * Math.Max(f.PixelWidth, f.PixelHeight));
            foreach (var frame in frames) poseScale[frame] = Math.Max(frame.PixelWidth, frame.PixelHeight) / maximumPixels;
            while (order.Count >= 16)
            {
                string expiredKey=order.First!.Value;order.RemoveFirst();
                if (cache.Remove(expiredKey, out var expired)) foreach (var old in expired) { geometry.Remove(old); sheetHeight.Remove(old); poseScale.Remove(old); }
            }
            cache[key] = frames;
        }
        order.Remove(key);order.AddLast(key);
        return frames[Math.Clamp(index, 0, frames.Length - 1)];
    }
    public double GroundLine(BitmapSource frame) => geometry[frame].Ground;
    public double VisibleHeight(BitmapSource frame) => geometry[frame].Height;
    public double SheetHeight(BitmapSource frame) => sheetHeight[frame];
    public double PoseScale(BitmapSource frame) => poseScale[frame];
    public double HorizontalAnchor(BitmapSource frame) => geometry[frame].AnchorX;
    private static int[] CellOwners(BitmapSource source, SpriteCell[] cells)
    {
        int width=source.PixelWidth,height=source.PixelHeight,count=width*height;
        byte[] pixels=new byte[count*4]; source.CopyPixels(pixels,width*4,0);
        int[] owners=new int[count],queue=new int[count];
        for (int start=0;start<count;start++)
        {
            if (owners[start]!=0 || pixels[start*4+3]<48) continue;
            int read=0,end=1,left=start%width,right=left,top=start/width,bottom=top;
            queue[0]=start;owners[start]=-1;
            while (read<end)
            {
                int at=queue[read++],x=at%width,y=at/width;
                left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;if(nx<0||nx>=width||ny<0||ny>=height)continue;
                    int next=ny*width+nx;
                    if(owners[next]==0&&pixels[next*4+3]>=48){owners[next]=-1;queue[end++]=next;}
                }
            }
            double best=double.MaxValue;int owner=-1;
            for(int i=0;i<cells.Length;i++)
            {
                var c=cells[i];if(left<c.X||right>=c.X+c.Width||top<c.Y||bottom>=c.Y+c.Height)continue;
                double distance=Math.Pow((left+right)/2d-c.X-c.Width/2d,2)+Math.Pow((top+bottom)/2d-c.Y-c.Height/2d,2);
                if(distance<best){best=distance;owner=i+1;}
            }
            for(int i=0;i<end;i++)owners[queue[i]]=owner;
        }
        // Extend ownership into the low-alpha antialiasing fringe without changing
        // any retained pixel's colour or alpha. Fully transparent space stays empty.
        int head=0,tail=0;for(int i=0;i<count;i++)if(owners[i]>0)queue[tail++]=i;
        while(head<tail)
        {
            int at=queue[head++],x=at%width,y=at/width;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {
                int nx=x+dx,ny=y+dy;if(nx<0||nx>=width||ny<0||ny>=height)continue;
                int next=ny*width+nx;
                if(owners[next]==0&&pixels[next*4+3]>0){owners[next]=owners[at];queue[tail++]=next;}
            }
        }
        return owners;
    }
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
    public void Clear() { cache.Clear(); geometry.Clear(); sheetHeight.Clear(); poseScale.Clear(); order.Clear(); }
}
