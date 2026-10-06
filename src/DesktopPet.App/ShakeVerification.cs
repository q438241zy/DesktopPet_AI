using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class ShakeVerification
{
    internal static async Task Run(PetWindow pet, string output)
    {
        var checks = new List<string>();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        var canvas = (Canvas)pet.Content;
        var sprite = canvas.Children.OfType<Image>().Single();
        var effects = canvas.Children.OfType<InteractionFeedback>().Single();
        var proofs = new List<(string Label, BitmapSource Image)>();
        pet.State.Size = 240; pet.State.Opacity = 1; pet.State.Wander = false;
        pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now));
        BitmapSource Capture(Visual visual)
        {
            canvas.UpdateLayout();
            var bmp = new RenderTargetBitmap(560, 680, 96, 96, PixelFormats.Pbgra32); bmp.Render(visual); bmp.Freeze(); return bmp;
        }
        byte[] Pixels(BitmapSource bmp) { var bytes = new byte[560 * 680 * 4]; bmp.CopyPixels(bytes, 560 * 4, 0); return bytes; }
        string Hash(BitmapSource bmp) => Convert.ToHexString(SHA256.HashData(Pixels(bmp)));
        foreach (var c in pet.Catalog.Characters)
        foreach (string outfit in Catalog.BuiltInOutfits)
        {
            string label = c.Id + "/" + outfit;
            pet.BeginPreview(); pet.State.ReducedMotion = false;
            pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id] = outfit; pet.ApplySettings(); pet.StopInteraction();
            double x = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280, y = pet.WorkArea.Bottom - 468 - 110;
            pet.Left = x; pet.Top = y;
            pet.BeginLift();
            for (int i = 1; i <= 8; i++) { pet.AdvancePreview(i * 80); pet.MoveLift(x + i * 12, y - i * 5); }
            Require(pet.CurrentAction == "pickup" && !pet.HasDizzyStars, label + ": ordinary dragging stays a stable lift");
            pet.ReleaseLift(); Require(pet.CurrentAction == "idle" && !pet.IsDropping, label + ": ordinary release remains at the chosen height");
            pet.AdvancePreview(1000); pet.Left = x; pet.Top = y; pet.BeginLift();
            for (int i = 1; i <= 10; i++) { pet.AdvancePreview(1000 + i * 40); pet.MoveLift(x + (i % 2 == 0 ? 7 : -7), y); }
            Require(!pet.HasDizzyStars, label + ": small pointer jitter never causes dizziness");
            pet.ReleaseLift();
            pet.AdvancePreview(2000); pet.Left = x; pet.Top = y; pet.BeginLift();
            for (int i = 1; i <= 5; i++) { pet.AdvancePreview(2000 + i * 100); pet.MoveLift(x + (i % 2 == 0 ? 0 : 100), y); }
            Require(pet.CurrentAction == "pickup" && pet.UsingDrawnAction && pet.HasDizzyStars && sprite.RenderTransform is ScaleTransform,
                label + ": deliberate repeated shaking adds stars to the current lifted pose");
            var heldSource = (BitmapSource)sprite.Source;
            var expectedLift = c.Resolve(outfit, "pickup", 500).Sprite;
            Require(ReferenceEquals(heldSource, pet.Art.Frame(c, expectedLift, pet.DrawnFrame)), label + ": lifted clothing and character do not change");
            var haloA = Capture(effects);
            Require(Pixels(haloA).Where((_, i) => i % 4 == 3).Count(alpha => alpha > 40) > 90, label + ": stars and orbit produce visible rendered pixels");
            pet.AdvancePreview(2700);
            Require(Hash(haloA) != Hash(Capture(effects)), label + ": halo moves while held");
            double placedTop = pet.Top;
            pet.ReleaseLift(); pet.AdvancePreview(3100);
            Require(pet.CurrentAction == "dizzy" && pet.HasDizzyStars && !pet.IsDropping && Math.Abs(pet.Top - placedTop) < .1,
                label + ": released dizziness preserves the user placement");
            var expected = c.Resolve(outfit, "dizzy", 400);
            Require(ReferenceEquals(sprite.Source, pet.Art.Frame(c, expected.Sprite, pet.DrawnFrame)), label + ": recovery keeps the current appearance and outfit");
            if (c.Category != "chibi") Require(pet.ActiveMotion is not null || pet.ActiveAuthoredVisual is not null, label + ": portrait has its own recovery gesture");
            if (c.FamilyId == "gpt" && c.Category != "chibi")
                proofs.Add(($"GPT · 3D真人 · {outfit}", Capture(canvas)));
            pet.AdvancePreview(5501);
            Require(pet.CurrentAction == "idle" && !pet.HasDizzyStars && !pet.IsDropping && Math.Abs(pet.Top - placedTop) < .1,
                label + ": automatic recovery clears the halo after three seconds");
            pet.AdvancePreview(6000); pet.Left = x; pet.Top = y; pet.BeginLift();
            for (int i = 1; i <= 5; i++) { pet.AdvancePreview(6000 + i * 100); pet.MoveLift(x, y + (i % 2 == 0 ? 0 : -85)); }
            Require(pet.HasDizzyStars, label + ": vertical shaking also triggers feedback");
            pet.AdvancePreview(9501); pet.ReleaseLift();
            Require(pet.CurrentAction == "idle" && !pet.HasDizzyStars, label + ": holding still before release does not replay stale dizziness");
            pet.AdvancePreview(10000); pet.Left = x; pet.Top = y; pet.State.ReducedMotion = true; pet.BeginLift();
            for (int i = 1; i <= 5; i++) { pet.AdvancePreview(10000 + i * 100); pet.MoveLift(x + (i % 2 == 0 ? 0 : 100), y); }
            var reduced = Capture(effects); pet.AdvancePreview(10800);
            Require(pet.HasDizzyStars && Hash(reduced) == Hash(Capture(effects)), label + ": reduced motion retains a static readable halo");
            pet.ReleaseLift(); Require(pet.ActiveMotion is null, label + ": reduced motion disables body sway");
            pet.StopInteraction(); Require(!pet.HasDizzyStars, label + ": cancelling clears the feedback");
            pet.BeginLift(); pet.MoveLift(x + 100, y); pet.ReleaseLift();
            Require(!pet.HasDizzyStars, label + ": a new ordinary drag never inherits past reversals");
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }
        var board = new DrawingVisual();
        using (var dc = board.RenderOpen())
        {
            dc.DrawRectangle(CloudTheme.Brush("#FFF8F5"), null, new Rect(0, 0, 960, 820));
            for (int i = 0; i < proofs.Count; i++)
            {
                double left = i % 3 * 320, top = i / 3 * 410;
                dc.DrawText(new FormattedText(proofs[i].Label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"), 16, CloudTheme.Brush("#775562"), 1), new Point(left + 30, top + 24));
                dc.DrawImage(proofs[i].Image, new Rect(left - 120, top - 100, 560, 680));
            }
        }
        var proof = new RenderTargetBitmap(960, 820, 96, 96, PixelFormats.Pbgra32); proof.Render(board);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(proof));
        using (var stream = File.Create(Path.Combine(output, "gpt-dizzy-proof.png"))) png.Save(stream);
        File.WriteAllLines(Path.Combine(output, "shake-check.txt"), checks.Append($"{checks.Count} shake and recovery checks passed across {pet.Catalog.Characters.Count*Catalog.BuiltInOutfits.Length} appearances."));
    }
}
