using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class IdlePostureVerification
{
    internal static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        var approved = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "approved-demo.json"))).RootElement;
        var canvas = (Canvas)pet.Content; var image = canvas.Children.OfType<Image>().Single();
        var idleField = typeof(PetWindow).GetField("lastCompanionActivity", BindingFlags.Instance | BindingFlags.NonPublic)!;
        pet.State.AutoHide = pet.State.Wander = pet.State.ReducedMotion = false; pet.State.Size = 240; pet.BeginPreview();
        void Capture(FrameworkElement view, string file)
        { view.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); PetWindow.SaveCompanionPhoto(bitmap, Path.Combine(output, file + ".png")); }
        foreach (string outfit in Catalog.BuiltInOutfits)
        {
            var gallery = new DrawingVisual(); using (var dc = gallery.RenderOpen())
            {
                dc.DrawRectangle(CloudTheme.Cream, null, new Rect(0, 0, 1600, 1040)); int tile = 0;
                foreach (var character in pet.Catalog.Characters)
                {
                    pet.BeginPreview(); pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit; pet.ApplySettings(); pet.StopInteraction(); pet.SetPostureMode("auto");
                    string first = pet.CurrentPosture; double activity = (double)idleField.GetValue(pet)!;
                    pet.AdvancePreview(29999); Require(pet.CurrentPosture == first, character.Id + "/" + outfit + ": no early pose change");
                    pet.AdvancePreview(30000); Require(pet.CurrentPosture != first && pet.IsRenderingIdlePosture, character.Id + "/" + outfit + ": 30 second pose change");
                    Require((double)idleField.GetValue(pet)! == activity, character.Id + "/" + outfit + ": automatic posture preserves user-idle clock");
                    foreach (string pose in new[] { "stand", "sit" })
                    {
                        pet.SetPostureMode(pose); pet.UpdateLayout();
                        var expected = approved.GetProperty(character.Id).GetProperty(outfit).GetProperty(pose);
                        var actual = IdlePosture.Resolve(character, outfit, pose);
                        Require(actual.Sprite.File == expected.GetProperty("file").GetString() && actual.Frame == expected.GetProperty("frame").GetInt32(), character.Id + "/" + outfit + "/" + pose + ": approved image and frame");
                        var frame = (BitmapSource)image.Source; double extent = Math.Max(frame.PixelWidth, frame.PixelHeight);
                        var reference = character.Resolve(outfit, "idle", 0, true); double canonical = pet.State.Size * pet.Art.VisibleHeight(pet.Art.Frame(character, reference.Sprite, reference.Frame));
                        double expectedScale = canonical / expected.GetProperty("reference").GetDouble() * expected.GetProperty("scale").GetDouble();
                        Require(Math.Abs(image.Width / extent - expectedScale) < .005, character.Id + "/" + outfit + "/" + pose + ": shared anatomical reference scale");
                        double foot = .5 + (expected.GetProperty("footY").GetDouble() - frame.PixelHeight / 2d) / extent;
                        Require(Math.Abs(Canvas.GetTop(image) + image.Height * foot - 468) < 1
                            && Math.Abs(Canvas.GetTop(image) + image.Height * pet.Art.GroundLine(frame) - 468) < .01,
                            character.Id + "/" + outfit + "/" + pose + ": approved anchor and exact desktop sole baseline");
                        Require(image.Opacity > 0 && pet.ActiveAuthoredVisual is null && pet.ActiveMotion is null && pet.InputSurface.Width > 0 && pet.InputSurface.Height > 0, character.Id + "/" + outfit + "/" + pose + ": visible pose and live mouse surface");
                        var shot = new RenderTargetBitmap(560, 680, 96, 96, PixelFormats.Pbgra32); shot.Render(canvas);
                        double x = tile % 8 * 200, y = tile / 8 * 260; dc.DrawImage(shot, new Rect(x, y + 20, 200, 240));
                        dc.DrawText(new FormattedText(character.Id + " / " + pose, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, CloudTheme.Ink, 1), new Point(x + 5, y + 6)); tile++;
                    }
                    pet.AdvancePreview(150000); Require(pet.CurrentPosture == "sit", character.Id + "/" + outfit + ": fixed pose stays fixed");
                }
            }
            var sheet = new RenderTargetBitmap(1600, 1040, 96, 96, PixelFormats.Pbgra32); sheet.Render(gallery); PetWindow.SaveCompanionPhoto(sheet, Path.Combine(output, outfit + ".png"));
            await Task.Delay(1);
        }
        pet.BeginPreview(); pet.SelectCharacter("gpt"); pet.State.Outfits["gpt"] = "sports"; pet.ApplySettings(); pet.StopInteraction(); pet.SetPostureMode("auto");
        string before = pet.CurrentPosture; pet.Play("headpat", duration: 40000); pet.AdvancePreview(39000);
        Require(pet.CurrentAction == "headpat" && !pet.IsRenderingIdlePosture && pet.CurrentPosture == before, "long interaction is never overwritten");
        pet.AdvancePreview(40000); pet.AdvancePreview(69999); Require(pet.CurrentPosture == before, "interaction completion starts a fresh full interval");
        pet.AdvancePreview(70000); Require(pet.CurrentPosture != before, "posture resumes after interaction");
        before = pet.CurrentPosture; pet.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonUpEvent });
        pet.AdvancePreview(160000); Require(pet.IsMenuOpen && pet.CurrentPosture == before, "radial menu holds the posture");
        pet.StopInteraction(); pet.OpenChat(); pet.AdvancePreview(250000); Require(pet.ActiveChat is not null && !pet.IsRenderingIdlePosture && pet.CurrentPosture == before, "inline chat keeps its own pose");
        pet.StopInteraction(); pet.State.ReducedMotion = true; pet.ApplySettings(); pet.AdvancePreview(340000); Require(pet.CurrentPosture == before, "reduced motion suspends automatic changes");
        pet.State.ReducedMotion = false; pet.ApplySettings(); pet.AdvancePreview(369999); Require(pet.CurrentPosture == before, "reduced motion exit restarts 30 seconds"); pet.AdvancePreview(370000); Require(pet.CurrentPosture != before, "automatic posture resumes after reduced motion");
        pet.State.Postures["kimi"] = "auto"; pet.AdvancePreview(370000); before = pet.CurrentPosture; string otherPose = pet.PostureFor("kimi");
        pet.ToggleVisible(); typeof(PetWindow).GetField("previewClock", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pet, 460000d); pet.ToggleVisible();
        pet.AdvancePreview(489999); Require(pet.CurrentPosture == before && pet.PostureFor("kimi") == otherPose, "hidden timer gap restarts both current and roster posture clocks");
        pet.AdvancePreview(490000); Require(pet.CurrentPosture != before && pet.PostureFor("kimi") != otherPose, "current and roster resume after a full visible interval");
        pet.SetPostureMode("sit"); pet.SelectStyle(CharacterStyles.Realistic); pet.StopInteraction(); Require(pet.PostureMode("gpt") == "sit" && pet.CurrentPosture == "sit", "Q and 3D share their family preference");
        pet.SelectCharacter("whale"); pet.StopInteraction(); pet.SetPostureMode("stand"); Require(pet.PostureMode("gpt") == "sit", "other characters keep independent preferences");
        var loaded = new StateStore(output).Load(); Require(loaded.Postures["gpt"] == "sit" && loaded.Postures["whale"] == "stand", "posture preferences reload from isolated state");
        pet.OpenSettings(); var window = Application.Current.Windows.OfType<SettingsWindow>().Single(); window.IsHitTestVisible = false; window.UpdateLayout();
        IEnumerable<Button> Buttons(DependencyObject root)
        { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is Button button) yield return button; foreach (var nested in Buttons(child)) yield return nested; } }
        foreach (string label in new[] { "自动 · 30秒", "站立", "坐下" })
        { Buttons(window).Single(b => AutomationProperties.GetName(b) == "待机姿态 " + label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Require(pet.PostureMode("whale") == (label == "站立" ? "stand" : label == "坐下" ? "sit" : "auto"), "real control selects " + label); }
        Capture(window, "settings-q"); pet.SelectStyle(CharacterStyles.Realistic); window.RefreshLife(); window.Close(); pet.OpenSettings(); window = Application.Current.Windows.OfType<SettingsWindow>().Single(); window.UpdateLayout(); Capture(window, "settings-realistic"); window.Close();
        File.WriteAllLines(Path.Combine(output, "idle-posture-check.txt"), checks.Append($"PASS {checks.Count} idle posture checks."));
    }
}
