using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Compare poses at one user size using the actual desktop renderer.</summary>
internal static class ScaleVerification
{
    internal static async Task Run(PetWindow pet, string output, bool baseline = false)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var measurements = new List<object>();
        void Require(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks.Add("PASS " + label);
        }
        pet.State.Size = 240; pet.State.Opacity = 1;
        pet.State.Wander = pet.State.ReducedMotion = false; pet.BeginPreview();
        var canvas = (Canvas)pet.Content;
        var image = canvas.Children.OfType<Image>().Single();
        (string Action, double Time)[] poses = [("idle",0),("walk",241),("headpat",420),("poke",400),
            ("eat",700),("chat",420),("build",1900),("curl",1900),("sleep",1800),("jump",350),("pickup",500),("think",420)];
        foreach (var c in pet.Catalog.Characters)
        foreach (string outfit in Catalog.BuiltInOutfits)
        {
            pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id] = outfit; pet.ApplySettings();
            var idleFrame = (BitmapSource)image.Source;
            double standing = image.Height * pet.Art.VisibleHeight(idleFrame);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(CloudTheme.Cream, null, new Rect(0,0,1120,1020));
                int n = 0;
                foreach (var (action,time) in poses)
                {
                    pet.PreviewMotion(action,time,5000); pet.UpdateLayout();
                    var frame = (BitmapSource)image.Source;
                    double pixelScale = image.Height / Math.Max(frame.PixelWidth,frame.PixelHeight);
                    measurements.Add(new { character=c.Id,outfit,action,frame=pet.DrawnFrame,pixelScale,
                        height=image.Height*pet.Art.VisibleHeight(frame), imageHeight=image.Height });
                    Require(pet.State.Size == 240,c.Id+"/"+outfit+"/"+action+": user size is unchanged");
                    Require(Math.Abs(Canvas.GetTop(image)+image.Height*pet.Art.GroundLine(frame)+pet.AirborneOffset-468)<.01,
                        c.Id+"/"+outfit+"/"+action+": sole baseline is unchanged");
                    if (!baseline && c.Category == CharacterStyles.Chibi && outfit != "sports" && action == "build")
                        Require(image.Height*pet.Art.VisibleHeight(frame)/standing is >=.85 and <=1.2,
                            c.Id+"/"+outfit+": seated block building does not shrink the character");
                    if (!baseline && c.Category == CharacterStyles.Chibi && outfit == "original" && action is "jump" or "think")
                        Require(image.Height*pet.Art.VisibleHeight(frame)/standing is >=.88 and <=1.5,
                            c.Id+"/"+action+": middle legacy pose retains character scale");
                    if (c.FamilyId is "whale" or "gpt")
                    {
                        var shot = new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32); shot.Render(canvas);
                        double x = n%4*280, y=n/4*340;
                        dc.DrawImage(new CroppedBitmap(shot,new Int32Rect(0,128,560,352)),new Rect(x,y,280,176));
                        dc.DrawText(new FormattedText($"{action} · {time:0} ms\nheight {image.Height*pet.Art.VisibleHeight(frame):0.0} · px {pixelScale:0.000}",
                            CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,CloudTheme.Ink,1),new Point(x+18,y+187));
                    }
                    n++;
                }
            }
            if (!baseline)
            {
                foreach (var (action,clip) in (outfit == "original" ? c.Motions : c.Outfits[outfit].Motions)
                             .Where(p => p.Value.ReferenceHeightPixels > 0))
                {
                    double elapsed = 0; var scales = new List<double>();
                    int[] order = clip.Frames ?? Enumerable.Range(0,clip.Columns*clip.Rows).ToArray();
                    for (int i=0;i<order.Length;i++)
                    {
                        pet.PreviewMotion(action,elapsed+1,20000);
                        var frame=(BitmapSource)image.Source;
                        double calibratedFactor=clip.FrameScaleFactors?[pet.DrawnFrame] ?? 1;
                        scales.Add(image.Height/Math.Max(frame.PixelWidth,frame.PixelHeight)/calibratedFactor);
                        elapsed+=clip.FrameMs?[i] ?? 240;
                    }
                    Require(scales.Max()/scales.Min()<1.03,c.Id+"/"+outfit+"/"+action+": source pixel scale follows the original calibrated factors through the full clip");
                }
                // Returning from a calibrated pose must restore the exact idle
                // size, including after repeated action changes at slider limits.
                foreach (double userSize in new[] {120d,300d})
                {
                    pet.State.Size=userSize; pet.ApplySettings();
                    double expected=image.Height;
                    foreach (var (action,time) in poses)
                    {
                        pet.PreviewMotion(action,time,5000); pet.PreviewMotion("idle",0,0);
                        Require(Math.Abs(image.Height-expected)<.001,c.Id+"/"+outfit+"/"+action+": returning to idle has no accumulated scaling");
                    }
                }
                pet.State.Size=240; pet.ApplySettings();
            }
            if (c.FamilyId is "whale" or "gpt")
            {
                var shot = new RenderTargetBitmap(1120,1020,96,96,PixelFormats.Pbgra32); shot.Render(visual);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(shot));
                using var stream = File.Create(Path.Combine(output,c.Id+"-"+outfit+"-scale.png")); png.Save(stream);
            }
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }
        File.WriteAllText(Path.Combine(output,"scale-measurements.json"),JsonSerializer.Serialize(measurements,Json.Options));
        File.WriteAllLines(Path.Combine(output,"scale-check.txt"),checks.Append($"{checks.Count} scale checks passed."));
        pet.EndPreview();
    }
}
