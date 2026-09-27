namespace DesktopPet.Core;

public enum HidePhase { Approach, Hide, Hidden, Peek, Return, Complete }
public readonly record struct HidePose(double Center, int Direction, HidePhase Phase, bool Walking);

/// <summary>A screen-edge journey, calculated in work-area coordinates, including negative monitors.</summary>
public sealed class EdgeHide
{
    public int Side { get; }
    public double Edge { get; }
    public double RestingCenter { get; }
    public double ApproachSeconds { get; }
    public double Duration => ApproachSeconds + 4.6;
    private readonly double start, hidden, peeking;

    public EdgeHide(double center, double left, double right, double size, double speed, int? side = null)
    {
        Side = side is -1 or 1 ? side.Value : (center - left <= right - center ? -1 : 1);
        Edge = Side < 0 ? left : right;
        RestingCenter = Edge - Side * Math.Min(size * .46, (right - left) / 2);
        start = center; hidden = Edge + Side * size * .56; peeking = Edge + Side * size * .12;
        ApproachSeconds = Math.Abs(RestingCenter - start) / Math.Max(1, speed);
    }

    public HidePose At(double seconds)
    {
        double t = Math.Max(0, seconds);
        if (t < ApproachSeconds) return new(start + (RestingCenter - start) * t / ApproachSeconds, Math.Sign(RestingCenter - start), HidePhase.Approach, true);
        t -= ApproachSeconds;
        if (t < .8) return new(Lerp(RestingCenter, hidden, t / .8), Side, HidePhase.Hide, true);
        if (t < 1.45) return new(hidden, -Side, HidePhase.Hidden, false);
        if (t < 1.95) return new(Lerp(hidden, peeking, (t - 1.45) / .5), -Side, HidePhase.Peek, false);
        if (t < 3.65) return new(peeking, -Side, HidePhase.Peek, false);
        if (t < 4.6) return new(Lerp(peeking, RestingCenter, (t - 3.65) / .95), -Side, HidePhase.Return, true);
        return new(RestingCenter, -Side, HidePhase.Complete, false);
    }
    private static double Lerp(double from, double to, double progress)
    { double eased = progress * progress * (3 - 2 * progress); return from + (to - from) * eased; }
}

public readonly record struct DancePose(double X, double Y, double Angle, int Beat);

/// <summary>A sixteen-count sway, side step and hop, always using the selected appearance.</summary>
public sealed class PetDance
{
    public const double BeatMs = 500;
    public const int BeatCount = 16;
    public const double DurationMs = BeatMs * BeatCount;
    public int Hits { get; private set; }
    private int lastTappedBeat = -1;
    public static DancePose At(double elapsed, double size, bool reducedMotion = false)
    {
        double t = Math.Clamp(elapsed / BeatMs, 0, BeatCount);
        int beat = Math.Min(BeatCount - 1, (int)t);
        if (reducedMotion) return new(0, 0, 0, beat);
        double envelope = Math.Min(1, Math.Min(t, BeatCount - t));
        double sway = Math.Sin(t * Math.PI / 2), hop = Math.Pow(Math.Sin(t * Math.PI), 2);
        return new(size * .075 * sway * envelope, -size * .04 * hop * envelope, 7 * sway * envelope, beat);
    }
    public bool Tap(double elapsed)
    {
        int beat = (int)Math.Round(elapsed / BeatMs);
        if (elapsed < 0 || elapsed >= DurationMs || beat >= BeatCount || beat == lastTappedBeat || Math.Abs(elapsed - beat * BeatMs) > 145) return false;
        lastTappedBeat = beat; Hits++; return true;
    }
}
