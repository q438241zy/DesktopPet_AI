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
    private sealed record PoseProof(string CharacterId,string Outfit,string Name,string Motion,double Elapsed,
        int Duration,int Frame,double Airborne,string SourceFile);

    public static async Task Run(PetWindow pet, string output, bool pilot = false, bool availableOnly = false, string? appearance = null)
    {
        var checks = new List<string>();
        var captures = new List<Task>();
        var proofs = new List<PoseProof>();
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
                // This is called only after the measured sequence has stopped.
                // PNG encoding stays off the dispatcher as well.
                captures.Add(Task.Run(() =>
                {
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
                    using var stream = File.Create(Path.Combine(output, file + ".png")); png.Save(stream);
                }));
            }
            finally { canvas.Background = previous; }
        }
        // Read the already retained drawing; rendering another bitmap here would
        // stall the same dispatcher whose timing these checks are measuring.
        bool QuietFeedback() => VisualTreeHelper.GetDrawing(effects) is not { } drawing || drawing.Bounds.IsEmpty;
        double BodyHeight() => sprite.Height * pet.Art.VisibleHeight((BitmapSource)sprite.Source);
        pet.IsHitTestVisible = false; pet.State.Size = 280; pet.State.Wander = pet.State.ReducedMotion = false;
        pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now)); pet.ApplySettings();
        var appearances = (from c in pet.Catalog.Characters where c.Category != "chibi"
                           from outfit in Catalog.BuiltInOutfits
                           where !pilot || c.Id == "deepseek-adult" && outfit == "original"
                           where appearance is null || c.Id + "-" + outfit == appearance
                           where !availableOnly || c.MotionFor(outfit,"think") is { HeightRatios:not null } or { ReferenceHeightPixels:>0 }
                           select (c,outfit)).ToArray();
        Require(appearances.Length == (pilot || appearance is not null ? 1 : pet.Catalog.Characters.Count(c=>c.Category==CharacterStyles.Realistic)*Catalog.BuiltInOutfits.Length) || availableOnly && appearances.Length > 0, "requested appearances are installed");
        foreach (var (character,outfit) in appearances)
        {
            string key = character.Id + "-" + outfit;
            pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit; pet.ApplySettings();
            pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.Top = pet.WorkArea.Bottom - 468;
            int First(string motion)=>character.MotionFor(outfit,motion)!.Frames?.First()??0;
            int Last(string motion)=>character.MotionFor(outfit,motion)!.Frames?.Last()??0;
            int Pose(string motion,int index)=>character.MotionFor(outfit,motion)!.Frames![index];
            double ProofTime(string motion)
            {
                var clip=character.MotionFor(outfit,motion)!;
                var frames=clip.Frames??Enumerable.Range(0,clip.Columns*clip.Rows).ToArray();
                var times=clip.FrameMs??Enumerable.Repeat(240,frames.Length).ToArray();
                double elapsed=0;
                if(pet.ActiveAuthoredVisual is { } visual && visual.SheetFile==clip.File)
                {
                    var pose=visual.Current;
                    for(int i=0;i<frames.Length;i++)
                    {
                        int next=i+1<frames.Length?i+1:clip.Loop?0:i;
                        if(frames[i]==pose.A && frames[next]==pose.B) return elapsed+times[i]*pose.Amount;
                        elapsed+=times[i];
                    }
                }
                elapsed=0;
                for(int i=0;i<frames.Length;i++)
                {
                    if(frames[i]==pet.DrawnFrame)
                    {
                        if(motion=="jump" && pet.AirborneOffset>0)
                            return 240+Math.Asin(Math.Clamp(pet.AirborneOffset/(pet.State.Size*.30),0,1))*640/Math.PI;
                        return elapsed+times[i]/2d;
                    }
                    elapsed+=times[i];
                }
                throw new InvalidOperationException(key+": presented pose is outside its selected outfit's "+motion+" clip");
            }
            void Remember(string name,string motion,int duration=10000)
            {
                var clip=character.MotionFor(outfit,motion)!;
                Require(ReferenceEquals(sprite.Source,pet.Art.Frame(character,clip,pet.DrawnFrame)),
                    key+"/"+name+": presented source belongs to this outfit's "+motion+" sheet");
                proofs.Add(new(character.Id,outfit,name,motion,ProofTime(motion),duration,pet.DrawnFrame,pet.AirborneOffset,clip.File));
            }
            double standing = BodyHeight();
            pet.RunInteraction("think"); await Task.Delay(100);
            Require(pet.UsingDrawnAction && pet.ActiveMotion is null && pet.DrawnFrame == First("think") && QuietFeedback(), key+": silent daydream pose without speaking effects"); Remember("daydream","think");
            pet.Play("listen",duration:0); await Task.Delay(60);
            Require(pet.DrawnFrame == First("listen") && pet.UsingDrawnAction, key+": closed-mouth listening pose");
            pet.Play("chat",duration:2000); await Until(()=>pet.DrawnFrame==Pose("chat",1),"speaking pose");
            Require(QuietFeedback() && pet.ActiveMotion is null, key+": speaking uses a distinct drawn pose"); Remember("chat","chat",2000);
            // Subscribe before starting the real-time jump. In authored sports
            // motion the dominant knee texture changes halfway through a blend;
            // timer polling after a synchronous screenshot can miss that window.
            var jumpFrames = new List<object>();
            var jumpClock = Stopwatch.StartNew();
            var jumpFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool sawAirborneKnees = false, sawLanding = false, observingJump = true;
            TimeSpan lastJumpPresentation = TimeSpan.MinValue;
            void ObserveJump(object? sender, EventArgs e)
            {
                if(jumpFinished.Task.IsCompleted || e is not RenderingEventArgs rendering || rendering.RenderingTime == lastJumpPresentation) return;
                lastJumpPresentation = rendering.RenderingTime;
                // The pet may subscribe its RenderWalk handler after ours when
                // this action switches from the timer to the presentation clock.
                // Sample after all handlers for this presented frame have run.
                pet.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,new Action(() =>
                {
                if(!observingJump || jumpFinished.Task.IsCompleted) return;
                double baseline = Canvas.GetTop(sprite)+sprite.Height*pet.Art.GroundLine((BitmapSource)sprite.Source);
                jumpFrames.Add(new { ms=jumpClock.Elapsed.TotalMilliseconds, presentedMs=rendering.RenderingTime.TotalMilliseconds,
                    action=pet.CurrentAction, frame=pet.DrawnFrame, airborne=pet.AirborneOffset, baseline, drawn=pet.UsingDrawnAction });
                try
                {
                    if(!sawAirborneKnees && pet.CurrentAction=="jump" && pet.DrawnFrame==Pose("jump",1) && pet.AirborneOffset>35)
                    {
                        Require(pet.UsingDrawnAction && Math.Abs(baseline-(468-pet.AirborneOffset))<.1,
                            key+": jump changes pose and visibly leaves the ground");
                        sawAirborneKnees = true;
                        Remember("jump-air","jump",880);
                    }
                    if(!sawLanding && pet.CurrentAction=="land" && pet.DrawnFrame==First("land"))
                    {
                        Require(sawAirborneKnees,key+": airborne knee pose is presented before landing");
                        Require(pet.UsingDrawnAction && pet.AirborneOffset==0 && Math.Abs(baseline-468)<.1,
                            key+": bent-knee landing reaches the ground in its own timed phase");
                        sawLanding = true;
                        Remember("jump-land","land",410);
                    }
                    if(pet.CurrentAction=="idle")
                    {
                        Require(sawAirborneKnees && sawLanding,key+": presented jump finishes after its airborne and landing poses");
                        jumpFinished.TrySetResult(true);
                    }
                }
                catch(Exception error) { jumpFinished.TrySetException(error); }
                }));
            }
            CompositionTarget.Rendering += ObserveJump;
            try
            {
                pet.RunInteraction("jump");
                Remember("jump-ready","jump",880);
                if(await Task.WhenAny(jumpFinished.Task,Task.Delay(2500))!=jumpFinished.Task)
                    throw new TimeoutException($"{key}: jump presentation did not finish; action={pet.CurrentAction}, frame={pet.DrawnFrame}, airborne={pet.AirborneOffset:0.###}, kneeSeen={sawAirborneKnees}, landingSeen={sawLanding}, samples={jumpFrames.Count}");
                await jumpFinished.Task;
            }
            finally
            {
                observingJump = false;
                CompositionTarget.Rendering -= ObserveJump;
                File.WriteAllText(Path.Combine(output,"pose-"+key+"-jump-presentation.json"),
                    System.Text.Json.JsonSerializer.Serialize(jumpFrames,Json.Options));
            }
            pet.RunInteraction("curl"); await Until(()=>pet.DrawnFrame==Last("curl"),"seated curl");
            Require(BodyHeight()<standing*.64 && pet.ActiveMotion is null,key+": curl is compact sitting, never a standing-height squat"); Remember("curl","curl");
            pet.RunInteraction("rest"); await Until(()=>pet.CurrentAction=="sleep","sleep transition");
            // Side-lying artwork must fit below half the standing silhouette.
            // GLM's approved sports drawing includes raised cat ears and a tail
            // (46.3%); shrinking its anatomy to satisfy the old 44% cut-off is wrong.
            Require(pet.IsResting && pet.DrawnFrame==First("sleep") && BodyHeight()<standing*.5 && QuietFeedback(),key+$": sleep lies on its side without floating rings or speech symbols (resting={pet.IsResting}, frame={pet.DrawnFrame}/{First("sleep")}, heightRatio={BodyHeight()/standing:0.####}, quiet={QuietFeedback()})");
            double before = sprite.Height; await Task.Delay(430); Require(Math.Abs(sprite.Height-before)>.01,key+": drawn sleeping body breathes subtly"); Remember("rest","sleep");
            pet.Touch(.2); Require(!pet.IsResting,key+": sleeping character can be awakened");
            pet.RunInteraction("blocks"); Remember("blocks-pick","build",4400);
            await Until(()=>pet.DrawnFrame==Pose("build",1),"placing a block");
            Require(pet.UsingDrawnAction && character.MotionFor(outfit,"build")!.BakedProps && QuietFeedback(),key+": cubes are part of the hand-placement artwork, not an independent floating tower"); Remember("blocks-place","build",4400);
            await Until(()=>pet.DrawnFrame==Last("build"),"completed block tower"); Remember("blocks-done","build",4400);
            pet.StopInteraction(); pet.Top = pet.WorkArea.Bottom-468; pet.BeginLift(); pet.Top-=110; await Until(()=>pet.DrawnFrame==Last("pickup"),"hanging pickup pose");
            Require(pet.LiftedFromTaskbar && pet.UsingDrawnAction && pet.ActiveMotion is null,key+": lifting from the taskbar shows dangling legs"); Remember("lift","pickup");
            double placedTop = pet.Top; pet.ReleaseLift();
            Require(!pet.IsDropping && Math.Abs(pet.Top-placedTop)<.1,key+": manual release stays at the user's chosen position");
            pet.DropToFloor(); Require(pet.IsDropping,key+": natural return to floor is separate from manual placement"); await Until(()=>!pet.IsDropping,"fall settles");
            Require(Math.Abs(pet.Top+468-pet.WorkArea.Bottom)<.1 && pet.CurrentAction=="land",key+": release lands on the same work-area bottom"); Remember("drop-land","land",410);
            await Until(()=>pet.CurrentAction=="idle","landing completes");
            pet.State.ReducedMotion=true; pet.ApplySettings(); pet.RunInteraction("jump");
            Require(pet.AirborneOffset==0 && pet.ActiveMotion is null,key+": reduced motion disables airborne displacement");
            pet.State.ReducedMotion=false; pet.ApplySettings();
            Require(pet.State.Character==character.Id && pet.State.Outfit==outfit,key+": every interaction preserves character, style and outfit");
            pet.StopInteraction();
        }
        pet.RunInteraction("jump"); Thread.Sleep(1500);
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Require(pet.CurrentAction=="land" && pet.DrawnFrame==(pet.Character.MotionFor(pet.State.Outfit,"land")!.Frames?.First()??0) && pet.AirborneOffset==0,"a delayed desktop update still displays the landing pose before returning to idle");
        await Until(()=>pet.CurrentAction=="idle","delayed landing completes");
        Require(!PetActions.Menu("play").Any(a=>a.Key=="nudge"),"play menu has one ball interaction");
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
        File.WriteAllText(Path.Combine(output,"local-chat-history.json"),
            System.Text.Json.JsonSerializer.Serialize(chat.History,Json.Options));
        string sameFamily=pet.Character.FamilyId; pet.SelectStyle(CharacterStyles.Chibi);
        Require(pet.Character.FamilyId==sameFamily && chat.History.Count==6,"style changes preserve the active conversation");
        var pendingReply=chat.SendText("再说一点");
        pet.SelectCharacter(sameFamily=="gpt"?"deepseek-adult":"gpt-adult"); await pendingReply;
        Require(chat.History.Count==0,"changing companions cancels an old reply before it reaches the new conversation");
        chat.Close(); pet.StopInteraction();

        // No bitmap rendering or PNG encoding starts until the complete live
        // matrix, delayed-frame test and conversation checks have all finished.
        // Otherwise a prior appearance's screenshot work can starve the next
        // appearance's short rendered poses even outside a Rendering callback.
        foreach(var group in proofs.GroupBy(p=>(p.CharacterId,p.Outfit)))
        {
            var (characterId,outfit)=group.Key;
            string key=characterId+"-"+outfit;
            pet.SelectCharacter(characterId); pet.State.Outfits[characterId]=outfit; pet.ApplySettings();
            pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280; pet.Top=pet.WorkArea.Bottom-468;
            pet.BeginPreview();
            try
            {
                foreach(var proof in group)
                {
                    pet.PreviewMotion(proof.Motion,proof.Elapsed,proof.Duration);
                    var clip=pet.Character.MotionFor(outfit,proof.Motion)!;
                    Require(pet.DrawnFrame==proof.Frame && clip.File==proof.SourceFile
                        && ReferenceEquals(sprite.Source,pet.Art.Frame(pet.Character,clip,proof.Frame)),
                        key+"/"+proof.Name+": replay retains the observed outfit, source and frame");
                    Capture(canvas,"pose-"+key+"-"+proof.Name+"-replay");
                }
            }
            finally { pet.EndPreview(); }
            File.WriteAllText(Path.Combine(output,"pose-"+key+"-replays.json"),
                System.Text.Json.JsonSerializer.Serialize(group.Select(p=>new { p.CharacterId,p.Outfit,p.Name,p.Motion,p.Elapsed,p.Duration,p.Frame,p.SourceFile,
                    observedAirborne=p.Airborne,kind="static replay after the complete live matrix; see jump-presentation.json for actual timing" }),Json.Options));
            await Task.WhenAll(captures);
        }
        File.WriteAllLines(Path.Combine(output,"choreography-check.txt"),checks.Append($"{checks.Count} choreography checks passed."));
    }
}
