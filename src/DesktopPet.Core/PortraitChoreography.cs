namespace DesktopPet.Core;

public static class PortraitChoreography
{
    public static double JumpHeight(double elapsed, double size, bool reduced = false)
    {
        if (reduced || elapsed <= 240 || elapsed >= 880) return 0;
        return Math.Sin(Math.PI * (elapsed - 240) / 640) * size * .30;
    }
    public static double Breath(double elapsed, bool reduced = false) => reduced ? 1 : 1 + .006 * Math.Sin(elapsed / 1000 * 1.7);
    public static bool HitsBody(double x0, double y0, double x1, double y1, double centerX, double top, double height, double ballRadius)
    {
        // Sweep through a tall body ellipse. A high-speed ball cannot tunnel through between frames.
        double rx = height * .21 + ballRadius, ry = height * .45 + ballRadius, cy = top + height * .51;
        return BallPhysics.Hit((x0-centerX)/rx,(y0-cy)/ry,(x1-centerX)/rx,(y1-cy)/ry,0,0,1);
    }
}
