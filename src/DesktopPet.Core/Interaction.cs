namespace DesktopPet.Core;

/// <summary>Only recent, sufficiently long reversals contribute to a dizzy release.</summary>
public sealed class ShakeTracker
{
    private readonly Queue<double> reversals = new();
    private double anchor, lastX, lastTime, legTime;
    private int direction;
    public string Motion { get; private set; } = "pickup";
    public double LastReversal { get; private set; } = double.NegativeInfinity;
    public void Start(double x, double now) { anchor = lastX = x; lastTime = legTime = now; direction = 0; reversals.Clear(); Motion = "pickup"; LastReversal = double.NegativeInfinity; }
    public void Move(double x, double now)
    {
        double delta = x - lastX;
        if (Math.Abs(delta) < 3) return;
        int next = Math.Sign(delta);
        if (direction != 0 && next != direction)
        {
            double distance = Math.Abs(lastX - anchor);
            if (distance >= 40)
            {
                reversals.Enqueue(now);
                LastReversal = now;
                Motion = distance > 120 && distance / Math.Max(1, lastTime - legTime) >= .6 ? "shaken-strong" : "shaken";
            }
            anchor = lastX; legTime = lastTime;
        }
        direction = next; lastX = x; lastTime = now;
        Trim(now);
    }
    private void Trim(double now) { while (reversals.TryPeek(out var t) && now - t > 1500) reversals.Dequeue(); }
    public bool IsDizzy(double now) { Trim(now); return reversals.Count >= 4; }
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
