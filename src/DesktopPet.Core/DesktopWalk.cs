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
    public static double Speed(double size, Sprite clip) => size * .22 / (CycleMilliseconds(clip) / 1000d);
}

/// <summary>The feet advance only with travelled distance. A boundary turn holds a planted pose.</summary>
public sealed class WalkPlayback
{
    public double Milliseconds { get; private set; }
    public double TurnRemaining { get; private set; }
    private int nextDirection;
    public void Reset() { Milliseconds = TurnRemaining = 0; nextDirection = 0; }
    public (double Center, int Direction) Advance(double center, int direction, double seconds, double size, Sprite clip, double left, double right)
    {
        if (TurnRemaining > 0)
        {
            TurnRemaining = Math.Max(0, TurnRemaining - seconds);
            return (center, TurnRemaining == 0 ? nextDirection : direction);
        }
        double speed = DesktopWalk.Speed(size, clip);
        var step = DesktopWalk.Step(center, direction, speed * seconds, left, right);
        Milliseconds += Math.Abs(step.Center - center) / speed * 1000;
        if (step.Direction != direction)
        {
            nextDirection = step.Direction; TurnRemaining = .16; Milliseconds = 0;
            return (step.Center, direction);
        }
        return step;
    }
}
