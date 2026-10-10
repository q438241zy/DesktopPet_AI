using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class ClubVerification
{
    internal static async Task Run(PetWindow pet,string output,bool pilot,string? appearance=null)
    {
        var checks=new List<string>();
        void Require(bool pass,string message) { if(!pass)throw new InvalidOperationException(message);checks.Add("PASS "+message); }
        byte[] Capture(string? file=null,FrameworkElement? element=null)
        {
            element??=(FrameworkElement)pet.Content;element.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth),(int)Math.Ceiling(element.ActualHeight),96,96,PixelFormats.Pbgra32);
            if(element is ClubPoseVisual)
            {
                // Viewport3D retains its Canvas layout offset when rendered directly.
                // A VisualBrush captures the actor's own bounds at a zero origin.
                var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(element){Stretch=Stretch.Fill},null,new Rect(0,0,element.ActualWidth,element.ActualHeight));
                bitmap.Render(drawing);
            }
            else bitmap.Render(element);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);
            var bytes=stream.ToArray();if(file is not null)File.WriteAllBytes(Path.Combine(output,file+".png"),bytes);return SHA256.HashData(bytes);
        }
        Directory.CreateDirectory(output);pet.State.Wander=false;pet.State.ReducedMotion=false;pet.State.Size=250;pet.ApplySettings();
        pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
        var characters=pet.Catalog.Characters.Where(c=>!pilot || c.FamilyId=="whale").ToArray();
        var appearances=(from c in characters from outfit in Catalog.BuiltInOutfits
            where appearance is null || appearance==c.Id || appearance==c.Id+"/"+outfit
            select (c,outfit)).ToArray();
        if(appearances.Length==0)throw new ArgumentException("No matching club appearance: "+appearance);
        // Complete every live journey before the first bitmap capture. Prior
        // preview Yield calls or bulk screenshots must not influence live timing.
        // A focused appearance request still performs its full static artwork audit.
        if(appearance is null)
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(pet.Dispatcher,DispatcherPriority.Normal));
            // Real dispatcher frames prove the runtime clocks keep advancing with menus visible.
            foreach(string id in new[]{"whale","deepseek-adult"})foreach(int side in new[]{-1,1})
            {
                pet.SelectCharacter(id);pet.State.Outfits[id]="swim";pet.ApplySettings();
                pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
                var canvas=(Canvas)pet.Content;
                var actor=canvas.Children.OfType<Image>().Single();
                double start=pet.Left;
                var pursued=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void Presented(object? sender,EventArgs e)
                {
                    if(e is RenderingEventArgs && pet.CurrentClub=="butterfly" && (pet.Left-start)*side>2
                        && actor.RenderTransform is ScaleTransform facing && facing.ScaleX==side) pursued.TrySetResult();
                }
                CompositionTarget.Rendering+=Presented;
                try
                {
                    pet.RunInteraction("butterfly");pet.ClubEffects.Select(new Point(280+side*80,200));
                    Require(await Task.WhenAny(pursued.Task,Task.Delay(1800))==pursued.Task,
                        $"{id}: butterfly pursuit faces and walks toward side {side}");
                    await pursued.Task;
                }
                finally { CompositionTarget.Rendering-=Presented; }
                pet.IsHitTestVisible=true;
                Require(ReferenceEquals(canvas.InputHitTest(new Point(50,330)),pet.ClubEffects),"empty space can receive butterfly lure clicks");
                Require(ReferenceEquals(canvas.InputHitTest(new Point(280,400)),pet.InputSurface),"butterfly effects leave the pet available for dragging");
                pet.IsHitTestVisible=false;
                var landingDeadline=System.Diagnostics.Stopwatch.StartNew();
                while(!pet.ClubEffects.ButterflyLanded && landingDeadline.ElapsedMilliseconds<13000)await Task.Delay(20);
                Require(pet.ClubEffects.ButterflyLanded,$"{id}: butterfly settles after the full nearby stroll");pet.StopInteraction();
            }
            pet.SelectCharacter("whale");pet.State.Outfits["whale"]="original";pet.ApplySettings();pet.RunInteraction("stars");
            await Task.Delay(850);pet.HandleRightClick();var before=pet.ActiveClubVisual!.Current;await Task.Delay(450);
            Require(pet.IsMenuOpen && pet.CurrentClub=="stars" && pet.ActiveClubVisual.Current!=before,"real dispatcher animates while menu remains open");
            pet.StopInteraction();pet.RunInteraction("walk");await Task.Delay(300);double left=pet.Left;pet.HandleRightClick();await Task.Delay(450);
            Require(pet.IsExploring && Math.Abs(pet.Left-left)>2 && pet.IsMenuOpen,"real walking continues behind radial menu");
            pet.StopInteraction();pet.State.ReducedMotion=true;pet.RunInteraction("stretch");await Task.Delay(200);var reduced=Capture();await Task.Delay(200);
            Require(Capture().SequenceEqual(reduced),"reduced motion holds a stable stretch pose");pet.StopInteraction();pet.State.ReducedMotion=false;
        }
        // Static artwork comparisons and screenshots run only after live checks.
        foreach(var (c,outfit) in appearances)
        {
            pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]=outfit;pet.ApplySettings();pet.BeginPreview();
            pet.PreviewMotion("idle",0,0);Capture($"club-{c.Id}-{outfit}-idle");
            foreach(string key in ClubMotion.Actions)
            {
                pet.StopInteraction();pet.PreviewClub(key,650);await Dispatcher.Yield(DispatcherPriority.Render);
                var actorBefore=pet.ActiveClubVisual is {} firstVisual?Capture(pilot?$"debug-{c.Id}-{outfit}-{key}-650":null,element:firstVisual):null;
                var first=Capture();double plane=pet.ActiveClubVisual?.Height??0;
                pet.PreviewClub(key,1650);await Dispatcher.Yield(DispatcherPriority.Render);
                Require(!Capture().SequenceEqual(first),$"{c.Id}/{outfit}/{key}: actual rendered motion changes");
                Require(pet.State.Outfit==outfit && pet.State.Character==c.Id,$"{c.Id}/{outfit}/{key}: appearance preserved");
                if(ClubMotion.HasPoses(key,c.MotionFor(outfit,key)) && ClubPoseVisual.Supports(c,c.MotionFor(outfit,key)))
                {
                    Require(pet.ActiveClubVisual?.Appearance==c.Id+"/"+outfit && pet.ActiveClubVisual.Height==plane,$"{c.Id}/{outfit}/{key}: dedicated continuous poses and fixed body scale");
                    var actor=pet.ActiveClubVisual!;var body=actorBefore!;
                    pet.PreviewClub(key,1850);await Dispatcher.Yield(DispatcherPriority.Render);
                    Require(!Capture(pilot?$"debug-{c.Id}-{outfit}-{key}-1850":null,element:actor).SequenceEqual(body),$"{c.Id}/{outfit}/{key}: the actor changes independently of effect objects");
                }
                if(key is "comb" or "wipe")
                    Require(pet.ActiveClubVisual is not null && c.MotionFor(outfit,key)?.BakedProps==true,$"{c.Id}/{outfit}/{key}: dedicated hands and the single illustrated prop");
                var current=pet.CurrentAction;var phase=pet.ActiveClubVisual?.Current;
                pet.HandleRightClick();Require(pet.IsMenuOpen && pet.CurrentAction==current && pet.CurrentClub==key,$"{c.Id}/{outfit}/{key}: right click preserves action");
                pet.ShowMenu("care");Require(pet.CurrentAction==current && pet.ActiveClubVisual?.Current==phase,$"{c.Id}/{outfit}/{key}: submenu does not restart pose");pet.HandleRightClick();
                if(c.FamilyId is "whale" or "gpt" || key is "comb" or "wipe") Capture($"club-{c.Id}-{outfit}-{key}");
            }
            pet.PreviewClub("stars",7450);await Dispatcher.Yield(DispatcherPriority.Render);Require(pet.ClubScore==5,$"{c.Id}/{outfit}: automatically counted five stars");
            pet.PreviewClub("bubbles",800);await Dispatcher.Yield(DispatcherPriority.Render);Capture();Require(pet.ClubEffects.VisibleBubbles==0,"no bubble before blowing");
            pet.PreviewClub("bubbles",1700);await Dispatcher.Yield(DispatcherPriority.Render);Capture();Require(pet.ClubEffects.VisibleBubbles>0,"blowing emits bubbles");
            if(pet.ClubEffects.FirstTarget is {} point) { pet.ClubEffects.Select(point);Require(pet.ClubScore==1,"bubble click registers a pop"); }
            pet.StopInteraction();Require(pet.CurrentClub is null && pet.ActiveClubVisual is null,"replacement removes pose and effects");
            pet.EndPreview();
        }
        WriteResult();
        void WriteResult()=>File.WriteAllLines(Path.Combine(output,"club-check.txt"),checks.Append($"PASS {checks.Count} checks, {appearances.Length} appearances, six new interactions."));
    }
}
