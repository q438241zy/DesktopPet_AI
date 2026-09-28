using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class PlacementVerification
{
    internal static async Task Run(PetWindow pet, string output)
    {
        var checks = new List<string>();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        async Task Until(Func<bool> ok) { var time=Stopwatch.StartNew(); while(!ok()) { if(time.ElapsedMilliseconds>4000) throw new InvalidOperationException("placement timed out: "+pet.CurrentAction); await Task.Delay(25); } }
        var canvas=(Canvas)pet.Content; var sprite=canvas.Children.OfType<Image>().Single();
        pet.IsHitTestVisible=false; pet.State.Size=200; pet.State.Wander=pet.State.ReducedMotion=false; pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now));
        foreach(var c in pet.Catalog.Characters)
        foreach(string outfit in new[]{"original","swim","wedding"})
        {
            pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id]=outfit; pet.ApplySettings();
            pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280; pet.Top=pet.WorkArea.Bottom-468;
            pet.BeginLift(); double x=pet.Left,y=pet.Top-130;
            foreach(double dx in new[]{-60d,70,-85,100,-60,0}) pet.MoveLift(x+dx,y);
            Require(pet.CurrentAction=="pickup" && sprite.RenderTransform is ScaleTransform,c.Id+"/"+outfit+": dragging never becomes shaking or a pendulum");
            pet.ReleaseLift(); await Task.Delay(70);
            Require(!pet.IsDropping && Math.Abs(pet.Top-y)<.1,c.Id+"/"+outfit+": manual placement detaches from taskbar");
            Require(Math.Abs(new StateStore(output).Load().Top!.Value-y)<.1,c.Id+"/"+outfit+": chosen height is persisted");
            var frame=(BitmapSource)sprite.Source;
            Require(Math.Abs(Canvas.GetTop(sprite)+sprite.Height*pet.Art.GroundLine(frame)-468)<.15,c.Id+"/"+outfit+": foot baseline uses rendered alpha bounds");
            if(c.FamilyId=="whale")
            {
                pet.DropToFloor(); await Task.Delay(80); double interrupted=pet.Top;
                pet.BeginLift(); Require(!pet.IsDropping && Math.Abs(pet.Top-interrupted)<.1,c.Id+"/"+outfit+": grabbing a falling pet does not teleport it to taskbar");
                pet.MoveLift(x,y); pet.ReleaseLift(); await Task.Delay(550);
                Require(!pet.IsDropping && Math.Abs(pet.Top-y)<.1,c.Id+"/"+outfit+": released pet stays placed across timer updates");
                pet.StartWalk(false,1); Require(pet.IsDropping,c.Id+"/"+outfit+": walking first requests a natural return to floor");
                await Until(()=>pet.CurrentAction=="walk");
                Require(Math.Abs(pet.Top+468-pet.WorkArea.Bottom)<.1,c.Id+"/"+outfit+": natural fall completes before walking");
                pet.StopInteraction(); pet.OpenChat();
                Require(pet.ActiveChat is { } chat && Window.GetWindow(chat)==pet && Application.Current.Windows.OfType<Window>().Count()==1,c.Id+"/"+outfit+": chat is embedded and creates no window");
                int earlier = pet.Chat.History.Count;
                var response=pet.Chat.SendText("你好"); await Task.Delay(300);
                Require(pet.CurrentAction=="thinking" && pet.Chat.IsThinking && pet.Chat.History.Count==earlier+1,c.Id+"/"+outfit+": visible thinking precedes reply");
                await response;
                Require(pet.CurrentAction=="chat" && pet.Chat.History.Count==earlier+2 && pet.Chat.ReplyText.Length>4,c.Id+"/"+outfit+": inline reply is visible after thinking");
                var bg=canvas.Background; canvas.Background=CloudTheme.Brush("#EEF2F7"); canvas.UpdateLayout();
                var capture=new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32); capture.Render(canvas); canvas.Background=bg;
                var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(capture));
                using(var stream=File.Create(Path.Combine(output,$"inline-{c.Id}-{outfit}.png"))) png.Save(stream);
                pet.StopInteraction();
            }
        }
        pet.State.ReducedMotion=true; pet.Top=pet.WorkArea.Bottom-468-110; pet.DropToFloor();
        Require(!pet.IsDropping && Math.Abs(pet.Top+468-pet.WorkArea.Bottom)<.1,"reduced motion returns to ground without a falling loop");
        File.WriteAllLines(Path.Combine(output,"placement-check.txt"),checks.Append($"{checks.Count} placement and inline chat checks passed."));
    }
}
