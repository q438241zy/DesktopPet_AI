using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class DetailVerification
{
    public static async Task Run(PetWindow pet, string output)
    {
        var checks = new List<string>();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i); if (child is T typed) yield return typed;
                foreach (var nested in Find<T>(child)) yield return nested;
            }
        }
        RenderTargetBitmap Render(FrameworkElement visual)
        {
            visual.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); return bitmap;
        }
        void Capture(FrameworkElement visual, string file)
        {
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(Render(visual))); using var stream = File.Create(Path.Combine(output, file + ".png")); png.Save(stream);
        }
        var items = new Canvas { Width = 800, Height = 340, Background = ItemArt.Brush("#F5F6F8") };
        for (int i = 0; i < Collectibles.All.Count; i++)
        {
            var icon = new ItemVisual { Item = Collectibles.All[i], Width = 68, Height = 68 }; items.Children.Add(icon); Canvas.SetLeft(icon, i % 10 * 80 + 6); Canvas.SetTop(icon, i / 10 * 170 + 34);
            var label = new TextBlock { Text = Collectibles.All[i].Name, Width = 80, TextAlignment = TextAlignment.Center, FontSize = 12, Foreground = CloudTheme.Ink };
            items.Children.Add(label); Canvas.SetLeft(label, i % 10 * 80); Canvas.SetTop(label, i / 10 * 170 + 116);
        }
        items.Measure(new Size(800, 340)); items.Arrange(new Rect(0, 0, 800, 340)); Capture(items, "twenty-items");
        var hashes = new HashSet<string>();
        foreach (var item in Collectibles.All)
        {
            var visual = new ItemVisual { Item = item, Width = 64, Height = 64 }; visual.Measure(new Size(64, 64)); visual.Arrange(new Rect(0, 0, 64, 64));
            var bitmap = Render(visual); byte[] bytes = new byte[64 * 64 * 4]; bitmap.CopyPixels(bytes, 64 * 4, 0);
            Require(bytes.Where((b, i) => i % 4 == 3 && b > 30).Count() > 250, item.Id + ": artwork has visible coverage");
            Require(hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))), item.Id + ": distinct artwork");
        }
        pet.IsHitTestVisible = false; pet.State.Wander = false; pet.State.ReducedMotion = false; pet.State.Size = 240;
        pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now)); pet.ApplySettings();
        foreach (var character in pet.Catalog.Characters.Where(c => c.Category != "chibi"))
        foreach (string outfit in new[] { "original", "swim", "wedding" })
        {
            pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit; pet.ApplySettings();
            var expected = character.Resolve(outfit, "idle", 0); var texture = pet.Art.Frame(character, expected.Sprite, 0);
            pet.RunInteraction("headpat"); await Task.Delay(90);
            Require(pet.ActiveMotion is { ActionName: "headpat" } rig && ReferenceEquals(rig.Texture, texture), $"{character.Id}/{outfit}: head pat uses selected portrait mesh");
            Require(pet.ActiveMotion!.Pose.Bones.Zip(pet.ActiveMotion.Rig.Rest).Any(p => (p.First.B - p.Second.B).Length > .000001), $"{character.Id}/{outfit}: head pat articulates joints");
            pet.RunInteraction("tickle"); await Task.Delay(90);
            Require(!pet.UsingDrawnAction && pet.ActiveMotion is { ActionName: "tickle" } tickle && ReferenceEquals(tickle.Texture, texture), $"{character.Id}/{outfit}: tickling keeps its own gesture instead of borrowing jump poses");
            pet.RunInteraction("snack"); await Task.Delay(80);
            var food = character.MotionFor(outfit, "eat");
            Require(pet.CurrentAction == "eat" && pet.EffectKey == "eat" && pet.UsingDrawnAction && pet.ActiveMotion is null
                && food is { BakedProps: true } && ReferenceEquals(Find<Image>(pet).Single().Source, pet.Art.Frame(character, food, pet.DrawnFrame)), $"{character.Id}/{outfit}: snack uses this outfit's drawn hands and food");
        }
        foreach (string id in new[] { "deepseek-3d", "deepseek-adult" })
        foreach (string outfit in new[] { "swim", "wedding" })
        {
            pet.SelectCharacter(id); pet.State.Outfits[id] = outfit; pet.ApplySettings();
            pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.Top = pet.WorkArea.Bottom - 468;
            foreach (var (input, expected) in new[] { ("headpat", "headpat"), ("poke", "poke"), ("tickle", "tickle"), ("snack", "eat"), ("checkin", "meal"), ("think", "think"), ("jump", "jump"), ("curl", "curl"), ("bonk", "bonk"), ("ball", "ball-ready"), ("blocks", "build") })
            {
                int checkins = pet.State.CheckIns.Count; pet.RunInteraction(input); await Task.Delay(620);
                string phase = expected == "jump" && pet.CurrentAction == "land" ? "land" : expected;
                Require(pet.CurrentAction == phase && (pet.UsingDrawnAction || pet.ActiveMotion?.ActionName == phase) && pet.EffectKey == phase, $"{id}/{outfit}/{input}: menu invokes its own motion and feedback");
                Require(pet.State.CheckIns.Count == checkins, "replaying breakfast does not add another check-in");
                Capture((FrameworkElement)pet.Content, $"response-{id}-{outfit}-{input}");
                var bitmap = Render((FrameworkElement)pet.Content); byte[] bytes = new byte[560 * 680 * 4]; bitmap.CopyPixels(bytes, 560 * 4, 0);
                Require(bytes.Where((b, i) => i % 4 == 3 && b > 30).Count() > 2500, $"{id}/{outfit}/{input}: transparent desktop renders the actual portrait and effects");
            }
        }
        foreach (string id in new[] { "deepseek-3d", "deepseek-adult" })
        {
            pet.SelectCharacter(id); pet.RunInteraction("rest"); await Task.Delay(1700);
            var sleepingSprite = Find<Image>(pet).Single();
            Require(pet.IsResting && pet.CurrentAction == "sleep" && pet.UsingDrawnAction && pet.ActiveMotion is null
                && pet.DrawnFrame == 8 && sleepingSprite.Visibility == Visibility.Visible, id + ": rest uses the side-lying sleeping pose");
            double before = sleepingSprite.Height; await Task.Delay(450);
            Require(Math.Abs(sleepingSprite.Height - before) > .01, id + ": sleeping body keeps a subtle breathing cycle");
            Capture((FrameworkElement)pet.Content, "sleep-" + id);
            pet.Touch(.16); Require(!pet.IsResting && pet.CurrentAction == "farewell", id + ": touching the resting pet wakes it");
        }
        pet.Touch(.16); Require(pet.CurrentAction == "poke", "adult face hit region invokes cheek rubbing");
        pet.SelectStyle("3d"); pet.Touch(.19); Require(pet.CurrentAction == "poke", "3D face hit region invokes cheek rubbing");
        pet.Touch(.44); Require(pet.CurrentAction == "tickle", "portrait body hit region invokes tickling");
        pet.State.ReducedMotion = true; pet.ApplySettings(); pet.RunInteraction("headpat");
        Require(pet.ActiveMotion is null && pet.EffectKey == "headpat", "reduced motion retains static feedback without mesh motion");
        pet.State.ReducedMotion = false; pet.ApplySettings();
        foreach (var toy in Collectibles.Sports)
        {
            pet.PlayWithToy(toy.Id); pet.UpdateLayout();
            Require(pet.ActiveToy == toy && Find<ItemVisual>(pet).Single().Item == toy
                && Find<ItemVisual>(pet).Single().ActualWidth == toy.Diameter && !pet.HasCaughtBall, "play offers the selected throwable toy: " + toy.Id);
        }
        var randomToys = new HashSet<string>(); for (int i = 0; i < 20; i++) { pet.RunInteraction("ball"); randomToys.Add(pet.ActiveToy.Id); }
        Require(randomToys.Count == 10, "random play reaches all ten toys within two bag rounds");
        pet.RunInteraction("nudge");
        Require(pet.CurrentAction == "ball-ready" && !PetActions.Menu("play", true).Any(entry => entry.Key == "nudge"), "legacy nudge shares ball play without a duplicate menu entry");
        int treasures = pet.State.Treasures.Count;
        pet.RunInteraction("walk"); await Task.Delay(6600);
        Require(pet.IsExploring, "exploration survives the former six-second visual-effect timeout");
        await Task.Delay(2800);
        Require(!pet.IsExploring && pet.State.Treasures.Count == treasures + 1 && pet.LatestFind is not null && pet.State.Treasures[^1] == pet.LatestFind.Name, "finished walk awards exactly one of the twenty mixed items");
        Require(new StateStore(output).Load().Treasures.Count == treasures + 1, "walk reward is saved immediately");
        Capture((FrameworkElement)pet.Content, "mixed-walk-reward");
        pet.RunInteraction("walk"); await Task.Delay(100); pet.StopInteraction(); await Task.Delay(9250);
        Require(pet.State.Treasures.Count == treasures + 1, "cancelling exploration never awards a phantom item");
        foreach (var item in Collectibles.All) pet.State.Treasures.Add(item.Name);
        pet.State.Treasures.Add("一块圆石头"); pet.Save();
        var window = new SettingsWindow(pet) { IsHitTestVisible = false, ShowActivated = false }; window.Show();
        Find<Button>(window).Single(b => AutomationProperties.GetName(b) == "陪伴日常").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        var scroll = Find<ScrollViewer>(window).First(); scroll.ScrollToBottom(); window.UpdateLayout(); Capture((FrameworkElement)window.Content, "collectible-shelf");
        Require(Find<ItemVisual>(window).Count() == 20 && Find<TextBlock>(window).Any(t => t.Text.Contains("一块圆石头")), "shelf displays all twenty collectible types and retains legacy treasures");
        Find<Button>(window).Single(b => AutomationProperties.GetName(b) == "玩收藏 棒球").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(pet.ActiveToy.Id == "baseball", "collected sports equipment can be played with again");
        window.Close(); pet.StopInteraction();
        File.WriteAllLines(Path.Combine(output, "detail-check.txt"), checks.Append($"{checks.Count} detailed interaction checks passed."));
    }
}
