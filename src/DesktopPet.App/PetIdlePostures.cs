using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class PetWindow
{
    private readonly Dictionary<string, IdlePostureClock> postureClocks = [];
    private readonly Dictionary<string, PostureGeometry> postureGeometry = [];
    private sealed record PostureGeometry(IdlePostureFrame Art, double Reference, double FootX, double FootY, Rect Bounds);

    private IdlePostureClock PostureClock(string family)
    {
        if (!postureClocks.TryGetValue(family, out var clock))
        {
            int index = Array.IndexOf(Catalog.BuiltInFamilies, family);
            postureClocks[family] = clock = new(index >= 0 && index % 2 == 1 ? "sit" : "stand");
            clock.Reset(Now);
        }
        return clock;
    }
    internal string PostureMode(string family) => IdlePosture.Normalize(State.Postures.GetValueOrDefault(family));
    internal string PostureFor(string family) => PostureMode(family) is "auto" ? PostureClock(family).Pose : PostureMode(family);
    internal string CurrentPosture => PostureFor(Character.FamilyId);
    internal bool IsRenderingIdlePosture { get; private set; }
    internal void SetPostureMode(string mode)
    {
        State.Postures[Character.FamilyId] = IdlePosture.Normalize(mode);
        CompanionActivity(); Save(); Render(); settings?.RefreshIdlePostures();
    }
    private void ResetIdlePosture() => PostureClock(Character.FamilyId).Reset(Now);
    private void ResetAllIdlePostures() { foreach (var clock in postureClocks.Values) clock.Reset(Now); }
    private bool IdlePostureEligible => action == "idle" && hideJourney is null && !resting && !pressed && !dragging
        && !liftActive && !dropping && !roaming && !exploring && !holdingBall && !flyingBall
        && !conversationActive && ActiveChat is null && careRoutine is null && five is null && clubAction is null && prop is null;
    private void TickIdlePostures()
    {
        bool changed = false;
        foreach (string family in Catalog.Characters.Select(c => c.FamilyId).Distinct())
        {
            bool eligible = IsVisible && !State.ReducedMotion && photoWindow?.IsVisible != true
                && PostureMode(family) == "auto"
                && (family != Character.FamilyId || IdlePostureEligible && menu.Children.Count == 0 && agendaNotice is null && workReminder is null);
            changed |= PostureClock(family).Tick(Now, eligible);
        }
        if (changed) settings?.RefreshIdlePostures();
    }
    private PostureGeometry MeasurePosture(Character character, string outfit, string pose)
    {
        string key = character.Root + "/" + outfit + "/" + pose;
        if (postureGeometry.TryGetValue(key, out var measured)) return measured;
        var art = IdlePosture.Resolve(character, outfit, pose); var sheet = art.Sprite;
        double reference = sheet.ReferenceHeightPixels;
        if (reference <= 0)
            for (int i = 0; i < sheet.Columns * sheet.Rows; i++)
            {
                var f = Art.Frame(character, sheet, i);
                reference = Math.Max(reference, Art.VisibleHeight(f) * Math.Max(f.PixelWidth, f.PixelHeight) * (sheet.FrameScaleFactors?[i] ?? 1));
            }
        var frame = Art.Frame(character, sheet, art.Frame); double extent = Math.Max(frame.PixelWidth, frame.PixelHeight);
        double footX = art.Anchor?.FootX ?? frame.PixelWidth / 2d + (Art.HorizontalAnchor(frame) - .5) * extent;
        double footY = art.Anchor?.FootY ?? frame.PixelHeight / 2d + (Art.GroundLine(frame) - .5) * extent;
        var pixels = new byte[frame.PixelWidth * frame.PixelHeight * 4]; frame.CopyPixels(pixels, frame.PixelWidth * 4, 0);
        int left = frame.PixelWidth, right = 0, top = frame.PixelHeight, bottom = 0;
        for (int y = 0; y < frame.PixelHeight; y++) for (int x = 0; x < frame.PixelWidth; x++)
            if (pixels[(y * frame.PixelWidth + x) * 4 + 3] >= 48) { left = Math.Min(left, x); right = Math.Max(right, x + 1); top = Math.Min(top, y); bottom = y + 1; }
        var bounds = right > left ? new Rect(left, top, right - left, bottom - top) : new Rect(0, 0, frame.PixelWidth, frame.PixelHeight);
        if (postureGeometry.Count >= 256) postureGeometry.Clear();
        return postureGeometry[key] = new(art, Math.Max(1, reference), footX, footY, bounds);
    }
    private void DrawPosture(Image target, Character character, string outfit, string pose, double center, double floor, double height)
    {
        var drawing = MeasurePosture(character, outfit, pose);
        var frame = Art.Frame(character, drawing.Art.Sprite, drawing.Art.Frame);
        double scale = height / drawing.Reference * (drawing.Art.Sprite.FrameScaleFactors?[drawing.Art.Frame] ?? 1);
        double extent = Math.Max(frame.PixelWidth, frame.PixelHeight), size = extent * scale;
        target.Source = frame; target.Width = target.Height = size;
        Canvas.SetLeft(target, center - (drawing.FootX + (extent - frame.PixelWidth) / 2) * scale);
        Canvas.SetTop(target, floor - (drawing.FootY + (extent - frame.PixelHeight) / 2) * scale);
    }
    internal void DrawPosturePreview(Canvas stage, Image image, Character character, string outfit)
    {
        if (stage.ActualWidth <= 0 || stage.ActualHeight <= 0) return;
        double height = stage.ActualHeight * .86;
        foreach (string pose in new[] { "stand", "sit" })
        {
            var p = MeasurePosture(character, outfit, pose); double scale = p.Art.Sprite.FrameScaleFactors?[p.Art.Frame] ?? 1;
            double half = Math.Max(p.FootX - p.Bounds.Left, p.Bounds.Right - p.FootX);
            height = Math.Min(height, Math.Min(stage.ActualWidth * .47 * p.Reference / Math.Max(1, half * scale), stage.ActualHeight * .88 * p.Reference / Math.Max(1, p.Bounds.Height * scale)));
        }
        DrawPosture(image, character, outfit, PostureFor(character.FamilyId), stage.ActualWidth / 2, stage.ActualHeight * .95, height);
    }
    private bool RenderIdlePosture()
    {
        IsRenderingIdlePosture = IdlePostureEligible;
        if (!IsRenderingIdlePosture) return false;
        ClearAuthoredVisual();
        if (motionVisual is not null) { surface.Children.Remove(motionVisual); motionVisual = null; }
        var idle = Character.Resolve(State.Outfit, "idle", 0, true);
        double height = State.Size * Art.VisibleHeight(Art.Frame(Character, idle.Sprite, idle.Frame));
        var drawing = MeasurePosture(Character, State.Outfit, CurrentPosture);
        DrawPosture(sprite, Character, State.Outfit, CurrentPosture, CenterX, FloorY, height);
        var frame = (BitmapSource)sprite.Source;
        // The desktop floor follows visible source pixels; authored preview anchors
        // can differ by a pixel and must not make a dropped pet float above the taskbar.
        groundLine = Art.GroundLine(frame); Canvas.SetTop(sprite, PetTop);
        sprite.RenderTransform = facing; facing.ScaleX = facing.ScaleY = 1; sprite.Opacity = State.Opacity; sprite.Visibility = Visibility.Visible;
        UsingDrawnAction = true; DrawnFrame = drawing.Art.Frame; bakedProps = true; AirborneOffset = 0; HandTarget = null; handSpan = 0;
        effects.Clear(); Canvas.SetTop(bubble, Math.Max(8, PetTop - 78)); LayoutChat(); UpdatePetInput(); UpdateWalkClock();
        return true;
    }
}
