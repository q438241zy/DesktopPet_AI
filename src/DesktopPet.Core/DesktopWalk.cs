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

    public static double Speed(double size, Sprite clip) => size * .22 / ((clip.FrameMs?.Sum() ?? clip.Columns * clip.Rows * 240) / 1000d);
}
