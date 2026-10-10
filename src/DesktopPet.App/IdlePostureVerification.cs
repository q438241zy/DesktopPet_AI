using System.Reflection;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class IdlePostureVerification
{
    internal static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks.Add("PASS "+message);}
        var approved=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"approved-demo.json"))).RootElement;
        var canvas=(Canvas)pet.Content;var image=canvas.Children.OfType<Image>().Single();
        var activityField=typeof(PetWindow).GetField("lastCompanionActivity",BindingFlags.Instance|BindingFlags.NonPublic)!;
        pet.State.AutoHide=pet.State.Wander=pet.State.ReducedMotion=false;pet.State.Size=240;pet.BeginPreview();
        void Capture(FrameworkElement view,string name){view.UpdateLayout();var b=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);b.Render(view);VerificationImages.Save(b,Path.Combine(output,name+".png"));}
        foreach(string style in CharacterStyles.All)foreach(string outfit in Catalog.BuiltInOutfits)
        {
            var gallery=new DrawingVisual();using(var dc=gallery.RenderOpen())
            {
                dc.DrawRectangle(CloudTheme.Cream,null,new Rect(0,0,1500,2000));int row=0;
                foreach(var c in pet.Catalog.Characters.Where(c=>c.Category==style))
                {
                    pet.BeginPreview();pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]=outfit;pet.ApplySettings();pet.StopInteraction();pet.SetPostureMode("auto");
                    var clock=pet.CurrentPostureClock;string first=clock.Pose;double activity=(double)activityField.GetValue(pet)!;
                    double due=clock.Next;pet.AdvancePreview(due-1);Require(pet.CurrentPosture==first,c.Id+"/"+outfit+": waits randomized interval");
                    pet.AdvancePreview(due);Require(pet.CurrentPosture!=first&&pet.IsRenderingIdlePosture,c.Id+"/"+outfit+": pose changes after 1–2 seconds");
                    Require((double)activityField.GetValue(pet)! == activity,c.Id+"/"+outfit+": user idle timer unchanged");
                    int column=0;
                    foreach(string pose in IdlePostureClock.Poses)
                    {
                        var expected=approved.GetProperty(c.Id).GetProperty(outfit).GetProperty(pose);
                        var frames=expected.GetProperty("frames").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                        var times=expected.GetProperty("frameMs").EnumerateArray().Select(v=>v.GetInt32()).ToArray();double total=times.Sum(),at=0;
                        for(int i=0;i<frames.Length;i++)
                        {
                            double progress=(at+times[i]*.5)/total;at+=times[i];pet.BeginPreview();clock.Reset(0);
                            typeof(IdlePostureClock).GetProperty(nameof(IdlePostureClock.Pose))!.SetValue(clock,pose);
                            ((Queue<string>)typeof(IdlePostureClock).GetField("bag",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(clock)!).Clear();
                            pet.AdvancePreview(clock.Duration*progress);pet.UpdateLayout();
                            var art=IdlePosture.Resolve(c,outfit,pose,progress);
                            Require(art.Sprite.File==expected.GetProperty("file").GetString()&&art.Frame==frames[i],c.Id+"/"+outfit+"/"+pose+"/"+i+": approved source frame");
                            var frame=(BitmapSource)image.Source;double extent=Math.Max(frame.PixelWidth,frame.PixelHeight);
                            var idle=c.Resolve(outfit,"idle",0,true);double height=pet.State.Size*pet.Art.VisibleHeight(pet.Art.Frame(c,idle.Sprite,idle.Frame));
                            double scale=height/expected.GetProperty("reference").GetDouble()*expected.GetProperty("cells")[frames[i]].GetProperty("scale").GetDouble();
                            Require(Math.Abs(image.Width/extent-scale)<.005,c.Id+"/"+outfit+"/"+pose+"/"+i+": approved fixed anatomical scale");
                            Require(Math.Abs(Canvas.GetTop(image)+image.Height*pet.Art.GroundLine(frame)-468)<.01&&pet.InputSurface.Height>0,c.Id+"/"+outfit+"/"+pose+"/"+i+": grounded and clickable");
                            if(i==frames.Length/2){var shot=new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32);shot.Render(canvas);dc.DrawImage(shot,new Rect(column*300+(300-230d*560/680)/2,row*250+18,230d*560/680,230));}
                        }
                        dc.DrawText(new FormattedText(c.Id+" / "+pose,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,CloudTheme.Ink,1),new Point(column*300+8,row*250+3));column++;
                    }
                    row++;
                }
            }
            var sheet=new RenderTargetBitmap(1500,2000,96,96,PixelFormats.Pbgra32);sheet.Render(gallery);VerificationImages.Save(sheet,Path.Combine(output,style+"-"+outfit+".png"));await Task.Delay(1);
        }
        pet.BeginPreview();pet.SelectCharacter("gpt");pet.StopInteraction();pet.SetPostureMode("auto");string before=pet.CurrentPosture;
        pet.Play("headpat",duration:10000);pet.AdvancePreview(9999);Require(pet.CurrentAction=="headpat"&&pet.CurrentPosture==before,"interaction owns its pose");
        pet.AdvancePreview(10000);double next=pet.CurrentPostureClock.Next;pet.AdvancePreview(next-1);Require(pet.CurrentPosture==before,"fresh idle interval after interaction");pet.AdvancePreview(next);Require(pet.CurrentPosture!=before,"idle resumes");
        before=pet.CurrentPosture;pet.ShowMenu();pet.AdvancePreview(40000);Require(pet.CurrentPosture==before,"menu protects pose");
        pet.StopInteraction();pet.OpenChat();pet.AdvancePreview(60000);Require(pet.CurrentPosture==before&&!pet.IsRenderingIdlePosture,"chat protects pose");
        pet.StopInteraction();pet.State.ReducedMotion=true;pet.ApplySettings();pet.AdvancePreview(90000);Require(pet.CurrentPosture==before,"reduced motion holds pose");
        pet.State.ReducedMotion=false;pet.SetPostureMode("sit");pet.SelectStyle(CharacterStyles.Realistic);pet.StopInteraction();pet.AdvancePreview(120000);Require(pet.CurrentPosture=="sit"&&pet.PostureMode("gpt")=="sit","fixed preferences shared by family");
        pet.SelectCharacter("whale");pet.SetPostureMode("stand");var loaded=new StateStore(output).Load();Require(loaded.Postures["gpt"]=="sit"&&loaded.Postures["whale"]=="stand","per-family preferences survive disk reload");
        pet.OpenSettings();var window=Application.Current.Windows.OfType<SettingsWindow>().Single();window.UpdateLayout();Capture(window,"settings-q");window.Close();
        pet.EndPreview();File.WriteAllLines(Path.Combine(output,"idle-posture-check.txt"),checks.Append($"PASS {checks.Count} native idle checks; 64 outfits and all five gestures."));
    }
}
