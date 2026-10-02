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
    internal static async Task Run(PetWindow pet,string output,bool pilot)
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
        foreach(var c in characters)foreach(string outfit in new[]{"original","swim","wedding"})
        {
            pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]=outfit;pet.ApplySettings();pet.BeginPreview();
            foreach(string key in ClubMotion.Actions)
            {
                pet.StopInteraction();pet.PreviewClub(key,650);await Dispatcher.Yield(DispatcherPriority.Render);
                var actorBefore=pet.ActiveClubVisual is {} firstVisual?Capture(pilot?$"debug-{c.Id}-{outfit}-{key}-650":null,element:firstVisual):null;
                var first=Capture();double plane=pet.ActiveClubVisual?.Height??0;
                pet.PreviewClub(key,1650);await Dispatcher.Yield(DispatcherPriority.Render);
                Require(!Capture().SequenceEqual(first),$"{c.Id}/{outfit}/{key}: actual rendered motion changes");
                Require(pet.State.Outfit==outfit && pet.State.Character==c.Id,$"{c.Id}/{outfit}/{key}: appearance preserved");
                if(ClubMotion.HasPoses(key))
                {
                    Require(pet.ActiveClubVisual?.Appearance==c.Id+"/"+outfit && pet.ActiveClubVisual.Height==plane,$"{c.Id}/{outfit}/{key}: dedicated continuous poses and fixed body scale");
                    var actor=pet.ActiveClubVisual!;var body=actorBefore!;
                    pet.PreviewClub(key,1850);await Dispatcher.Yield(DispatcherPriority.Render);
                    Require(!Capture(pilot?$"debug-{c.Id}-{outfit}-{key}-1850":null,element:actor).SequenceEqual(body),$"{c.Id}/{outfit}/{key}: the actor changes independently of effect objects");
                }
                var current=pet.CurrentAction;var phase=pet.ActiveClubVisual?.Current;
                pet.HandleRightClick();Require(pet.IsMenuOpen && pet.CurrentAction==current && pet.CurrentClub==key,$"{c.Id}/{outfit}/{key}: right click preserves action");
                pet.ShowMenu("care");Require(pet.CurrentAction==current && pet.ActiveClubVisual?.Current==phase,$"{c.Id}/{outfit}/{key}: submenu does not restart pose");pet.HandleRightClick();
                if(c.FamilyId is "whale" or "gpt") Capture($"club-{c.Id}-{outfit}-{key}");
            }
            pet.PreviewClub("stars",7450);await Dispatcher.Yield(DispatcherPriority.Render);Require(pet.ClubScore==5,$"{c.Id}/{outfit}: automatically counted five stars");
            pet.PreviewClub("bubbles",800);await Dispatcher.Yield(DispatcherPriority.Render);Capture();Require(pet.ClubEffects.VisibleBubbles==0,"no bubble before blowing");
            pet.PreviewClub("bubbles",1700);await Dispatcher.Yield(DispatcherPriority.Render);Capture();Require(pet.ClubEffects.VisibleBubbles>0,"blowing emits bubbles");
            if(pet.ClubEffects.FirstTarget is {} point) { pet.ClubEffects.Select(point);Require(pet.ClubScore==1,"bubble click registers a pop"); }
            pet.StopInteraction();Require(pet.CurrentClub is null && pet.ActiveClubVisual is null,"replacement removes pose and effects");
            pet.EndPreview();
        }
        // Real dispatcher frames prove the runtime clocks keep advancing with menus visible.
        foreach(string id in new[]{"whale","deepseek-adult"})foreach(int side in new[]{-1,1})
        {
            pet.SelectCharacter(id);pet.State.Outfits[id]="swim";pet.ApplySettings();
            pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
            pet.RunInteraction("butterfly");double start=pet.Left;pet.ClubEffects.Select(new Point(280+side*80,200));await Task.Delay(350);
            var canvas=(Canvas)pet.Content;
            pet.IsHitTestVisible=true;
            Require(ReferenceEquals(canvas.InputHitTest(new Point(50,330)),pet.ClubEffects),"empty space can receive butterfly lure clicks");
            Require(canvas.InputHitTest(new Point(280,400)) is Image,"butterfly effects leave the pet available for dragging");
            pet.IsHitTestVisible=false;
            var actor=((Canvas)pet.Content).Children.OfType<Image>().Single();
            Require((pet.Left-start)*side>2 && ((ScaleTransform)actor.RenderTransform).ScaleX==side,$"{id}: butterfly pursuit faces and walks toward side {side}");
            await Task.Delay(1600);Require(pet.ClubEffects.ButterflyLanded,$"{id}: butterfly reaches the palm after walking");pet.StopInteraction();
        }
        pet.SelectCharacter("whale");pet.State.Outfits["whale"]="original";pet.ApplySettings();pet.RunInteraction("stars");
        await Task.Delay(850);pet.HandleRightClick();var before=pet.ActiveClubVisual!.Current;await Task.Delay(450);
        Require(pet.IsMenuOpen && pet.CurrentClub=="stars" && pet.ActiveClubVisual.Current!=before,"real dispatcher animates while menu remains open");
        pet.StopInteraction();pet.RunInteraction("walk");await Task.Delay(300);double left=pet.Left;pet.HandleRightClick();await Task.Delay(450);
        Require(pet.IsExploring && Math.Abs(pet.Left-left)>2 && pet.IsMenuOpen,"real walking continues behind radial menu");
        pet.StopInteraction();pet.State.ReducedMotion=true;pet.RunInteraction("stretch");await Task.Delay(200);var reduced=Capture();await Task.Delay(200);
        Require(Capture().SequenceEqual(reduced),"reduced motion holds a stable stretch pose");pet.StopInteraction();pet.State.ReducedMotion=false;
        File.WriteAllLines(Path.Combine(output,"club-check.txt"),checks.Append($"PASS {checks.Count} checks, {characters.Length*3} appearances, six new interactions."));
    }
}
