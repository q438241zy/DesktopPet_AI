using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class ContactVerification
{
    public static async Task Run(PetWindow pet, string output, bool pilot, bool availableOnly = false, string? appearance = null)
    {
        var checks = new List<string>();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        async Task Until(Func<bool> predicate, string message, int timeout = 3000)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (!predicate())
            {
                if (elapsed.ElapsedMilliseconds > timeout) throw new InvalidOperationException(message + ": " + pet.CurrentAction + "/" + pet.DrawnFrame);
                await Task.Delay(20);
            }
        }
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
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file);
        }
        pet.IsHitTestVisible = false; pet.State.Size = 280; pet.State.Wander = pet.State.ReducedMotion = false;
        pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now)); pet.ApplySettings();
        var appearances = (from c in pet.Catalog.Characters where c.Category != "chibi"
                           from outfit in new[] { "original", "swim", "wedding" }
                           where !pilot || c.Id == "qwen-adult" && outfit == "swim"
                           where appearance is null || c.Id + "-" + outfit == appearance
                           where !availableOnly || c.MotionFor(outfit, "meal")?.BakedProps == true
                           select (c, outfit)).ToArray();
        Require(availableOnly ? appearances.Length > 0 : appearances.Length == (pilot || appearance is not null ? 1 : 24), availableOnly ? "covers installed action sheets for visual review" : "covers every requested portrait appearance");
        foreach (var (character, outfit) in appearances)
        {
            pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit; pet.ApplySettings();
            pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.Top = pet.WorkArea.Bottom - 468;
            foreach (var (input, action) in new[] { ("snack", "eat"), ("checkin", "meal") })
            {
                var clip = character.MotionFor(outfit, action);
                Require(clip is { BakedProps: true, Frames.Length: >= 3, Cells.Length: 9 }, character.Id + "/" + outfit + ": drawn " + action + " contains its own food");
                pet.RunInteraction(input);
                Require(pet.UsingDrawnAction && pet.ActiveMotion is null, "drawn hands are never deformed by the waving-portrait rig");
                Require(ReferenceEquals(sprite.Source, pet.Art.Frame(character, clip!, clip!.Frames![0])), "first pose comes from this exact outfit's action sheet");
                double feedingPixelScale = sprite.Height / Math.Max(((BitmapSource)sprite.Source).PixelWidth,((BitmapSource)sprite.Source).PixelHeight);
                Capture($"contact-{character.Id}-{outfit}-{action}-0");
                await Until(() => pet.DrawnFrame == clip.Frames[1], character.Id + "/" + outfit + ": next feeding pose");
                Require(pet.DrawnFrame == clip.Frames[1], "the next drawn hand pose actually advances");
                var feedingFrame=(BitmapSource)sprite.Source;
                Require(Math.Abs(sprite.Height / Math.Max(feedingFrame.PixelWidth,feedingFrame.PixelHeight) / feedingPixelScale - 1) < .003,
                    "feeding retains the same body pixel scale as hands and food move");
                Capture($"contact-{character.Id}-{outfit}-{action}-1");
                await Until(() => pet.DrawnFrame == clip.Frames[2], character.Id + "/" + outfit + ": third feeding pose");
                Capture($"contact-{character.Id}-{outfit}-{action}-2");
            }
            pet.PlayWithToy("basketball"); await Task.Delay(70);
            Require(!pet.HasCaughtBall && pet.CurrentAction == "ball-ready" && ball.Visibility == Visibility.Visible
                && Math.Abs(Canvas.GetTop(ball) + ball.Height - 468) < .1, "selected toy waits on the floor for a throw");
            Capture($"contact-{character.Id}-{outfit}-ready");
            double height = sprite.Height * pet.Art.VisibleHeight((BitmapSource)sprite.Source);
            pet.ThrowToy(80, 468 - height * .55 - ball.Height / 2, 1000, -80);
            var flight = System.Diagnostics.Stopwatch.StartNew();
            while (!pet.BallWasHit && flight.ElapsedMilliseconds < 900) await Task.Delay(20);
            Require(pet.BallWasHit && !pet.HasCaughtBall && pet.ToyInFlight && pet.CurrentAction == "ball-hit"
                && pet.DrawnFrame == 14, character.Id + "/" + outfit + ": body impact flinches without attaching the ball to an elbow");
            double impactX = Canvas.GetLeft(ball); await Task.Delay(100);
            Require(Canvas.GetLeft(ball) < impactX, "impact reverses the ball away from the character");
            Capture($"contact-{character.Id}-{outfit}-hit");
            pet.ThrowToy(35, 70, 0, 0);
            flight.Restart();
            while (pet.ToyInFlight && flight.ElapsedMilliseconds < 6000) await Task.Delay(35);
            Require(!pet.BallWasHit && !pet.HasCaughtBall && pet.CurrentAction == "ball-miss"
                && pet.DrawnFrame == 15, character.Id + "/" + outfit + ": a distant throw produces the separate miss reaction (" + pet.CurrentAction + "/" + pet.DrawnFrame + ", flight=" + pet.ToyInFlight + ", hit=" + pet.BallWasHit + ")");
            Capture($"contact-{character.Id}-{outfit}-miss");
            Require(pet.State.Character == character.Id && pet.State.Outfit == outfit, "feeding and ball reactions never change style or clothes");
        }
        pet.ThrowToy(35, 70, 0, 0);
        // Simulate a busy dispatcher: physics deliberately limits long time steps,
        // but an expired throw must still finish on the next rendered update.
        Thread.Sleep(4200);
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Require(!pet.ToyInFlight && pet.CurrentAction == "ball-miss", "a delayed UI frame cannot prolong a missed throw beyond its real-time deadline");
        pet.StopInteraction();
        File.WriteAllLines(Path.Combine(output, "contact-check.txt"), checks.Append($"{checks.Count} drawn contact checks passed."));
    }
}
