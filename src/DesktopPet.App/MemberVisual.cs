using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class MemberVisual
{
    internal static (Brush Surface, Brush Ink) Colors(MembershipTier tier) => tier switch
    {
        MembershipTier.BlackGold => (CloudTheme.Sky("#151C23", "#41474D"), CloudTheme.Brush("#D2B890")),
        MembershipTier.Platinum => (CloudTheme.Sky("#E0EFE7", "#93B6A6"), CloudTheme.Brush("#325C49")),
        MembershipTier.Gold => (CloudTheme.Sky("#F3EAD7", "#D7C096"), CloudTheme.Brush("#785B32")),
        MembershipTier.Silver => (CloudTheme.Sky("#F1F3F7", "#C8D0DA"), CloudTheme.Brush("#526071")),
        _ => (CloudTheme.Sky("#EDDBC6", "#CAA587"), CloudTheme.Brush("#704B35"))
    };
    internal static FrameworkElement ActionIcon(string glyph, bool locked, double size = 25)
    {
        var grid = new Grid { Width = size + 8, Height = size + 8, IsHitTestVisible = false };
        grid.Children.Add(new LineIcon { Glyph = glyph, Soft = true, Width = size, Height = size, Opacity = locked ? .46 : 1, Focusable = false });
        if (locked) grid.Children.Add(new MemberLock { Width = 19, Height = 21, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -5, -5, 0) });
        return grid;
    }
}

/// <summary>An original vector lock with cheeks and a little smile, readable at menu size.</summary>
internal sealed class MemberLock : FrameworkElement
{
    public MemberLock() { IsHitTestVisible = false; Focusable = false; }
    protected override void OnRender(DrawingContext dc)
    {
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 26));
        var pen = new Pen(CloudTheme.Brush("#BD7894"), 1.7) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        dc.DrawGeometry(null, new Pen(CloudTheme.Cream, 4), Geometry.Parse("M7,12 V8 C7,1 17,1 17,8 V12"));
        dc.DrawGeometry(null, pen, Geometry.Parse("M7,12 V8 C7,1 17,1 17,8 V12"));
        dc.DrawRoundedRectangle(CloudTheme.Sky("#FFFAEE", "#FADDE8"), new Pen(CloudTheme.Cream, 3), new Rect(3, 11, 18, 13), 5, 5);
        dc.DrawRoundedRectangle(null, pen, new Rect(3, 11, 18, 13), 5, 5);
        dc.DrawEllipse(CloudTheme.Brush("#EFAFC4"), null, new Point(6.5, 19), 1.9, 1.1);
        dc.DrawEllipse(CloudTheme.Brush("#EFAFC4"), null, new Point(17.5, 19), 1.9, 1.1);
        dc.DrawGeometry(null, pen, Geometry.Parse("M8,16 V16.4 M16,16 V16.4 M10,19 Q12,21 14,19"));
        dc.Pop();
    }
}
