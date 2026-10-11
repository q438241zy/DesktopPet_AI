using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class SettingsWindow
{
    // This clock belongs to the visible settings window. A hidden desktop pet,
    // a fixed standing/seated preference or a chat draft must not freeze cards.
    private readonly Stopwatch cardPreviewTime = Stopwatch.StartNew();
    private readonly Dictionary<string, IdlePostureClock> cardPreviewClocks = [];
    private readonly List<(Canvas Stage, Image Image, Character Character, string Outfit)> cardPreviews = [];
    private readonly ConditionalWeakTable<BitmapSource, CardSourceHash> cardSourceHashes = new();
    private readonly Dictionary<string, CardFrameCache> cardFrameCaches = [];
    private string cardCacheCategory = "";
    private long cardCacheDraws, cardDecodedDraws;
    private DispatcherTimer? cardPreviewTimer;
    private Button? cardPreviewPauseButton;
    private bool cardPreviewPaused;
    private double CardNow => cardPreviewTime.Elapsed.TotalMilliseconds;
    private bool CardPreviewEligible => IsVisible && IsEnabled && WindowState != WindowState.Minimized
        && page == "partners" && !pet.State.ReducedMotion && !cardPreviewPaused && !ComponentDispatcher.IsThreadModal;

    private void InitializeCardPreviews()
    {
        if (cardPreviewTimer is not null) return;
        cardPreviewTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(33) };
        cardPreviewTimer.Tick += (_, _) => TickCardPreviews();
        IsVisibleChanged += (_, _) => TickCardPreviews();
        IsEnabledChanged += (_, _) => TickCardPreviews();
        StateChanged += (_, _) => TickCardPreviews();
        Closed += (_, _) => { cardPreviewTimer.Stop(); SuspendCardPreviews(); cardPreviews.Clear(); cardFrameCaches.Clear(); };
        cardPreviewTimer.Start(); TickCardPreviews();
    }
    private IdlePostureClock CardClock(string family)
    {
        if (cardPreviewClocks.TryGetValue(family, out var value)) return value;
        int index = Array.IndexOf(Catalog.BuiltInFamilies, family);
        var clock = new IdlePostureClock(index >= 0 && index % 2 == 1 ? "sit" : "stand");
        clock.Reset(CardNow); clock.Tick(CardNow, false); cardPreviewClocks.Add(family, clock); return clock;
    }
    private void RegisterCardPreview(Canvas canvas, Image image, Character character, string outfit)
    {
        if (cardCacheCategory != category) { cardFrameCaches.Clear(); cardCacheCategory = category; }
        CardClock(character.FamilyId);
        cardPreviews.Add((canvas, image, character, outfit));
        canvas.SizeChanged += (_, _) => DrawCardPreview(canvas, image, character, outfit);
        DrawCardPreview(canvas, image, character, outfit);
    }
    private void ClearCardPreviews()
    {
        // Rebuilding a selection on the same page preserves the other cards'
        // schedules. Leaving the page starts a fresh interval on return.
        if (page != "partners") { SuspendCardPreviews(); cardFrameCaches.Clear(); }
        cardPreviews.Clear(); cardPreviewPauseButton = null;
    }
    private void SuspendCardPreviews()
    { double now = CardNow; foreach (var clock in cardPreviewClocks.Values) clock.Tick(now, false); }
    private void TickCardPreviews()
    {
        double now = CardNow; bool eligible = CardPreviewEligible;
        foreach (var clock in cardPreviewClocks.Values) clock.Tick(now, eligible);
        var visible = cardPreviews.Select(p => p.Character.Id).ToHashSet();
        foreach (string id in cardFrameCaches.Keys.Where(id => !visible.Contains(id)).ToArray()) cardFrameCaches.Remove(id);
        if (eligible) foreach (var preview in cardPreviews) DrawCardPreview(preview.Stage, preview.Image, preview.Character, preview.Outfit);
    }
    private sealed record CachedCardFrame(BitmapSource Source, double Width, double Height, double Left, double Top);
    private sealed class CardFrameCache(string outfit, double width, double height)
    {
        internal string Outfit { get; } = outfit;
        internal double Width { get; } = width;
        internal double Height { get; } = height;
        internal Dictionary<string, CachedCardFrame> Frames { get; } = [];
    }
    private void DrawCardPreview(Canvas canvas, Image image, Character character, string outfit)
    {
        if (canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return;
        var clock = CardClock(character.FamilyId); double progress = clock.Progress(CardNow);
        var art = IdlePosture.Resolve(character, outfit, clock.Pose, progress);
        string key = clock.Pose + "/" + art.Sprite.File + "/" + art.Frame;
        if (!cardFrameCaches.TryGetValue(character.Id, out var cache) || cache.Outfit != outfit
            || cache.Width != canvas.ActualWidth || cache.Height != canvas.ActualHeight)
            cardFrameCaches[character.Id] = cache = new(outfit, canvas.ActualWidth, canvas.ActualHeight);
        if (!cache.Frames.TryGetValue(key, out var frame))
        {
            pet.DrawPosturePreview(canvas, image, character, outfit, clock.Pose, progress); cardDecodedDraws++;
            if (image.Source is not BitmapSource source) return;
            // Copy the exact full-resolution cell, not its entire parent atlas.
            // This bounds the page cache without downsampling or retaining every
            // large sprite sheet after ArtCache evicts it.
            var pixels = new byte[source.PixelWidth * source.PixelHeight * 4]; source.CopyPixels(pixels, source.PixelWidth * 4, 0);
            var copy = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, pixels, source.PixelWidth * 4); copy.Freeze();
            frame = new(copy, image.Width, image.Height, Canvas.GetLeft(image), Canvas.GetTop(image));
            if (cache.Frames.Count >= 48) cache.Frames.Remove(cache.Frames.Keys.First());
            cache.Frames[key] = frame;
        }
        else cardCacheDraws++;
        image.Source = frame.Source; image.Width = frame.Width; image.Height = frame.Height;
        Canvas.SetLeft(image, frame.Left); Canvas.SetTop(image, frame.Top);
        image.Tag = new PosturePreviewFrame(character.Id, outfit, clock.Pose, art.Sprite.File, art.Frame, progress);
    }
    private Button CardsPreviewButton()
    {
        cardPreviewPauseButton = MakeButton(cardPreviewPaused ? "继续预览" : "暂停预览", () => SetCardPreviewPaused(!cardPreviewPaused));
        cardPreviewPauseButton.Background = Brushes.Transparent; cardPreviewPauseButton.BorderThickness = new Thickness(0);
        cardPreviewPauseButton.FontSize = 11; cardPreviewPauseButton.ToolTip = "只控制角色卡的随机动作";
        return cardPreviewPauseButton;
    }
    private void SetCardPreviewPaused(bool paused)
    {
        cardPreviewPaused = paused;
        if (cardPreviewPauseButton is not null)
        {
            cardPreviewPauseButton.Content = paused ? "继续预览" : "暂停预览";
            AutomationProperties.SetName(cardPreviewPauseButton, paused ? "继续预览" : "暂停预览");
        }
        TickCardPreviews();
    }

    private sealed record CardSourceHash(string Value, int VisiblePixels);
    internal sealed record CardPreviewSample(string Character, string Family, string Outfit, string Pose, string File, int Frame,
        string PixelHash, string CanvasHash, int SourceVisiblePixels, int CanvasVisiblePixels,
        double Elapsed, double Progress, double Next, double Duration, double Width, double Left, double Top);
    internal IReadOnlyList<CardPreviewSample> CardPreviewSnapshot()
    {
        var result = new List<CardPreviewSample>();
        foreach (var p in cardPreviews)
        {
            // Diagnostic sampling can run before WPF's queued layout/render pass.
            // Flush this app canvas so its pixels and the accompanying frame tag
            // describe the same frame, including after a size or pose change.
            p.Stage.UpdateLayout();
            if (p.Image.Tag is not PosturePreviewFrame frame || p.Image.Source is not BitmapSource bitmap) continue;
            var sourceHash = cardSourceHashes.GetValue(bitmap, source =>
            {
                var pixels = new byte[source.PixelWidth * source.PixelHeight * 4]; source.CopyPixels(pixels, source.PixelWidth * 4, 0);
                int visible = 0; for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] >= 48) visible++;
                return new(Convert.ToHexString(SHA256.HashData(pixels)), visible);
            });
            var canvas = new RenderTargetBitmap(Math.Max(1, (int)p.Stage.ActualWidth), Math.Max(1, (int)p.Stage.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            canvas.Render(p.Stage);
            var canvasPixels = new byte[canvas.PixelWidth * canvas.PixelHeight * 4]; canvas.CopyPixels(canvasPixels, canvas.PixelWidth * 4, 0);
            string canvasHash = Convert.ToHexString(SHA256.HashData(canvasPixels));
            int canvasVisible = 0; for (int i = 3; i < canvasPixels.Length; i += 4) if (canvasPixels[i] > 0) canvasVisible++;
            var clock = CardClock(p.Character.FamilyId);
            result.Add(new(frame.Character, p.Character.FamilyId, frame.Outfit, frame.Pose, frame.File, frame.Frame,
                sourceHash.Value, canvasHash, sourceHash.VisiblePixels, canvasVisible,
                CardNow, frame.Progress, clock.Next, clock.Duration, p.Image.Width, Canvas.GetLeft(p.Image), Canvas.GetTop(p.Image)));
        }
        return result;
    }
    internal sealed record CardCacheStatistics(int Cards, int Frames, long PixelBytes, long CachedDraws, long DecodedDraws);
    internal CardCacheStatistics CardPreviewCacheStatistics() => new(cardFrameCaches.Count, cardFrameCaches.Values.Sum(c => c.Frames.Count),
        cardFrameCaches.Values.SelectMany(c => c.Frames.Values).Sum(f => (long)f.Source.PixelWidth * f.Source.PixelHeight * 4), cardCacheDraws, cardDecodedDraws);
    internal BitmapSource CaptureCardPreviews()
    {
        // Render only the application's character canvases, never the desktop,
        // chat text, account fields or other windows.
        var drawing = new DrawingVisual(); using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(CloudTheme.Cream, null, new Rect(0, 0, 1000, 460)); int index = 0;
            foreach (var p in cardPreviews)
            {
                p.Stage.UpdateLayout();
                var bitmap = new RenderTargetBitmap(Math.Max(1, (int)p.Stage.ActualWidth), Math.Max(1, (int)p.Stage.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(p.Stage); double x = index % 4 * 250, y = index / 4 * 230;
                double scale = Math.Min(230d / bitmap.PixelWidth, 170d / bitmap.PixelHeight);
                double width = bitmap.PixelWidth * scale, height = bitmap.PixelHeight * scale;
                dc.DrawImage(bitmap, new Rect(x + 10 + (230 - width) / 2, y + 10 + (170 - height) / 2, width, height));
                string label = p.Character.Name + " / " + (p.Image.Tag as PosturePreviewFrame)?.Pose;
                dc.DrawText(new FormattedText(label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 14, CloudTheme.Ink, 1), new Point(x + 14, y + 194)); index++;
            }
        }
        var result = new RenderTargetBitmap(1000, 460, 96, 96, PixelFormats.Pbgra32); result.Render(drawing); result.Freeze(); return result;
    }
    internal sealed record CardReviewView(string Page, string Category, bool Paused);
    internal CardReviewView CaptureCardReviewView() => new(page, category, cardPreviewPaused);
    internal void ShowCardReviewStyle(string style)
    { page = "partners"; category = style; Rebuild(); UpdateLayout(); TickCardPreviews(); }
    internal void RestoreCardReviewView(CardReviewView view)
    { page = view.Page; category = view.Category; cardPreviewPaused = view.Paused; Rebuild(); UpdateLayout(); TickCardPreviews(); }
    internal void PauseCardReview(bool paused) => SetCardPreviewPaused(paused);
    internal void NavigateCardReview(string destination) => Navigate(destination);
}
