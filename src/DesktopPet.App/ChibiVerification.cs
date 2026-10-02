using System.Globalization;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class ChibiVerification
{
    internal static async Task Run(PetWindow pet,string output,string? appearance=null)
    {
        var checks=new List<string>();
        void Require(bool value,string label) { if(!value)throw new InvalidOperationException(label);checks.Add("PASS "+label); }
        Directory.CreateDirectory(output);
        pet.State.Size=240;pet.State.Wander=pet.State.ReducedMotion=false;pet.ApplySettings();pet.BeginPreview();
        var canvas=(Canvas)pet.Content;var image=canvas.Children.OfType<Image>().Single();
        var targets=(from c in pet.Catalog.Characters where c.Category=="chibi"
                     from outfit in new[]{"original","swim","wedding"}
                     where appearance is null || c.Id+"-"+outfit==appearance select(c,outfit)).ToArray();
        Require(targets.Length==(appearance is null?24:1),"requested Q appearances available");
        foreach(var (c,outfit) in targets)
        {
            string key=c.Id+"/"+outfit;pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]=outfit;pet.ApplySettings();
            var catalogRows=ActionCoverage.Actions.Select(a=>ActionCoverage.Assess(c,outfit,a));
            Require(catalogRows.All(r=>r.Status is not ("缺少动作" or "近似动作")),key+": audited actions have their own implementation");
            string[] actions=outfit=="original"?["poke","tickle","ball-ready","eat"]:["chat","think","headpat","poke","tickle","bonk","jump","curl","sleep","pickup","ball-ready","ball-hit","ball-miss","eat","meal"];
            foreach(string action in actions)
            {
                var motions=outfit=="original"?c.Motions:c.Outfits[outfit].Motions;
                Require(motions.TryGetValue(action,out var clip),key+"/"+action+": exact action, no alias");
                Require(clip is { IsolateCells:true,Frames.Length:>=2,HeightRatios:not null },key+"/"+action+": native drawn sequence");
                var hashes=new HashSet<string>();var scales=new List<double>();double elapsed=0;
                var visual=new DrawingVisual();
                using(var dc=visual.RenderOpen())
                {
                    dc.DrawRectangle(CloudTheme.Cream,null,new Rect(0,0,1600,640));
                    for(int i=0;i<clip!.Frames!.Length;i++)
                    {
                        pet.PreviewMotion(action,elapsed+1,(int)PortraitMotion.Duration(action));pet.UpdateLayout();
                        var bitmap=(BitmapSource)image.Source;var cell=clip.Cells![clip.Frames[i]];
                        Require(pet.UsingDrawnAction&&pet.DrawnFrame==clip.Frames[i]&&pet.ActiveMotion is null,key+$"/{action}/{i}: selected outfit drawing rendered");
                        Require(bitmap.PixelWidth==cell.Width&&bitmap.PixelHeight==cell.Height,key+$"/{action}/{i}: source resolution retained");
                        Require(Math.Abs(Canvas.GetTop(image)+image.Height*pet.Art.GroundLine(bitmap)+pet.AirborneOffset-468)<.01,key+$"/{action}/{i}: floor/airborne baseline");
                        byte[] pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);hashes.Add(Convert.ToHexString(SHA256.HashData(pixels)));
                        scales.Add(image.Height/Math.Max(bitmap.PixelWidth,bitmap.PixelHeight));
                        if(action is "eat" or "meal")Require(!pet.DrawsExtraFood&&clip.BakedProps,key+$"/{action}/{i}: food stays in the illustrated hands");
                        if(action=="bonk")Require(!pet.DrawsExtraHammer&&clip.BakedProps,key+$"/{action}/{i}: one illustrated mallet");
                        if(c.FamilyId is "whale" or "gpt")
                        {
                            var shot=new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32);shot.Render(canvas);
                            double x=i%4*400,y=i/4*320;
                            dc.DrawImage(new CroppedBitmap(shot,new Int32Rect(110,140,340,350)),new Rect(x+45,y,290,298));
                            dc.DrawText(new FormattedText($"{c.Name} · {outfit} · {action} · {elapsed:0} ms",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI, Microsoft YaHei UI"),11,CloudTheme.Ink,1),new Point(x+15,y+305));
                        }
                        elapsed+=clip.FrameMs![i];
                    }
                }
                Require(hashes.Count==clip.Frames.Length,key+"/"+action+": distinct source poses");
                Require(scales.Max()/scales.Min()<1.06,key+"/"+action+": stable pixel scale between poses");
                if(!clip.Loop)
                {
                    pet.PreviewMotion(action,elapsed+8000,(int)elapsed);
                    Require(pet.DrawnFrame==clip.Frames[^1],key+"/"+action+": final pose holds without restart");
                }
                if(c.FamilyId is "whale" or "gpt")
                {
                    var sheet=new RenderTargetBitmap(1600,640,96,96,PixelFormats.Pbgra32);sheet.Render(visual);
                    var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(sheet));using var file=File.Create(Path.Combine(output,$"{c.Id}-{outfit}-{action}.png"));png.Save(file);
                }
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        File.WriteAllLines(Path.Combine(output,"chibi-check.txt"),checks.Append($"{checks.Count} Q wardrobe action checks passed."));
        pet.EndPreview();
    }
}
