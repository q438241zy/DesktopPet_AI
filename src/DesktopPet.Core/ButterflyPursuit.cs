namespace DesktopPet.Core;

/// <summary>A short, bounded stroll. Each turn slows to zero before changing direction.</summary>
public sealed class ButterflyPursuit(double origin, double range, double left, double right, int side = 1)
{
    public const double MovingDuration = 11500, Duration = MovingDuration + 1400;
    private static readonly double[] Stops = [0, .82, -.82, .65, -.60, 0];
    public double Center(double elapsed)
    {
        double t = Math.Clamp(elapsed, 0, MovingDuration) / 2300;
        int leg = Math.Min(4, (int)t);
        double eased = .5 - .5 * Math.Cos(Math.PI * Math.Min(1, t - leg));
        double offset = (Stops[leg] + (Stops[leg + 1] - Stops[leg]) * eased) * Math.Max(0, range) * (side < 0 ? -1 : 1);
        return Math.Clamp(origin + offset, Math.Min(left, right), Math.Max(left, right));
    }
}
