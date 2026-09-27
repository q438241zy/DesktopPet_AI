namespace DesktopPet.Core;

public readonly record struct MenuPoint(double X, double Y);
public readonly record struct MenuBounds(double Left, double Top, double Right, double Bottom);

/// <summary>Keep a circular menu intact; at desktop edges open an arc towards the free space.</summary>
public static class RadialMenu
{
    public const double ButtonSize = 44;
    public static MenuPoint[] Place(int count, MenuPoint center, double petSize, MenuBounds bounds)
    {
        if (count < 1) return [];
        double inset = ButtonSize / 2 + 8;
        bool Fits(MenuPoint p) => p.X >= bounds.Left + inset && p.X <= bounds.Right - inset
            && p.Y >= bounds.Top + inset && p.Y <= bounds.Bottom - inset;
        MenuPoint[] Arc(double facing, double span, double radius, bool closed = false) => Enumerable.Range(0, count).Select(i =>
        {
            double angle = (facing - span / 2 + span * i / (closed ? count : Math.Max(1, count - 1))) * Math.PI / 180;
            return new MenuPoint(center.X + Math.Cos(angle) * radius, center.Y + Math.Sin(angle) * radius);
        }).ToArray();
        double basic = Math.Max(petSize / 2 + 34, count * 54 / (2 * Math.PI));
        var ring = Arc(90, 360, basic, true);
        if (ring.All(Fits)) return ring;
        foreach (double span in new[] { 180d, 135, 90 })
        {
            double radius = Math.Max(basic, (count - 1) * 54 / (span * Math.PI / 180));
            foreach (double facing in new[] { -90d, 0, 180, 90, -45, -135, 45, 135 })
            {
                var arc = Arc(facing, span, radius);
                if (arc.All(Fits)) return arc;
            }
        }
        // Extremely small work areas: move the ring as a unit instead of stacking clamped buttons.
        double radiusFallback = count * 54 / (2 * Math.PI);
        double x = Math.Clamp(center.X, bounds.Left + radiusFallback + inset, Math.Max(bounds.Left + radiusFallback + inset, bounds.Right - radiusFallback - inset));
        double y = Math.Clamp(center.Y, bounds.Top + radiusFallback + inset, Math.Max(bounds.Top + radiusFallback + inset, bounds.Bottom - radiusFallback - inset));
        return ring.Select((_, i) => new MenuPoint(x + Math.Sin(i * 2 * Math.PI / count) * radiusFallback,
            y - Math.Cos(i * 2 * Math.PI / count) * radiusFallback)).ToArray();
    }
}
