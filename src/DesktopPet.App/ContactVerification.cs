using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class ContactVerification
{
    public static async Task Run(PetWindow pet, string output, bool pilot, bool availableOnly = false)
    {
        var checks = new List<string>();
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
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name + ".png")); png.Save(file);
        }
        pet.IsHitTestVisible = false; pet.State.Size = 280; pet.State.Wander = pet.State.ReducedMotion = false;
        pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now)); pet.ApplySettings();
        var appearances = (from c in pet.Catalog.Characters where c.Category != "chibi"
                           from outfit in new[] { "original", "swim", "wedding" }
                           where !pilot || c.Id == "qwen-3d" && outfit == "swim"
                           where !availableOnly || c.MotionFor(outfit, "meal")?.BakedProps == true
                           select (c, outfit)).ToArray();
        Require(availableOnly ? appearances.Length > 0 : appearances.Length == (pilot ? 1 : 48), availableOnly ? "covers installed action sheets for visual review" : "covers every requested portrait appearance");
        foreach (var (character, outfit) in appearances)
        {
            pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit; pet.ApplySettings();
            pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.Top = pet.WorkArea.Bottom - 468;
            var idle = pet.Art.Frame(character, character.Resolve(outfit, "idle", 0).Sprite, 0);
            double expectedHeight = pet.State.Size * pet.Art.VisibleHeight(idle);
            foreach (var (input, action) in new[] { ("snack", "eat"), ("checkin", "meal") })
            {
                var clip = character.MotionFor(outfit, action);
                Require(clip is { BakedProps: true, Frames.Length: >= 3, Cells.Length: 9 }, character.Id + "/" + outfit + ": drawn " + action + " contains its own food");
                pet.RunInteraction(input); await Task.Delay(100);
                Require(pet.UsingDrawnAction && pet.ActiveMotion is null, "drawn hands are never deformed by the waving-portrait rig");
                Require(ReferenceEquals(sprite.Source, pet.Art.Frame(character, clip!, clip!.Frames![0])), "first pose comes from this exact outfit's action sheet");
                Capture($"contact-{character.Id}-{outfit}-{action}-0");
                await Task.Delay(450);
                Require(pet.DrawnFrame == clip.Frames[1], "the next drawn hand pose actually advances");
                Require(Math.Abs(sprite.Height * pet.Art.VisibleHeight((BitmapSource)sprite.Source) - expectedHeight) < .5, "drawn pose preserves visible character height");
                Capture($"contact-{character.Id}-{outfit}-{action}-1");
                await Task.Delay(500); Capture($"contact-{character.Id}-{outfit}-{action}-2");
            }
            pet.PlayWithToy("basketball"); await Task.Delay(70);
            Require(pet.HasCaughtBall && pet.HandTarget is not null && pet.CurrentAction == "ball-hold", "taking a toy visibly holds it in both drawn hands");
            Capture($"contact-{character.Id}-{outfit}-holding");
            var hands = pet.HandTarget!.Value;
            pet.ThrowToy(hands.X - 17, hands.Y + 65, 0, 0); await Task.Delay(140);
            Require(!pet.HasCaughtBall, "a torso/elbow-height hit is not a successful hand catch");
            hands = pet.HandTarget!.Value;
            pet.ThrowToy(hands.X - 90, hands.Y - 17, 600, -35); await Task.Delay(190);
            Require(pet.HasCaughtBall && pet.CurrentAction == "ball-hit", "a throw into the palms is caught");
            Capture($"contact-{character.Id}-{outfit}-catching");
            var settling = System.Diagnostics.Stopwatch.StartNew();
            while (pet.CurrentAction == "ball-hit" && settling.ElapsedMilliseconds < 2200) await Task.Delay(40);
            Require(pet.CurrentAction == "ball-hold" && pet.HasCaughtBall, character.Id + "/" + outfit + ": catch settles into the cradling pose without bouncing through the body (" + pet.CurrentAction + ")");
            var center = new Point(Canvas.GetLeft(ball) + ball.Width / 2, Canvas.GetTop(ball) + ball.Height / 2);
            Require((center - pet.HandTarget!.Value).Length < .1, "held toy follows the current frame's palm anchor");
            Require(pet.State.Character == character.Id && pet.State.Outfit == outfit, "feeding and catching never change style or clothes");
        }
        pet.StopInteraction();
        File.WriteAllLines(Path.Combine(output, "contact-check.txt"), checks.Append($"{checks.Count} drawn contact checks passed."));
    }
}
