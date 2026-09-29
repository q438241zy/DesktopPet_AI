using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class ChoreographyVerification
{
    public static async Task Run(PetWindow pet, string output, bool pilot = false, bool availableOnly = false, string? appearance = null)
    {
        var checks = new List<string>();
        var captures = new List<Task>();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            {
                var child=VisualTreeHelper.GetChild(root,i); if(child is T typed) yield return typed;
                foreach(var nested in Find<T>(child)) yield return nested;
            }
        }
        async Task Until(Func<bool> predicate, string message, int timeout = 2500)
        { var elapsed = Stopwatch.StartNew(); while (!predicate()) { if (elapsed.ElapsedMilliseconds > timeout) throw new InvalidOperationException(message + ": " + pet.CurrentAction + "/" + pet.DrawnFrame); await Task.Delay(25); } }
        var canvas = (Canvas)pet.Content; var sprite = canvas.Children.OfType<Image>().Single();
        var effects = canvas.Children.OfType<InteractionFeedback>().Single();
        void Capture(FrameworkElement visual, string file)
        {
            var previous = canvas.Background; canvas.Background = CloudTheme.Brush("#EEF2F7");
            try
            {
                visual.UpdateLayout(); var image = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(visual);
                image.Freeze();
                // Encoding proof images must not block the dispatcher and skip a
                // short airborne/landing pose that the test is trying to observe.
                captures.Add(Task.Run(() =>
                {
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
                    using var stream = File.Create(Path.Combine(output, file + ".png")); png.Save(stream);
                }));
            }
            finally { canvas.Background = previous; }
        }
        bool QuietFeedback()
        { var image = new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32); image.Render(effects); byte[] data = new byte[560*680*4]; image.CopyPixels(data,560*4,0); return !data.Where((b,i)=>i%4==3 && b>0).Any(); }
        double BodyHeight() => sprite.Height * pet.Art.VisibleHeight((BitmapSource)sprite.Source);
        pet.IsHitTestVisible = false; pet.State.Size = 280; pet.State.Wander = pet.State.ReducedMotion = false;
        pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now)); pet.ApplySettings();
        var appearances = (from c in pet.Catalog.Characters where c.Category != "chibi"
                           from outfit in new[] { "original", "swim", "wedding" }
                           where !pilot || c.Id == "deepseek-adult" && outfit == "original"
                           where appearance is null || c.Id + "-" + outfit == appearance
                           where !availableOnly || c.MotionFor(outfit,"think")?.HeightRatios is not null
                           select (c,outfit)).ToArray();
        Require(appearances.Length == (pilot || appearance is not null ? 1 : 24) || availableOnly && appearances.Length > 0, "requested appearances are installed");
        foreach (var (character,outfit) in appearances)
        {
            string key = character.Id + "-" + outfit;
            pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit; pet.ApplySettings();
            pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.Top = pet.WorkArea.Bottom - 468;
            double standing = BodyHeight();
            pet.RunInteraction("think"); await Task.Delay(100);
            Require(pet.UsingDrawnAction && pet.ActiveMotion is null && pet.DrawnFrame == 2 && QuietFeedback(), key+": silent daydream pose without speaking effects"); Capture(canvas,"pose-"+key+"-daydream");
            pet.Play("listen",duration:0); await Task.Delay(60);
            Require(pet.DrawnFrame == 0 && pet.UsingDrawnAction, key+": closed-mouth listening pose");
            pet.Play("chat",duration:2000); await Until(()=>pet.DrawnFrame==1,"speaking pose");
            Require(QuietFeedback() && pet.ActiveMotion is null, key+": speaking uses a distinct drawn pose"); Capture(canvas,"pose-"+key+"-chat");
            pet.RunInteraction("jump"); Capture(canvas,"pose-"+key+"-jump-ready");
            await Until(()=>pet.DrawnFrame==4 && pet.AirborneOffset>35,"airborne knees");
            Require(pet.UsingDrawnAction && Math.Abs(Canvas.GetTop(sprite)+sprite.Height*pet.Art.GroundLine((BitmapSource)sprite.Source)-(468-pet.AirborneOffset))<.1,key+": jump changes pose and visibly leaves the ground"); Capture(canvas,"pose-"+key+"-jump-air");
            await Until(()=>pet.CurrentAction=="land" && pet.DrawnFrame==5,"landing pose"); Require(pet.AirborneOffset==0,key+": bent-knee landing reaches the ground in its own timed phase"); Capture(canvas,"pose-"+key+"-jump-land");
            await Until(()=>pet.CurrentAction=="idle","jump finishes");
            pet.RunInteraction("curl"); await Until(()=>pet.DrawnFrame==7,"seated curl");
            Require(BodyHeight()<standing*.64 && pet.ActiveMotion is null,key+": curl is compact sitting, never a standing-height squat"); Capture(canvas,"pose-"+key+"-curl");
            pet.RunInteraction("rest"); await Until(()=>pet.CurrentAction=="sleep","sleep transition");
            Require(pet.IsResting && pet.DrawnFrame==8 && BodyHeight()<standing*.44 && QuietFeedback(),key+": sleep lies on its side without floating rings or speech symbols");
            double before = sprite.Height; await Task.Delay(430); Require(Math.Abs(sprite.Height-before)>.01,key+": drawn sleeping body breathes subtly"); Capture(canvas,"pose-"+key+"-rest");
            pet.Touch(.2); Require(!pet.IsResting,key+": sleeping character can be awakened");
            pet.RunInteraction("blocks"); Capture(canvas,"pose-"+key+"-blocks-pick");
            await Until(()=>pet.DrawnFrame==10,"placing a block");
            Require(pet.UsingDrawnAction && character.MotionFor(outfit,"build")!.BakedProps && QuietFeedback(),key+": cubes are part of the hand-placement artwork, not an independent floating tower"); Capture(canvas,"pose-"+key+"-blocks-place");
            await Until(()=>pet.DrawnFrame==11,"completed block tower"); Capture(canvas,"pose-"+key+"-blocks-done");
            pet.StopInteraction(); pet.Top = pet.WorkArea.Bottom-468; pet.BeginLift(); pet.Top-=110; await Until(()=>pet.DrawnFrame==13,"hanging pickup pose");
            Require(pet.LiftedFromTaskbar && pet.UsingDrawnAction && pet.ActiveMotion is null,key+": lifting from the taskbar shows dangling legs"); Capture(canvas,"pose-"+key+"-lift");
            double placedTop = pet.Top; pet.ReleaseLift();
            Require(!pet.IsDropping && Math.Abs(pet.Top-placedTop)<.1,key+": manual release stays at the user's chosen position");
            pet.DropToFloor(); Require(pet.IsDropping,key+": natural return to floor is separate from manual placement"); await Until(()=>!pet.IsDropping,"fall settles");
            Require(Math.Abs(pet.Top+468-pet.WorkArea.Bottom)<.1 && pet.CurrentAction=="land",key+": release lands on the same work-area bottom"); Capture(canvas,"pose-"+key+"-drop-land");
            await Until(()=>pet.CurrentAction=="idle","landing completes");
            pet.State.ReducedMotion=true; pet.ApplySettings(); pet.RunInteraction("jump");
            Require(pet.AirborneOffset==0 && pet.ActiveMotion is null,key+": reduced motion disables airborne displacement");
            pet.State.ReducedMotion=false; pet.ApplySettings();
            Require(pet.State.Character==character.Id && pet.State.Outfit==outfit,key+": every interaction preserves character, style and outfit");
        }
        pet.RunInteraction("jump"); Thread.Sleep(1500);
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Require(pet.CurrentAction=="land" && pet.DrawnFrame==5 && pet.AirborneOffset==0,"a delayed desktop update still displays the landing pose before returning to idle");
        await Until(()=>pet.CurrentAction=="idle","delayed landing completes");
        Require(!PetActions.Menu("play",true).Any(a=>a.Key=="nudge"),"play menu has one ball interaction");
        pet.RunInteraction("chat"); await Task.Delay(80); var chat=pet.ActiveChat;
        Require(chat is not null && pet.CurrentAction=="listen" && Window.GetWindow(chat)==pet,"chat composer belongs to the pet window");
        var input=Find<TextBox>(chat!).Single(box=>AutomationProperties.GetName(box)=="聊天内容"); input.Text="你好";
        Find<Button>(chat!).Single(button=>AutomationProperties.GetName(button)=="发送聊天").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var thinking = Stopwatch.StartNew(); await Task.Delay(400);
        Require(chat!.History.Count==1 && pet.CurrentAction=="thinking","sending shows thinking before revealing the reply");
        await Until(()=>chat!.History.Count==2,"chat send button replies");
        Require(thinking.ElapsedMilliseconds>=950,"local reply waits at least one second");
        Require(input.Text=="" && pet.CurrentAction=="chat","sending from the text box clears input and animates the spoken reply");
        await chat!.SendText("今天好累"); await chat.SendText("工作有点多");
        Require(chat.History.Count==6 && chat.History[^1].Content.Contains("工作") && chat.History.Where(m=>m.Role=="assistant").Select(m=>m.Content).Distinct().Count()==3,"three real UI turns respond and carry the preceding topic in local mode");
        Capture(canvas,"local-chat");
        string sameFamily=pet.Character.FamilyId; pet.SelectStyle(CharacterStyles.Chibi);
        Require(pet.Character.FamilyId==sameFamily && chat.History.Count==6,"style changes preserve the active conversation");
        var pendingReply=chat.SendText("再说一点");
        pet.SelectCharacter(sameFamily=="gpt"?"deepseek-adult":"gpt-adult"); await pendingReply;
        Require(chat.History.Count==0,"changing companions cancels an old reply before it reaches the new conversation");
        chat.Close(); pet.StopInteraction();
        await Task.WhenAll(captures);
        File.WriteAllLines(Path.Combine(output,"choreography-check.txt"),checks.Append($"{checks.Count} choreography checks passed."));
    }
}
