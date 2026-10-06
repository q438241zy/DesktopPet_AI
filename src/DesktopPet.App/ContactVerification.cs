using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class ContactVerification
{
    private sealed record FeedSnapshot(double Milliseconds, int Frame, string Action, string Character, string Outfit, bool Drawn, bool PortraitRig, bool ExtraFood, BitmapSource Source, double PixelScale, ClubPose? Blend);
    private sealed record ImpactSnapshot(double Milliseconds, double X, double Y, int Frame, string Action, string Character, string Outfit, bool Caught, bool InFlight, ImageSource? Source, ClubPose? Blend);
    private sealed record ReplayPose(int Frame,ClubPose? Blend);
    private sealed record FeedProof(string Action,Sprite Clip,ReplayPose[] Poses);
    private sealed record AppearanceProof(Character Character,string Outfit,FeedProof[] Feeding,ReplayPose Hit,Point Ball,ReplayPose Miss);
    private static double ReplayTime(Sprite clip,int frame,ClubPose? blend)
    {
        double elapsed=0;
        for(int i=0;i<clip.Frames!.Length;i++)
        {
            int next=i+1<clip.Frames.Length?clip.Frames[i+1]:clip.Loop?clip.Frames[0]:clip.Frames[i];
            if(blend is { } pose ? clip.Frames[i]==pose.A && next==pose.B : clip.Frames[i]==frame)
                return elapsed+(blend?.Amount??0)*clip.FrameMs![i]+.00001;
            elapsed+=clip.FrameMs![i];
        }
        throw new InvalidOperationException("Recorded pose has no matching authored interval.");
    }
    public static async Task Run(PetWindow pet, string output, bool pilot, bool availableOnly = false, string? appearance = null)
    {
        var checks = new List<string>();
        var captures = new List<Task>();
        var proofs = new List<AppearanceProof>();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        var canvas = (Canvas)pet.Content;
        var sprite = canvas.Children.OfType<Image>().Single();
        var ball = canvas.Children.OfType<ItemVisual>().Single();
        void Capture(string name)
        {
            // Use an opaque desktop color: image viewers that ignore PNG alpha otherwise
            // exaggerate invisible fringe RGB and cannot be used to judge the real UI.
            var previous = canvas.Background; canvas.Background = new SolidColorBrush(Color.FromRgb(238, 242, 247));
            pet.UpdateLayout(); var bitmap = new RenderTargetBitmap(560, 680, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
            canvas.Background = previous;
            bitmap.Freeze();
            captures.Add(Task.Run(() =>
            {
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file);
            }));
        }
        pet.IsHitTestVisible = false; pet.State.Size = 280; pet.State.Wander = pet.State.ReducedMotion = false;
        pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now)); pet.ApplySettings();
        var appearances = (from c in pet.Catalog.Characters where c.Category != "chibi"
                           from outfit in Catalog.BuiltInOutfits
                           where !pilot || c.Id == "qwen-adult" && outfit == "swim"
                           where appearance is null || c.Id + "-" + outfit == appearance
                           where !availableOnly || c.MotionFor(outfit, "meal")?.BakedProps == true
                           select (c, outfit)).ToArray();
        Require(availableOnly ? appearances.Length > 0 : appearances.Length == (pilot || appearance is not null ? 1 : pet.Catalog.Characters.Count(c=>c.Category==CharacterStyles.Realistic)*Catalog.BuiltInOutfits.Length), availableOnly ? "covers installed action sheets for visual review" : "covers every requested portrait appearance");
        foreach (var (character, outfit) in appearances)
        {
            pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit; pet.ApplySettings();
            pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.Top = pet.WorkArea.Bottom - 468;
            var feedingProofs=new List<FeedProof>();
            foreach (var (input, action) in new[] { ("snack", "eat"), ("checkin", "meal") })
            {
                var clip = character.MotionFor(outfit, action) ?? throw new InvalidOperationException($"Missing feeding motion: {character.Id}/{outfit}/{action}");
                Require(clip is { BakedProps: true, Frames.Length: >= 3, Cells: not null }, character.Id + "/" + outfit + ": drawn " + action + " contains its own food");
                var feedFrames=clip.Frames!;
                Require(feedFrames.Take(3).Distinct().Count()==3,character.Id+"/"+outfit+": feeding has three distinct hand-and-food poses");
                var poses=new List<FeedSnapshot>();var framesSeen=new List<object>();
                var feedingClock=System.Diagnostics.Stopwatch.StartNew();
                var threePoses=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                TimeSpan lastPresentation=TimeSpan.MinValue;
                void ObserveFeeding(object? sender,EventArgs e)
                {
                    if(e is not RenderingEventArgs frame || frame.RenderingTime==lastPresentation)return;
                    lastPresentation=frame.RenderingTime;
                    framesSeen.Add(new { ms=feedingClock.Elapsed.TotalMilliseconds, action=pet.CurrentAction, frame=pet.DrawnFrame, character=pet.State.Character, outfit=pet.State.Outfit });
                    if(poses.Count>=3 || pet.CurrentAction!=action || pet.DrawnFrame!=feedFrames[poses.Count])return;
                    var bitmap=(BitmapSource)sprite.Source;
                    poses.Add(new FeedSnapshot(feedingClock.Elapsed.TotalMilliseconds,pet.DrawnFrame,pet.CurrentAction,pet.State.Character,pet.State.Outfit,pet.UsingDrawnAction,pet.ActiveMotion is not null,pet.DrawsExtraFood,bitmap,sprite.Height/Math.Max(bitmap.PixelWidth,bitmap.PixelHeight),pet.ActiveAuthoredVisual?.Current));
                    if(poses.Count==3)threePoses.TrySetResult();
                }
                CompositionTarget.Rendering+=ObserveFeeding;
                try
                {
                    pet.RunInteraction(input);
                    // Observe the actual live poses without rendering or encoding
                    // proof images on the action's clock.
                    if(await Task.WhenAny(threePoses.Task,Task.Delay(3000))!=threePoses.Task)
                        throw new TimeoutException($"Three feeding poses were not presented in order: {character.Id}/{outfit}/{action}; observed={string.Join(',',poses.Select(p=>p.Frame))}; current={pet.CurrentAction}/{pet.DrawnFrame}");
                    await threePoses.Task;
                }
                finally
                {
                    CompositionTarget.Rendering-=ObserveFeeding;
                    File.WriteAllText(Path.Combine(output,$"contact-{character.Id}-{outfit}-{action}-presentations.json"),System.Text.Json.JsonSerializer.Serialize(framesSeen,Json.Options));
                }
                for(int i=0;i<3;i++)
                {
                    var shown=poses[i];
                    Require(shown.Action==action && shown.Character==character.Id && shown.Outfit==outfit && shown.Frame==feedFrames[i]
                        && shown.Source.IsFrozen && ReferenceEquals(shown.Source,pet.Art.Frame(character,clip,shown.Frame)),character.Id+"/"+outfit+$": live {action} pose {i} uses this exact outfit's immutable drawing");
                    Require(shown.Drawn && !shown.PortraitRig && !shown.ExtraFood && clip.BakedProps,"presented hands and food use one illustrated source without a substitute portrait rig");
                    Require(Math.Abs(shown.PixelScale/poses[0].PixelScale-1)<.003,"presented hands and food retain the same body pixel scale");
                }
                File.WriteAllText(Path.Combine(output,$"contact-{character.Id}-{outfit}-{action}-observed.json"),System.Text.Json.JsonSerializer.Serialize(poses.Select(shown=>new { observedMilliseconds=shown.Milliseconds,shown.Frame,shown.Action,shown.Character,shown.Outfit,shown.Drawn,shown.PortraitRig,shown.ExtraFood,shown.PixelScale,sourceWidth=shown.Source.PixelWidth,sourceHeight=shown.Source.PixelHeight,replayMilliseconds=ReplayTime(clip,shown.Frame,shown.Blend),shown.Blend }),Json.Options));
                feedingProofs.Add(new FeedProof(action,clip,poses.Select(p=>new ReplayPose(p.Frame,p.Blend)).ToArray()));
            }
            pet.PlayWithToy("basketball"); await Task.Delay(70);
            Require(!pet.HasCaughtBall && pet.CurrentAction == "ball-ready" && ball.Visibility == Visibility.Visible
                && Math.Abs(Canvas.GetTop(ball) + ball.Height - 468) < .1, "selected toy waits on the floor for a throw");
            // Observe a normal presentation before the timed throw. Proof
            // images are replayed only after all live checks for this outfit.
            var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void PresentedReady(object? sender, EventArgs e) { if(e is RenderingEventArgs)prepared.TrySetResult(); }
            CompositionTarget.Rendering += PresentedReady;
            try
            {
                if(await Task.WhenAny(prepared.Task,Task.Delay(3000))!=prepared.Task)
                    throw new TimeoutException($"No ready presentation before throw: {character.Id}/{outfit}");
                await prepared.Task;
            }
            finally { CompositionTarget.Rendering -= PresentedReady; }
            double height = sprite.Height * pet.Art.VisibleHeight((BitmapSource)sprite.Source);
            var trajectory = new List<object>();
            var flight = System.Diagnostics.Stopwatch.StartNew();
            var impactFrame = new TaskCompletionSource<ImpactSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            var rebound = new TaskCompletionSource<(double ImpactX, double NextX, double ImpactMs, double NextMs)>(TaskCreationOptions.RunContinuationsAsynchronously);
            ImpactSnapshot? hitProof=null;
            double? observedImpactX = null;
            double observedImpactMs = 0;
            void ObserveFlight(object? sender, EventArgs e)
            {
                if(e is not RenderingEventArgs) return;
                double now=flight.Elapsed.TotalMilliseconds, x=Canvas.GetLeft(ball);
                trajectory.Add(new { ms=now, x, y=Canvas.GetTop(ball), hit=pet.BallWasHit, flight=pet.ToyInFlight, caught=pet.HasCaughtBall, action=pet.CurrentAction, frame=pet.DrawnFrame, character=pet.State.Character, outfit=pet.State.Outfit });
                if(!pet.BallWasHit) return;
                if(observedImpactX is not { } impact)
                {
                    observedImpactX=x;observedImpactMs=now;
                    impactFrame.TrySetResult(new ImpactSnapshot(now,x,Canvas.GetTop(ball),pet.DrawnFrame,pet.CurrentAction,pet.State.Character,pet.State.Outfit,pet.HasCaughtBall,pet.ToyInFlight,sprite.Source,pet.ActiveAuthoredVisual?.Current));
                }
                else if(Math.Abs(x-impact)>.000001) rebound.TrySetResult((impact,x,observedImpactMs,now));
            }
            CompositionTarget.Rendering += ObserveFlight;
            try
            {
                pet.ThrowToy(80, 468 - height * .55 - ball.Height / 2, 1000, -80);
                // Physics advances only on presented updates, not Task.Delay
                // samples. Require an actual hit presentation before the throw's
                // four-second expiry; never accept a later miss or stale hit flag.
                if(await Task.WhenAny(impactFrame.Task,Task.Delay(3000))!=impactFrame.Task)
                    throw new TimeoutException($"No body impact was presented within the throw: {character.Id}/{outfit}; elapsed={flight.Elapsed.TotalMilliseconds:0.0}; x={Canvas.GetLeft(ball)}; presentations={trajectory.Count}; action={pet.CurrentAction}");
                var impact=await impactFrame.Task;
                hitProof=impact;
                var hitClip=character.MotionFor(outfit,"ball-hit")!;
                Require(impact.Milliseconds<3000 && !impact.Caught && impact.InFlight && impact.Action=="ball-hit"
                    && impact.Character==character.Id && impact.Outfit==outfit && hitClip.Frames!.Contains(impact.Frame)
                    && ReferenceEquals(impact.Source,pet.Art.Frame(character,hitClip,impact.Frame)), character.Id + "/" + outfit + $": presented body impact at {impact.Milliseconds:0.0} ms flinches in this outfit without attaching the ball to an elbow");
                // A fixed delay may contain no presented physics update, or may
                // skip the first bounce. Judge the first actual movement after
                // contact so a later wall rebound cannot hide the wrong direction.
                if(await Task.WhenAny(rebound.Task,Task.Delay(900))!=rebound.Task)
                    throw new TimeoutException($"No post-impact movement was presented: {character.Id}/{outfit}; x={Canvas.GetLeft(ball)}; action={pet.CurrentAction}; flight={pet.ToyInFlight}");
                var firstStep=await rebound.Task;
                Require(firstStep.NextX < firstStep.ImpactX, $"{character.Id}/{outfit}: first post-impact movement reverses away ({firstStep.ImpactX:0.###}->{firstStep.NextX:0.###}, {firstStep.ImpactMs:0.0}->{firstStep.NextMs:0.0} ms)");
            }
            finally
            {
                CompositionTarget.Rendering -= ObserveFlight;
                File.WriteAllText(Path.Combine(output,$"contact-{character.Id}-{outfit}-trajectory.json"),System.Text.Json.JsonSerializer.Serialize(trajectory,Json.Options));
            }
            pet.ThrowToy(35, 70, 0, 0);
            flight.Restart();
            while (pet.ToyInFlight && flight.ElapsedMilliseconds < 6000) await Task.Delay(35);
            Require(!pet.BallWasHit && !pet.HasCaughtBall && pet.CurrentAction == "ball-miss"
                && character.MotionFor(outfit,"ball-miss")!.Frames!.Contains(pet.DrawnFrame), character.Id + "/" + outfit + ": a distant throw produces the separate miss reaction (" + pet.CurrentAction + "/" + pet.DrawnFrame + ", flight=" + pet.ToyInFlight + ", hit=" + pet.BallWasHit + ")");
            int missFrame=pet.DrawnFrame;var missBlend=pet.ActiveAuthoredVisual?.Current;
            Require(pet.State.Character == character.Id && pet.State.Outfit == outfit, "feeding and ball reactions never change style or clothes");
            var recordedHit=hitProof!;
            proofs.Add(new AppearanceProof(character,outfit,feedingProofs.ToArray(),new ReplayPose(recordedHit.Frame,recordedHit.Blend),new Point(recordedHit.X,recordedHit.Y),new ReplayPose(missFrame,missBlend)));
            pet.StopInteraction();
        }
        pet.ThrowToy(35, 70, 0, 0);
        // Simulate a busy dispatcher: physics deliberately limits long time steps,
        // but an expired throw must still finish on the next rendered update.
        Thread.Sleep(4200);
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Require(!pet.ToyInFlight && pet.CurrentAction == "ball-miss", "a delayed UI frame cannot prolong a missed throw beyond its real-time deadline");
        pet.StopInteraction();
        File.WriteAllLines(Path.Combine(output,"contact-live-check.txt"),checks.Append($"{checks.Count} live contact checks passed before any proof rendering."));
        File.WriteAllText(Path.Combine(output,"contact-replay-index.json"),System.Text.Json.JsonSerializer.Serialize(proofs.Select(proof=>new { character=proof.Character.Id,proof.Outfit,feeding=proof.Feeding.Select(feed=>new { feed.Action,poses=feed.Poses.Select(pose=>new { pose.Frame,pose.Blend,milliseconds=ReplayTime(feed.Clip,pose.Frame,pose.Blend) }) }),proof.Hit,proof.Ball,proof.Miss }),Json.Options));
        // All live appearances and the delayed-frame deadline must finish before
        // any RenderTargetBitmap work. Keep only small replay metadata here, not
        // the source bitmaps that were checked during the live observations.
        pet.BeginPreview();
        try
        {
            foreach(var appearanceProof in proofs)
            {
                var character=appearanceProof.Character;string outfit=appearanceProof.Outfit;
                pet.SelectCharacter(character.Id);pet.State.Outfits[character.Id]=outfit;pet.ApplySettings();
                pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
                foreach(var proof in appearanceProof.Feeding)
                    for(int i=0;i<proof.Poses.Length;i++)
                    {
                        var shown=proof.Poses[i];
                        pet.PreviewMotion(proof.Action,ReplayTime(proof.Clip,shown.Frame,shown.Blend),(int)PortraitMotion.Duration(proof.Action));
                        Require(pet.DrawnFrame==shown.Frame,"proof replay matches the independently observed feeding pose");
                        Capture($"contact-{character.Id}-{outfit}-{proof.Action}-{i}-replay");
                    }
                pet.PreviewMotion("ball-ready",0,2200);Capture($"contact-{character.Id}-{outfit}-ready-replay");
                var impact=appearanceProof.Hit;
                pet.PreviewMotion("ball-hit",ReplayTime(character.MotionFor(outfit,"ball-hit")!,impact.Frame,impact.Blend),1300);
                ball.Visibility=Visibility.Visible;Canvas.SetLeft(ball,appearanceProof.Ball.X);Canvas.SetTop(ball,appearanceProof.Ball.Y);
                Capture($"contact-{character.Id}-{outfit}-hit-replay");
                pet.PreviewMotion("ball-miss",ReplayTime(character.MotionFor(outfit,"ball-miss")!,appearanceProof.Miss.Frame,appearanceProof.Miss.Blend),1600);
                Capture($"contact-{character.Id}-{outfit}-miss-replay");
                await Task.WhenAll(captures);
            }
        }
        finally { pet.EndPreview(); }
        await Task.WhenAll(captures);
        File.WriteAllLines(Path.Combine(output, "contact-check.txt"), checks.Append($"{checks.Count} drawn contact checks passed."));
    }
}
