using System.Globalization;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Checks rendered gait, prop ownership and native pixels, then records reviewable contact sheets.</summary>
internal static class PolishVerification
{
    public static async Task Run(PetWindow pet, string output)
    {
        var checks = new List<string>();
        void Require(bool pass, string label) { if (!pass) throw new InvalidOperationException(label); checks.Add("PASS " + label); }
        var canvas = (Canvas)pet.Content;
        var sprite = canvas.Children.OfType<Image>().Single();
        pet.State.Size = 280; pet.State.Wander = pet.State.ReducedMotion = false;
        pet.ApplySettings(); pet.BeginPreview();
        Directory.CreateDirectory(output);
        // Two figures occupy overlapping bounding boxes without touching. The crop
        // must preserve its own native pixels and exclude the other figure's fragment.
        var fixtureVisual=new DrawingVisual();
        using(var dc=fixtureVisual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Green,null,new Rect(50,20,50,50));
            dc.DrawRectangle(Brushes.Green,null,new Rect(50,60,400,5));
            dc.DrawRectangle(Brushes.Blue,null,new Rect(200,5,500,5));
            dc.DrawRectangle(Brushes.Blue,null,new Rect(600,5,100,35));
        }
        var fixtureBitmap=new RenderTargetBitmap(800,80,96,96,PixelFormats.Pbgra32);fixtureBitmap.Render(fixtureVisual);
        var fixturePng=new PngBitmapEncoder();fixturePng.Frames.Add(BitmapFrame.Create(fixtureBitmap));
        string fixturePath=Path.Combine(output,"cell-ownership.png");using(var file=File.Create(fixturePath))fixturePng.Save(file);
        byte[] originalBytes=File.ReadAllBytes(fixturePath);
        var fixtureCharacter=new Character { Root=output };
        var fixtureClip=new Sprite("cell-ownership.png",2,1,Cells:[new(0,0,500,80),new(200,0,600,80)],IsolateCells:true);
        var green=pet.Art.Frame(fixtureCharacter,fixtureClip,0);var blue=pet.Art.Frame(fixtureCharacter,fixtureClip,1);
        byte Alpha(BitmapSource source,int x,int y) { byte[] pixel=new byte[4];source.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);return pixel[3]; }
        Require(green.PixelWidth==500 && blue.PixelWidth==600,"native sprites larger than 320 pixels are retained");
        Require(Alpha(green,70,40)==255 && Alpha(green,300,7)==0,"first crop keeps its figure and excludes the neighbour");
        Require(Alpha(blue,400,20)==255 && Alpha(blue,100,62)==0,"second crop keeps its figure and excludes the neighbour");
        Require(originalBytes.SequenceEqual(File.ReadAllBytes(fixturePath)),"cell isolation never modifies the source image");
        for(int i=0;i<15;i++)pet.Art.Frame(fixtureCharacter,new Sprite("cell-ownership.png",1,1,Cells:[new(0,0,500+i,80)]),0);
        Require(ReferenceEquals(green,pet.Art.Frame(fixtureCharacter,fixtureClip,0)),"recently used sprites retain their decoded native pixels");
        pet.Art.Frame(fixtureCharacter,new Sprite("cell-ownership.png",1,1,Cells:[new(0,0,520,80)]),0);
        Require(pet.Art.GroundLine(green)>0,"loading another sheet never evicts a freshly used sprite's geometry");
        // A faint hair fringe may connect neighbouring opaque figures. Choosing
        // stronger seeds must separate the figures without discarding soft alpha.
        var fringeVisual=new DrawingVisual();
        using(var dc=fringeVisual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(64,0,128,0)),null,new Rect(20,16,60,8));
            dc.DrawRectangle(Brushes.Green,null,new Rect(10,10,20,20));
            dc.DrawRectangle(Brushes.Blue,null,new Rect(70,10,20,20));
        }
        var fringeBitmap=new RenderTargetBitmap(100,40,96,96,PixelFormats.Pbgra32);fringeBitmap.Render(fringeVisual);
        var fringePng=new PngBitmapEncoder();fringePng.Frames.Add(BitmapFrame.Create(fringeBitmap));
        string fringePath=Path.Combine(output,"soft-cell-ownership.png");using(var file=File.Create(fringePath))fringePng.Save(file);
        byte[] fringeBytes=File.ReadAllBytes(fringePath);
        var fringeClip=new Sprite("soft-cell-ownership.png",2,1,Cells:[new(0,0,80,40),new(20,0,80,40)],IsolateCells:true,SeparationAlpha:128);
        var fringeLeft=pet.Art.Frame(fixtureCharacter,fringeClip,0);var fringeRight=pet.Art.Frame(fixtureCharacter,fringeClip,1);
        Require(Alpha(fringeLeft,20,20)==255 && Alpha(fringeLeft,75,20)==0,"opaque neighbours separate across a faint alpha bridge");
        Require(Alpha(fringeLeft,35,20)==64 && Alpha(fringeRight,45,20)==64,"isolated sprites preserve their native semitransparent hair edges");
        Require(Alpha(fringeRight,55,20)==255 && Alpha(fringeRight,5,20)==0,"both sides of a faint bridge retain only their own figure");
        Require(!ReferenceEquals(fringeLeft,pet.Art.Frame(fixtureCharacter,fringeClip with { SeparationAlpha=48 },0)),"alpha separation settings have independent cached frames");
        Require(fringeBytes.SequenceEqual(File.ReadAllBytes(fringePath)),"soft-edge isolation leaves source bytes unchanged");
        void Save(BitmapSource bitmap, string file)
        {
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, file + ".png")); png.Save(stream);
        }
        BitmapSource Capture()
        {
            pet.UpdateLayout();
            var result = new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32); result.Render(canvas); result.Freeze(); return result;
        }
        void Sheet(Character c, string outfit, string action, double[] times)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(CloudTheme.Cream,null,new Rect(0,0,1440,640));
                for (int i=0;i<times.Length;i++)
                {
                    pet.PreviewMotion(action,times[i],5000); var frame = Capture();
                    double x=i%6*240,y=i/6*320;
                    dc.DrawImage(new CroppedBitmap(frame,new Int32Rect(110,138,340,350)),new Rect(x,y+12,240,247.06));
                    dc.DrawText(new FormattedText($"{c.Name} / {outfit} / {times[i]:0} ms",CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,new Typeface("Segoe UI, Microsoft YaHei UI"),11,CloudTheme.Ink,1),new Point(x+12,y+280));
                }
            }
            var sheet = new RenderTargetBitmap(1440,640,96,96,PixelFormats.Pbgra32); sheet.Render(visual);
            Save(sheet,$"{c.Id}-{outfit}-{action}");
        }
        foreach (var c in pet.Catalog.Characters)
        foreach (string outfit in new[] { "original", "swim", "wedding" })
        {
            string key=$"{c.Id}/{outfit}";
            pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id]=outfit; pet.ApplySettings();
            var walk=c.MotionFor(outfit,"walk")!;
            Require(walk.Frames?.Length==12 && walk.Frames.Distinct().Count()==12,key+": twelve authored walk poses");
            Require(walk.Facing=="right" && walk.Loop && walk.FrameMs!.All(t=>t==80),key+": right-facing 960 ms gait");
            var calibration=pet.Art.Frame(c,c.Resolve(outfit,"idle",0).Sprite,0);
            double standing=280*pet.Art.VisibleHeight(calibration);
            var hashes=new HashSet<string>();
            for (int i=0;i<12;i++)
            {
                pet.PreviewMotion("walk",i*80+1,3000); var bitmap=(BitmapSource)sprite.Source;
                Require(bitmap.PixelWidth==walk.Cells![i].Width && bitmap.PixelHeight==walk.Cells[i].Height,key+$": frame {i} retains native pixels");
                byte[] pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4]; bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0); hashes.Add(Convert.ToHexString(SHA256.HashData(pixels)));
                double height=sprite.Height*pet.Art.VisibleHeight(bitmap);
                Require(height/standing is >=.87 and <=1.001,key+$": frame {i} keeps body scale ({height/standing:0.000})");
                Require(Math.Abs(Canvas.GetTop(sprite)+sprite.Height*pet.Art.GroundLine(bitmap)-468)<.01,key+$": frame {i} stays on the floor");
                Require(((ScaleTransform)sprite.RenderTransform).ScaleX==1,key+$": frame {i} faces the direction used by Demo");
            }
            Require(hashes.Count==12,key+": twelve distinct rendered frames");
            Sheet(c,outfit,"walk",Enumerable.Range(0,12).Select(i=>(double)i*80).ToArray());
            foreach (string action in new[] { "meal", "eat" })
            {
                pet.PreviewMotion(action,700,3500);
                Require(pet.DrawsExtraFood != (c.MotionFor(outfit,action)?.BakedProps==true),key+$": {action} has exactly one food source");
            }
            pet.PreviewMotion("bonk",490,2200);
            Require(pet.DrawsExtraHammer != (c.MotionFor(outfit,"bonk")?.BakedProps==true),key+": hammer is baked or the shared Q illustration");
            if (c.Category=="chibi")
            {
                var build=c.MotionFor(outfit,"build");
                Require(build is { Frames.Length:12, BakedProps:true, Loop:false },key+": Q blocks have twelve hand-and-cube poses");
                double t=0;
                for (int i=0;i<12;i++)
                {
                    pet.PreviewMotion("build",t+1,5000);
                    Require(pet.UsingDrawnAction && pet.DrawnFrame==build!.Frames![i] && pet.ActiveMotion is null,key+$": build pose {i} is drawn, with no substitute rig");
                    t+=build!.FrameMs![i];
                }
                var times=new double[12];for(int i=1;i<12;i++)times[i]=times[i-1]+build!.FrameMs![i-1];
                Sheet(c,outfit,"build",times);
            }
            if (c.FamilyId is "whale" or "gpt")
            {
                Sheet(c,outfit,"meal",[0,240,480,720,960,1200,1440,1680,1920,2160,2400,2640]);
                pet.PreviewMotion("bonk",490,2200); Save(Capture(),$"{c.Id}-{outfit}-bonk");
            }
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }
        pet.SelectCharacter("gpt"); pet.State.Outfits["gpt"]="original"; pet.ApplySettings();
        using (var window = new SettingsProof(new SettingsWindow(pet))) Save(window.Bitmap(),"warm-cloud-settings");
        File.WriteAllLines(Path.Combine(output,"polish-check.txt"),checks.Append($"{checks.Count} rendered motion polish checks passed."));
    }
    private sealed class SettingsProof : IDisposable
    {
        private readonly Window window;
        public SettingsProof(Window window) { this.window=window; window.ShowActivated=false;window.IsHitTestVisible=false;window.Show();window.UpdateLayout(); }
        public BitmapSource Bitmap() { var content=(FrameworkElement)window.Content;var bmp=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(content);return bmp; }
        public void Dispose()=>window.Close();
    }
}
