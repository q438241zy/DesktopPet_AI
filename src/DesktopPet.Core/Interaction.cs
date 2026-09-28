namespace DesktopPet.Core;

/// <summary>Deliberate recent reversals on either axis; jitter and diagonal motion cannot double-count.</summary>
public sealed class ShakeTracker
{
    public const int RecoveryMilliseconds = 3000;
    private readonly Axis horizontal = new(), vertical = new();
    public string Motion { get; private set; } = "pickup";
    public double LastReversal { get; private set; } = double.NegativeInfinity;
    public void Start(double x, double now) => Start(x, 0, now);
    public void Start(double x, double y, double now)
    {
        horizontal.Start(x, now); vertical.Start(y, now);
        Motion = "pickup"; LastReversal = double.NegativeInfinity;
    }
    public void Move(double x, double now) => Move(x, 0, now);
    public void Move(double x, double y, double now)
    {
        string? a = horizontal.Move(x, now), b = vertical.Move(y, now);
        if (a is not null || b is not null)
        { LastReversal = now; Motion = a == "shaken-strong" || b == "shaken-strong" ? "shaken-strong" : "shaken"; }
    }
    public bool IsDizzy(double now) => horizontal.IsDizzy(now) || vertical.IsDizzy(now);

    private sealed class Axis
    {
        private readonly Queue<double> reversals = new();
        private double anchor, extreme, legTime, extremeTime, lastMove;
        private int direction;
        public void Start(double x, double now)
        { anchor = extreme = x; legTime = extremeTime = lastMove = now; direction = 0; reversals.Clear(); }
        public string? Move(double x, double now)
        {
            if (now - lastMove > 500) { Start(x, now); return null; }
            lastMove = now;
            if (direction == 0)
            {
                if (Math.Abs(x - anchor) >= 40) { direction = Math.Sign(x - anchor); extreme = x; extremeTime = now; }
                return null;
            }
            if ((x - extreme) * direction >= 0) { extreme = x; extremeTime = now; return null; }
            // Wait for a real change in direction, rather than reacting to pointer noise at the apex.
            if (Math.Abs(x - extreme) < 12) return null;
            double distance = Math.Abs(extreme - anchor), speed = distance / Math.Max(1, extremeTime - legTime);
            string? motion = null;
            if (distance >= 40 && speed >= .18 && now - extremeTime <= 500)
            { reversals.Enqueue(now); motion = distance > 120 && speed >= .6 ? "shaken-strong" : "shaken"; }
            anchor = extreme; legTime = extremeTime; extreme = x; extremeTime = now; direction *= -1;
            Trim(now); return motion;
        }
        private void Trim(double now) { while (reversals.TryPeek(out var t) && now - t > 1500) reversals.Dequeue(); }
        public bool IsDizzy(double now) { Trim(now); return reversals.Count >= 4; }
    }
}

/// <summary>Swept circle contact catches fast balls that cross the pet between frames.</summary>
public static class BallPhysics
{
    public static bool Hit(double x0, double y0, double x1, double y1, double cx, double cy, double radius)
    {
        double dx = x1 - x0, dy = y1 - y0, length = dx * dx + dy * dy;
        double t = length == 0 ? 0 : Math.Clamp(((cx - x0) * dx + (cy - y0) * dy) / length, 0, 1);
        double x = x0 + t * dx - cx, y = y0 + t * dy - cy;
        return x * x + y * y <= radius * radius;
    }
}
