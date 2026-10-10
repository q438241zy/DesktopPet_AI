using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Real desktop clocks and routed input, followed by same-outfit image checks.</summary>
internal static class PlayUpdateVerification
{
    internal static async Task Run(PetWindow pet,string output)
    {
        Directory.CreateDirectory(output);var checks=new List<string>();string report=Path.Combine(output,"play-update-check.txt");
        void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);checks.Add("PASS "+why);File.WriteAllLines(report,checks);}
        async Task Until(Func<bool> test,int timeout,string why){var watch=Stopwatch.StartNew();while(!test()){if(watch.ElapsedMilliseconds>timeout)throw new TimeoutException(why);await Task.Delay(30);}}
        var canvas=(Canvas)pet.Content;var image=canvas.Children.OfType<Image>().Single();
        double X()=>pet.Left+canvas.RenderTransform.Value.OffsetX;
        void Capture(string name){canvas.UpdateLayout();var b=new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32);b.Render(canvas);VerificationImages.Save(b,Path.Combine(output,name+".png"));}
        IEnumerable<Button> Buttons(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);if(c is Button b)yield return b;foreach(var n in Buttons(c))yield return n;}}
        pet.State.Size=240;pet.State.Wander=pet.State.AutoHide=pet.State.ReducedMotion=false;pet.ConfigureWork(false,10);pet.ApplySettings();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(pet.Dispatcher));
        foreach(string id in new[]{"gpt","gpt-adult"})
        {
            pet.SelectCharacter(id);pet.State.Outfits[id]="sports";pet.ApplySettings();pet.StopInteraction();pet.SetPostureMode("auto");
            pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
            var poses=new HashSet<string>{pet.CurrentPosture};var intervals=new List<double>();var watch=Stopwatch.StartNew();string previous=pet.CurrentPosture;double changedAt=0;
            while(watch.ElapsedMilliseconds<13000){await Task.Delay(30);if(pet.CurrentPosture!=previous){intervals.Add(watch.Elapsed.TotalSeconds-changedAt);changedAt=watch.Elapsed.TotalSeconds;previous=pet.CurrentPosture;poses.Add(previous);}}
            Require(poses.SetEquals(IdlePostureClock.Poses)&&intervals.All(t=>t>=.85&&t<=2.5),id+": live 1–2s timer visits all five poses");
            int gifts=pet.State.Treasures.Count;pet.RunInteraction("gift");
            Require(pet.ActiveFive?.Phase=="delivering"&&!Buttons(pet.FivePanel).Any(b=>new[]{"递出礼物","一起拆开"}.Contains(AutomationProperties.GetName(b))),id+": gift starts without prompts");
            await Until(()=>pet.ActiveFive is null,10000,"gift complete");Require(pet.State.Treasures.Count==gifts+1,id+": automatic gift commits once and ends");
            pet.RunInteraction("gift");await Until(()=>pet.ActiveFive?.Phase=="opening",5000,"gift opening");pet.StopInteraction();await Task.Delay(1200);Require(pet.State.Treasures.Count==gifts+1,id+": cancel opening prevents reward");
            pet.RunInteraction("butterfly");double origin=X();var positions=new List<double>();var frameSet=new HashSet<int>();var sides=new HashSet<int>();double last=origin;
            int previousDirection=(int)typeof(PetWindow).GetField("direction",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pet)!;watch.Restart();
            while(watch.ElapsedMilliseconds<11400)
            {
                await Task.Delay(30);double x=X();positions.Add(x);frameSet.Add(pet.DrawnFrame);
                int direction=(int)typeof(PetWindow).GetField("direction",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pet)!;
                if(Math.Abs(x-last)>.02){int side=Math.Sign(x-last);sides.Add(side);var clip=pet.Character.MotionFor(pet.State.Outfit,"walk")!;
                    Require(((ScaleTransform)image.RenderTransform).ScaleX==DesktopWalk.ScaleX(direction,clip.Facing),id+": current pose faces the current travel direction");
                    if(direction==previousDirection)Require(side==direction,$"{id}: travel sign stable between turns at {watch.ElapsedMilliseconds}ms, delta={x-last:F4}");}
                previousDirection=direction;
                last=x;
            }
            Require(positions.Select(x=>Math.Round(x,2)).Distinct().Count()>80&&frameSet.Count>=8&&sides.Count==2,id+": live butterfly walks smoothly both ways with actual gait");
            Require(positions.All(x=>Math.Abs(x-origin)<=105.1),id+": butterfly stays in a small area");
            pet.RunInteraction("butterfly");await Task.Delay(600);pet.BeginLift();Require(pet.CurrentClub is null&&pet.CurrentAction=="pickup",id+": picking up interrupts butterfly");pet.MoveLift(pet.Left,pet.Top-90);pet.ReleaseLift();Require(!pet.IsDropping,id+": ordinary release remains where placed");pet.StopInteraction();
        }
        pet.SelectCharacter("gpt-adult");pet.State.Outfits[pet.State.Character]="sports";pet.ApplySettings();pet.RunInteraction("read");var reading=pet.ActiveFive!;string story=reading.Story.Id;int readBefore=pet.State.ReadStories.GetValueOrDefault(story);
        Require(reading.Phase=="reading"&&!Buttons(pet.FivePanel).Any(b=>new[]{"开始共读","下一句","读完"}.Contains(AutomationProperties.GetName(b))),"random story starts reading immediately");
        await Task.Delay(1400);pet.FiveCommand("pause");double held=reading.Time;await Task.Delay(650);Require(reading.Time==held&&reading.Phase=="paused","pause preserves sentence time");pet.FiveCommand("pause");
        await Until(()=>pet.ActiveFive is null,100000,"automatic story completion");Require(pet.State.ReadStories.GetValueOrDefault(story)==readBefore+1,"full real-time story automatically pages and collects exactly once");
        Require(StoryLibrary.All.Count==10,"ten native stories available");
        pet.BeginPreview();
        foreach(string group in new[]{"interact","play"})
        {
            var seen=new HashSet<string>();string current=group;int pages=0;
            do{var entries=PetActions.Menu(current);Require(entries.Length<=8,"radial page is not crowded");foreach(var a in entries.Where(a=>!a.Key.StartsWith("group:")))Require(seen.Add(a.Key),"unique entry "+a.Key);pages++;current=entries.LastOrDefault(a=>a.Key.StartsWith("group:"+group+":"))?.Key[6..]??group+":0";}while(!current.EndsWith(":0")&&pages<5);
            Require(seen.SetEquals((group=="play"?PetActions.Games:PetActions.Interactions).Select(a=>a.Key)),group+": every approved action reachable");
        }
        Require(PetActions.Menu("root").Select(a=>a.Title).SequenceEqual(new[]{"聊天","互动","玩耍","设定","收起"}),"merged top-level menu");
        Require(!PetActions.Daily.Any(a=>a.Key=="photo")&&PetActions.Care.Any(a=>a.Title=="吃饭")&&PetActions.Care.Any(a=>a.Title=="吃零食"),"photo removed and food labels renamed");
        foreach(var c in pet.Catalog.Characters)foreach(string outfit in Catalog.BuiltInOutfits)
        {
            pet.BeginPreview();pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]=outfit;pet.ApplySettings();
            foreach(var(key,time) in new[]{("gift",600),("gift",1500),("gift",2500),("gift",3700),("gift",5200),("read",1000),("read",5300)})
            {
                pet.PreviewFive(key,time);var model=pet.ActiveFive!;var art=c.FiveFor(outfit)!;var pose=art.Poses[model.Pose];var sheet=art.Atlases[pose.Atlas];var expected=pet.Art.Frame(c,sheet,pose.Frame);
                Require(ReferenceEquals(image.Source,expected)&&pet.State.Outfit==outfit,c.Id+"/"+outfit+"/"+key+"@"+time+": own original image and costume");
                Require(Math.Abs(Canvas.GetTop(image)+pose.FootY*image.Height/expected.PixelHeight-468)<.02,c.Id+"/"+outfit+"/"+key+": constant baseline");
                if(c.FamilyId=="gpt"&&outfit=="sports"&&(time==1500||time==3700||key=="read"))Capture(c.Id+"-"+key+"-"+time);
            }
            pet.BeginPreview();pet.StopInteraction();pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;pet.RunInteraction("butterfly");double origin=X();pet.AdvancePreview(1300);
            Require(pet.CurrentClub=="butterfly"&&Math.Abs(X()-origin)>3&&pet.WalkPhaseMilliseconds>0,c.Id+"/"+outfit+": walks rather than sliding while seated");
            if(c.FamilyId=="gpt"&&outfit=="sports")Capture(c.Id+"-butterfly");pet.StopInteraction();
        }
        pet.SelectCharacter("gpt");pet.OpenChat();Require(!Buttons(pet.Chat).Any(b=>AutomationProperties.GetName(b).Contains("行事历")),"pure chat contains no calendar entry");Capture("gpt-chat");pet.StopInteraction();
        pet.OpenSettings();var window=Application.Current.Windows.OfType<SettingsWindow>().Single();window.UpdateLayout();
        var selected=Buttons(window).Single(b=>AutomationProperties.GetName(b)=="我的伙伴");Require(((Panel)selected.Content).Children.OfType<LineIcon>().Single().Selected,"navigation icon fills when selected");
        var shot=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);shot.Render(window);VerificationImages.Save(shot,Path.Combine(output,"gpt-control-panel.png"));window.Close();
        pet.EndPreview();pet.Save();var restored=new StateStore(output).Load();Require(restored.ReadStories.GetValueOrDefault(story)==readBefore+1&&restored.Treasures.Count>=2,"collections survive reload");
        File.AppendAllText(report,$"PASS {checks.Count} native play update checks.\n");
    }
}
