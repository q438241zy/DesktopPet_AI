using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class FiveVerification
{
    internal static async Task Run(PetWindow pet,string output,bool available)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();
        void Check(bool pass,string text){if(!pass)throw new InvalidOperationException(text);checks.Add("PASS "+text);}
        async Task WaitFor(Func<bool> predicate)
        {
            var deadline=DateTime.UtcNow.AddSeconds(10);
            while(!predicate() && DateTime.UtcNow<deadline)await Task.Delay(40);
        }
        void Capture(string filename,BitmapSource? image=null)
        {
            if(image is null){var canvas=(Canvas)pet.Content;canvas.UpdateLayout();var bitmap=new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32);bitmap.Render(canvas);image=bitmap;}
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(Path.Combine(output,filename+".png"));encoder.Save(stream);
        }
        pet.State.Wander=pet.State.ReducedMotion=false;pet.State.Size=250;pet.ApplySettings();
        pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
        System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(pet.Dispatcher,DispatcherPriority.Normal));
        foreach(string id in new[]{"whale","deepseek-adult"})
        {
            pet.SelectCharacter(id);pet.State.Outfits[id]="original";pet.ApplySettings();pet.RunInteraction("highfive");await Task.Delay(850);
            Check(pet.ActiveFive?.Phase=="offer"&&pet.ActiveFive.Time>=750,id+": realtime offer advances");
            var five=pet.ActiveFive!;pet.FiveEffects.Activate();pet.HandleRightClick();await Task.Delay(550);
            Check(five.HighCount==1&&ReferenceEquals(five,pet.ActiveFive)&&pet.IsMenuOpen,id+": hand contact while right-click preserves interaction");
            await Task.Delay(1450);Check(five.HighCount==1&&five.Phase=="offer",id+": no idle auto-contact");
            pet.StopInteraction();pet.RunInteraction("rps");pet.FiveCommand("paper");await Task.Delay(1100);
            Check(pet.ActiveFive?.Phase=="countdown"&&pet.ActiveFive.Pet is null,id+": both fists count before reveal");
            await WaitFor(()=>pet.ActiveFive?.Phase=="revealed");Check(pet.ActiveFive?.Phase=="revealed"&&pet.ActiveFive.Outcome is not null,id+": realtime reveal completes");
            pet.RunInteraction("gift");pet.FiveCommand("deliver");await Task.Delay(750);pet.FiveCommand("unwrap");int before=pet.State.Treasures.Count;
            await Task.Delay(350);pet.StopInteraction();await Task.Delay(900);Check(pet.State.Treasures.Count==before,id+": stopping an open cancels reward");
        }
        var appearances=(from c in pet.Catalog.Characters from o in Catalog.BuiltInOutfits where !available||c.FiveFor(o)!=null select(c,o)).ToArray();
        Check(available||appearances.Length==64,"full rollout contains 64 appearances");pet.BeginPreview();
        foreach(var(c,outfit) in appearances)
        {
            pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]=outfit;pet.ApplySettings();string label=c.Id+"/"+outfit;
            var data=c.FiveFor(outfit);Check(data is not null,label+": artwork installed");data!.Validate();
            foreach(var(name,p) in data.Poses)
            {
                var atlas=data.Atlases[p.Atlas];var frame=pet.Art.Frame(c,atlas,p.Frame);
                Check(frame.PixelHeight>=200 && p.FootY<=frame.PixelHeight && p.FootX<=frame.PixelWidth,label+"/"+name+": measured crop and foot in bounds");
            }
            foreach(var(key,times) in new[]{("highfive",new[]{0,900,1400,1600}), ("rps",new[]{0,600,900,1200,2500,3100}), ("gift",new[]{0,600,1300,1900,2700}), ("read",new[]{0,1000,1800,2900}), ("photo",new[]{0,450,1800,3500})})
            foreach(int time in times)
            {
                pet.PreviewFive(key,time);var pose=data.Poses[pet.ActiveFive!.Pose];var sheet=data.Atlases[pose.Atlas];var expected=pet.Art.Frame(c,sheet,pose.Frame);
                var actor=((Canvas)pet.Content).Children.OfType<Image>().Single();
                Check(ReferenceEquals(expected,actor.Source)&&pet.State.Outfit==outfit,label+"/"+key+"@"+time+": exact same-outfit pose");
                Check(Math.Abs(Canvas.GetTop(actor)+pose.FootY*actor.Height/expected.PixelHeight-468)<.01,label+"/"+key+"@"+time+": fixed floor");
                Check(pet.ActiveMotion is null && pet.ActiveAuthoredVisual is null,label+": no unrelated rig or hand warping");
                if(c.FamilyId is "whale" or "gpt" && time==times.Last())Capture(c.Id+"-"+outfit+"-"+key);
            }
            pet.PreviewFive("photo",3500);var photo=pet.CreateFivePhoto();Check(photo.PixelWidth==720&&photo.PixelHeight==880,label+": photo export");
            if(c.FamilyId is "whale" or "gpt")Capture(c.Id+"-"+outfit+"-photo-card",photo);
            pet.StopInteraction();Check(pet.ActiveFive is null&&pet.FivePanel.Visibility==Visibility.Collapsed,label+": cancellation removes inline controls");
        }
        pet.EndPreview();pet.Save();var restored=new StateStore(output).Load();Check(restored.Treasures.Count==pet.State.Treasures.Count,"collection survives state reload");
        File.WriteAllLines(Path.Combine(output,"five-check.txt"),checks.Append($"{checks.Count} checks; {appearances.Length} appearances."));
    }
}
