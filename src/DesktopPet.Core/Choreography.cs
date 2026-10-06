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
