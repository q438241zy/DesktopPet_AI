using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet.App;

/// <summary>Original 24-point outline symbols. Color inherits from the enclosing control.</summary>
internal sealed class LineIcon : Control
{
    public string Glyph { get; init; } = "cloud";
    private static readonly Dictionary<string, Geometry> Shapes = new Dictionary<string, string>
    {
        ["cloud"] = "M6,18 C1,18 1,10 6,10 C6,3 17,3 18,10 C24,10 23,18 18,18 Z",
        ["heart"] = "M12,20 C9,17 3,13 3,8 C3,2 10,2 12,7 C14,2 21,2 21,8 C21,13 15,17 12,20 Z",
        ["sun"] = "M8,12 A4,4 0 1 0 16,12 A4,4 0 1 0 8,12 M12,2 L12,4 M12,20 L12,22 M2,12 L4,12 M20,12 L22,12 M5,5 L6.5,6.5 M17.5,17.5 L19,19 M5,19 L6.5,17.5 M17.5,6.5 L19,5",
        ["brush"] = "M9,15 L18,3 Q20,1 22,4 L12,17 Z M9,15 C5,14 5,18 3,20 C8,22 12,21 12,17",
        ["settings"] = "M5,3 L5,8 M5,12 L5,21 M12,3 L12,14 M12,18 L12,21 M19,3 L19,5 M19,9 L19,21 M2,8 L8,8 L8,12 L2,12 Z M9,14 L15,14 L15,18 L9,18 Z M16,5 L22,5 L22,9 L16,9 Z",
        ["cube"] = "M12,2 L21,7 L21,17 L12,22 L3,17 L3,7 Z M3,7 L12,12 L21,7 M12,12 L12,22 M7,4.8 L16,10",
        ["person"] = "M8,7 A4,4 0 1 0 16,7 A4,4 0 1 0 8,7 M4,21 C4,11 20,11 20,21",
        ["dress"] = "M8,3 L6,8 L9,11 L4,21 L20,21 L15,11 L18,8 L16,3 Q12,8 8,3 Z M9,11 L15,11",
        ["walk"] = "M12,4 A2,2 0 1 0 16,4 A2,2 0 1 0 12,4 M5,11 L10,8 L14,9 L17,13 L21,13 M12,9 L10,15 L4,21 M10,15 L16,17 L16,22",
        ["moon"] = "M19,15 C10,18 6,9 11,3 C0,4 0,19 10,21 C16,23 20,19 21,15 Z",
        ["ball"] = "M3,12 A9,9 0 1 0 21,12 A9,9 0 1 0 3,12 M6,5 Q17,12 8,20 M21,8 Q10,7 4,17",
        ["blocks"] = "M3,13 L11,13 L11,21 L3,21 Z M13,13 L21,13 L21,21 L13,21 Z M8,3 L16,3 L16,11 L8,11 Z",
        ["food"] = "M3,12 L21,12 C20,24 4,24 3,12 Z M7,8 C3,5 10,5 7,2 M15,8 C11,5 18,5 15,2",
        ["letter"] = "M3,5 L21,5 L21,19 L3,19 Z M3,5 L12,13 L21,5",
        ["chat"] = "M5,3 L19,3 Q22,3 22,6 L22,15 Q22,18 19,18 L10,18 L5,22 L5,18 Q2,18 2,15 L2,6 Q2,3 5,3 Z M7,9 L7,10 M12,9 L12,10 M17,9 L17,10",
        ["check"] = "M4,12 L9,17 L20,6",
        ["back"] = "M15,4 L7,12 L15,20",
        ["spark"] = "M12,2 L15,9 L22,12 L15,15 L12,22 L9,15 L2,12 L9,9 Z",
        ["headpat"] = "M6,21 L6,13 L4,10 Q3,7 6,8 L9,11 L9,4 Q9,1 11,4 L11,9 L11,3 Q12,1 13,3 L13,9 L13,4 Q15,2 15,5 L15,10 L16,7 Q18,5 18,9 L18,15 Q18,19 15,21 Z",
        ["poke"] = "M7,21 L5,16 Q4,13 7,14 L10,16 L10,7 Q12,4 13,7 L13,12 Q18,10 19,14 L19,18 L17,21 Z M6,3 L5,2 M3,8 L1,8 M18,3 L19,2",
        ["tickle"] = "M3,7 Q6,1 9,7 M3,13 Q6,7 9,13 M3,19 Q6,13 9,19 M14,5 Q18,1 21,5 M13,11 Q18,7 21,11 M14,17 Q18,13 21,17",
        ["peek"] = "M4,3 L13,3 L13,21 L4,21 Z M17,6 Q23,9 17,12 M17,16 L17,21 M16,9 L16.1,9",
        ["peek-left"] = "M13,3 L20,3 L20,21 L13,21 Z M8,7 Q1,11 8,15 M7,11 L7.1,11",
        ["peek-right"] = "M4,3 L11,3 L11,21 L4,21 Z M16,7 Q23,11 16,15 M17,11 L17.1,11",
        ["dance"] = "M14,4 L21,2 L21,16 M14,4 L14,18 M14,8 L21,6 M9,18 A2.5,2 0 1 0 14,18 A2.5,2 0 1 0 9,18 M16,16 A2.5,2 0 1 0 21,16 A2.5,2 0 1 0 16,16 M2,4 L2,10 M5,6 L5,13",
        ["think"] = "M8,18 L8,15 Q3,12 5,7 Q6,2 12,2 Q18,2 19,8 L21,12 L18,12 L18,18 L14,18 L14,22 M8,18 L8,22 M9,8 Q12,5 14,8 Q14,10 12,10 L12,12",
        ["jump"] = "M5,21 L19,21 M12,17 L12,3 M6,9 L12,3 L18,9",
        ["nudge"] = "M2,6 L11,6 M7,2 L11,6 L7,10 M12,16 A5,5 0 1 0 22,16 A5,5 0 1 0 12,16 M3,21 L8,21",
        ["bonk"] = "M4,5 L9,2 L17,10 L12,15 Z M13,14 L7,21 M18,3 L20,2 M21,7 L23,7",
        ["curl"] = "M19,16 C20,5 4,2 3,13 C2,23 20,23 21,17 M8,14 C8,8 17,9 16,15 C15,18 11,18 11,14",
        ["smile"] = "M3,12 A9,9 0 1 0 21,12 A9,9 0 1 0 3,12 M8,9 L8,10 M16,9 L16,10 M8,15 Q12,19 16,15",
        ["close"] = "M6,6 L18,18 M6,18 L18,6",
        ["minimize"] = "M5,12 L19,12",
        ["stop"] = "M6,6 L18,6 L18,18 L6,18 Z",
        ["play"] = "M8,4 L20,12 L8,20 Z",
        ["arrow"] = "M4,12 L20,12 M14,6 L20,12 L14,18"
    }.ToDictionary(pair => pair.Key, pair => { var geometry = Geometry.Parse(pair.Value); geometry.Freeze(); return geometry; });

    protected override void OnRender(DrawingContext dc)
    {
        var pen = new Pen(Foreground ?? CloudTheme.Ink, 1.65) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        dc.DrawGeometry(null, pen, Shapes.GetValueOrDefault(Glyph, Shapes["cloud"]));
        dc.Pop();
    }
}
