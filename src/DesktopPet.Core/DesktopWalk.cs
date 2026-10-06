namespace DesktopPet.Core;

/// <summary>Movement and image orientation share the same signed horizontal direction.</summary>
public static class DesktopWalk
{
    public static (double Center, int Direction) Step(double center, int direction, double distance, double left, double right)
    {
        if (right <= left) return ((left + right) / 2, direction);
        double next = center + direction * distance;
        if (next <= left) return (left, 1);
        if (next >= right) return (right, -1);
        return (next, direction);
    }

    public static double ScaleX(int direction, string sourceFacing) => direction * (sourceFacing == "left" ? -1 : 1);

    public static double CycleMilliseconds(Sprite clip) => clip.FrameMs?.Sum() ?? (clip.Frames?.Length ?? clip.Columns * clip.Rows) * 240;
    // The measured stride is travel per complete gait cycle in units of the
    // user's pet size. Zero preserves the established .22 stride of older art.
    public static double Speed(double size, Sprite clip) => size * (clip.WalkStride == 0 ? .22 : clip.WalkStride) / (CycleMilliseconds(clip) / 1000d);
}

/// <summary>The feet advance only with travelled distance. A boundary turn holds the arriving pose.</summary>
public sealed class WalkPlayback
{
    public double Milliseconds { get; private set; }
    public double TurnRemaining { get; private set; }
    private int nextDirection;
    public void Reset() { Milliseconds = TurnRemaining = 0; nextDirection = 0; }
    public (double Center, int Direction) Advance(double center, int direction, double seconds, double size, Sprite clip, double left, double right)
    {
        double speed = DesktopWalk.Speed(size, clip);
        if (seconds <= 0 || speed <= 0 || right <= left) return (center, direction);
        // Consume all elapsed time, including the part after a turn. Neither a
        // delayed frame nor the monitor refresh rate should change walking speed.
        while (seconds > 0)
        {
            if (TurnRemaining > 0)
            {
                double hold = Math.Min(seconds, TurnRemaining);
                TurnRemaining -= hold; seconds -= hold;
                if (TurnRemaining == 0) direction = nextDirection;
                if (seconds <= 0) break;
            }
            double distance = Math.Max(0, direction > 0 ? right - center : center - left);
            double moving = Math.Min(seconds, distance / speed);
            center += direction * speed * moving;
            Milliseconds += moving * 1000;
            seconds -= moving;
            if (distance <= speed * moving + 1e-9)
            {
                center = direction > 0 ? right : left;
                nextDirection = -direction;
                TurnRemaining = .16;
                // Hold the actual arriving pose. Resetting to frame zero made the
                // feet snap at every screen edge, unrelated to their stride phase.
            }
            else break;
        }
        return (center, direction);
    }
}
