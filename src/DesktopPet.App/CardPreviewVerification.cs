using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Uses the real settings DispatcherTimer and rendered image pixels, not the pet preview clock.</summary>
internal static class CardPreviewVerification
{
    private sealed record Observation(string Name, double WallMilliseconds, Dictionary<string, string[]> Poses,
        Dictionary<string, int> PixelVariants, Dictionary<string, int> CanvasVariants, SettingsWindow.CardCacheStatistics Cache,
        List<SettingsWindow.CardPreviewSample> Changes);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T value) yield return value;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static void Require(bool condition, string message, List<string> checks)
    { if (!condition) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
    private static async Task<Observation> Observe(SettingsWindow window, string output, string name, List<string> checks, int maximumMilliseconds = 16000)
    {
        window.UpdateLayout(); var first = window.CardPreviewSnapshot();
        Require(first.Count == 8, name + ": all eight cards have rendered image sources", checks);
        var poses = first.ToDictionary(p => p.Character, p => new HashSet<string> { p.Pose });
        var pixels = first.ToDictionary(p => p.Character, p => new HashSet<string> { p.PixelHash });
        var canvases = first.ToDictionary(p => p.Character, p => new HashSet<string> { p.CanvasHash });
        var previous = first.ToDictionary(p => p.Character, p => p.Pose + "/" + p.Frame + "/" + p.PixelHash + "/" + p.CanvasHash);
        var changes = first.ToList(); var clock = Stopwatch.StartNew();
        VerificationImages.Save(window.CaptureCardPreviews(), Path.Combine(output, name + "-start.png"));
        while (clock.ElapsedMilliseconds < maximumMilliseconds)
        {
            await Task.Delay(90);
            foreach (var sample in window.CardPreviewSnapshot())
            {
                poses[sample.Character].Add(sample.Pose); pixels[sample.Character].Add(sample.PixelHash); canvases[sample.Character].Add(sample.CanvasHash);
                string key = sample.Pose + "/" + sample.Frame + "/" + sample.PixelHash + "/" + sample.CanvasHash;
                if (previous[sample.Character] != key) { changes.Add(sample); previous[sample.Character] = key; }
            }
            if (clock.ElapsedMilliseconds >= 8500 && poses.Values.All(v => v.Count == 5) && pixels.Values.All(v => v.Count >= 3)) break;
        }
        VerificationImages.Save(window.CaptureCardPreviews(), Path.Combine(output, name + "-end.png"));
        var observation = new Observation(name, clock.Elapsed.TotalMilliseconds,
            poses.ToDictionary(p => p.Key, p => p.Value.Order().ToArray()), pixels.ToDictionary(p => p.Key, p => p.Value.Count), canvases.ToDictionary(p => p.Key, p => p.Value.Count), window.CardPreviewCacheStatistics(), changes);
        File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(observation, JsonOptions));
        foreach (var (character, seen) in poses)
        {
            foreach (var sample in changes.Where(c => c.Character == character))
                if (sample.SourceVisiblePixels == 0 || sample.CanvasVisiblePixels == 0)
                    throw new InvalidOperationException($"{name}/{sample.Character}/{sample.Outfit}/{sample.Pose}/frame{sample.Frame}: EMPTY FRAME; "
                        + $"source visible alpha≥48={sample.SourceVisiblePixels}, composited canvas alpha>0={sample.CanvasVisiblePixels}; {sample.File}");
            Require(seen.SetEquals(IdlePostureClock.Poses), name + "/" + character + ": real timer displays all five random poses", checks);
            Require(changes.Where(c => c.Character == character).All(c => c.SourceVisiblePixels > 0 && c.CanvasVisiblePixels > 0),
                name + "/" + character + ": every observed source frame and canvas contains visible pixels", checks);
            Require(pixels[character].Count >= 3, name + "/" + character + ": actual rendered image pixels change", checks);
            Require(canvases[character].Count >= 3, name + "/" + character + ": composited card canvas pixels change", checks);
            Require(changes.Where(c => c.Character == character).All(c => c.Duration is >= 1000 and <= 2000 && c.Width > 0),
                name + "/" + character + ": randomized 1–2 second interval and visible image geometry", checks);
        }
        Require(observation.Cache.Cards == 8 && observation.Cache.Frames <= 8 * 48,
            name + ": full-resolution preview cache is bounded to the visible eight appearances", checks);
        return observation;
    }
    private static string VisibleState(SettingsWindow window) => string.Join("|", window.CardPreviewSnapshot().Select(s => $"{s.Character}:{s.Pose}:{s.PixelHash}:{s.Left}:{s.Top}"));
    private static async Task VerifyHold(SettingsWindow window, string label, Action pause, Action resume, List<string> checks)
    {
        pause(); await Task.Delay(120); string held = VisibleState(window);
        await Task.Delay(2300); Require(VisibleState(window) == held, label + ": card pixels stay frozen", checks);
        resume(); await Task.Delay(120); var resumed = window.CardPreviewSnapshot();
        Require(resumed.All(s => s.Next - s.Elapsed >= 750), label + ": resumes with a fresh interval", checks);
        string pose = string.Join("|", resumed.Select(s => s.Pose));
        await Task.Delay(650); Require(string.Join("|", window.CardPreviewSnapshot().Select(s => s.Pose)) == pose, label + ": no immediate catch-up pose", checks);
        await Task.Delay(1600); Require(string.Join("|", window.CardPreviewSnapshot().Select(s => s.Pose)) != pose, label + ": resumes random changes", checks);
    }
    internal static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var observations = new List<Observation>();
        pet.State.AutoHide = pet.State.Wander = pet.State.ReducedMotion = false;
        // Stop only the desktop animation for isolation. Settings must own a real,
        // independent timer even while the desktop's preview time stays at zero.
        pet.BeginPreview(); pet.OpenSettings(); var window = Application.Current.Windows.OfType<SettingsWindow>().Single();
        try
        {
            foreach (string style in CharacterStyles.All)
                foreach (string outfit in Catalog.BuiltInOutfits)
                {
                    foreach (var c in pet.Catalog.Characters)
                    { pet.State.Outfits[c.Id] = outfit; pet.State.Postures[c.FamilyId] = style == CharacterStyles.Chibi ? "sit" : "stand"; }
                    window.ShowCardReviewStyle(style);
                    observations.Add(await Observe(window, output, style + "-" + outfit, checks));
                    Require(pet.Catalog.Characters.All(c => pet.PostureFor(c.FamilyId) == (style == CharacterStyles.Chibi ? "sit" : "stand")),
                        style + "/" + outfit + ": gallery does not change fixed desktop or hero posture", checks);
                }
            var activity = typeof(PetWindow).GetField("lastCompanionActivity", BindingFlags.NonPublic | BindingFlags.Instance)!;
            double beforeActivity = (double)activity.GetValue(pet)!;
            var scheduled = window.CardPreviewSnapshot().ToDictionary(s => s.Character);
            window.ShowCardReviewStyle(CharacterStyles.Realistic);
            Require(window.CardPreviewSnapshot().All(s => s.Next == scheduled[s.Character].Next
                || scheduled[s.Character].Next <= s.Elapsed && scheduled[s.Character].Pose != s.Pose),
                "same-page rebuild preserves schedules or normally advances an already due pose", checks);
            observations.Add(await Observe(window, output, "warm-realistic-sports", checks));
            await Task.Delay(2200);
            Require((double)activity.GetValue(pet)! == beforeActivity, "card movement never records user interaction", checks);
            pet.OpenChat(); pet.UpdateLayout(); window.UpdateLayout();
            var draft = Children<TextBox>(pet.ActiveChat!).First(); draft.Text = "测试草稿，不发送";
            observations.Add(await Observe(window, output, "draft-open", checks));
            Require(draft.Text == "测试草稿，不发送" && pet.ActiveChat is not null, "gallery keeps existing chat draft intact", checks);
            pet.StopInteraction(); pet.Hide();
            observations.Add(await Observe(window, output, "desktop-pet-hidden", checks));
            Require(!pet.IsVisible, "visible settings animate even when desktop pet is hidden", checks); pet.Show();
            await VerifyHold(window, "explicit preview pause", () => window.PauseCardReview(true), () => window.PauseCardReview(false), checks);
            await VerifyHold(window, "reduced motion", () => pet.State.ReducedMotion = true, () => pet.State.ReducedMotion = false, checks);
            await VerifyHold(window, "minimized settings", () => window.WindowState = WindowState.Minimized, () => window.WindowState = WindowState.Normal, checks);
            await VerifyHold(window, "hidden settings", window.Hide, window.Show, checks);
            await VerifyHold(window, "disabled modal owner", () => window.IsEnabled = false, () => window.IsEnabled = true, checks);
            window.NavigateCardReview("preferences");
            Require(window.CardPreviewCacheStatistics().Frames == 0, "leaving gallery releases its source-cell cache", checks);
            await Task.Delay(2300); window.ShowCardReviewStyle(CharacterStyles.Realistic); await Task.Delay(120);
            Require(window.CardPreviewSnapshot().All(s => s.Next - s.Elapsed >= 750), "return from other settings page starts a fresh interval", checks);
            var final = window.CardPreviewSnapshot(); window.Close(); await Task.Delay(2200);
            Require(window.CardPreviewSnapshot().Count == 0 && final.Count == 8 && window.CardPreviewCacheStatistics().Frames == 0,
                "closed settings detach card images, release cache and stop timer", checks);
            File.WriteAllText(Path.Combine(output, "card-preview-observations.json"), JsonSerializer.Serialize(observations, JsonOptions));
            File.WriteAllLines(Path.Combine(output, "card-preview-check.txt"), checks.Append($"PASS {checks.Count} native card checks; 64 appearances; real DispatcherTimer; no API calls."));
        }
        finally { if (window.IsLoaded) window.Close(); pet.EndPreview(); }
    }
    internal static async Task RunLive(SettingsWindow window, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var view = window.CaptureCardReviewView();
        var observations = new List<Observation>();
        try
        {
            foreach (string style in CharacterStyles.All)
            {
                window.ShowCardReviewStyle(style);
                observations.Add(await Observe(window, output, "shortcut-" + style, checks));
            }
            File.WriteAllText(Path.Combine(output, "shortcut-observations.json"), JsonSerializer.Serialize(observations, JsonOptions));
            File.WriteAllLines(Path.Combine(output, "shortcut-card-check.txt"), checks.Append($"PASS {checks.Count} read-only normal-process card checks; both styles; current outfits."));
        }
        finally { if (window.IsLoaded) window.RestoreCardReviewView(view); }
    }
}
