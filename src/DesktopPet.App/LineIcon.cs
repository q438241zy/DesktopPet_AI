using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopPet.App;

/// <summary>Original 24-point outline symbols. Color inherits from the enclosing control.</summary>
internal sealed class LineIcon : Control
{
    public string Glyph { get; init; } = "cloud";
    public bool Soft { get; init; }
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

    // The floating menu uses pill-shaped ends and curved corners, while the rest of the UI keeps its existing symbols.
    private static readonly Dictionary<string, Geometry> SoftShapes = new Dictionary<string, string>
    {
        ["heart"] = "M12,19 C10.5,19 3.5,13.5 3.5,8.5 C3.5,3.5 9.5,3 12,7 C14.5,3 20.5,3.5 20.5,8.5 C20.5,13.5 13.5,19 12,19 Z",
        ["spark"] = "M12,3 C13,3 13.2,7.4 15,9 C16.6,10.8 21,11 21,12 C21,13 16.6,13.2 15,15 C13.2,16.6 13,21 12,21 C11,21 10.8,16.6 9,15 C7.4,13.2 3,13 3,12 C3,11 7.4,10.8 9,9 C10.8,7.4 11,3 12,3 Z",
        ["settings"] = "M6,4 L6,7 M6,12 L6,20 M12,4 L12,13 M12,18 L12,20 M18,4 L18,6 M18,11 L18,20 M3.5,9.5 A2.5,2.5 0 1 0 8.5,9.5 A2.5,2.5 0 1 0 3.5,9.5 M9.5,15.5 A2.5,2.5 0 1 0 14.5,15.5 A2.5,2.5 0 1 0 9.5,15.5 M15.5,8.5 A2.5,2.5 0 1 0 20.5,8.5 A2.5,2.5 0 1 0 15.5,8.5",
        ["ball"] = "M3.5,12 A8.5,8.5 0 1 0 20.5,12 A8.5,8.5 0 1 0 3.5,12 M6.5,5.5 C13,9 13,15 8,19.5 M20,8 C13,7 7,11 4,16",
        ["moon"] = "M17.5,15.5 C10,17 6.5,10 10,4 C4,5 2.3,11 4.5,16 C7,22 15.5,22 19,16.5 Q20,15 17.5,15.5 Z",
        ["blocks"] = "M5,13.5 H9 Q10.5,13.5 10.5,15 V19 Q10.5,20.5 9,20.5 H5 Q3.5,20.5 3.5,19 V15 Q3.5,13.5 5,13.5 Z M15,13.5 H19 Q20.5,13.5 20.5,15 V19 Q20.5,20.5 19,20.5 H15 Q13.5,20.5 13.5,19 V15 Q13.5,13.5 15,13.5 Z M10,3.5 H14 Q15.5,3.5 15.5,5 V9 Q15.5,10.5 14,10.5 H10 Q8.5,10.5 8.5,9 V5 Q8.5,3.5 10,3.5 Z",
        ["food"] = "M5,12 H19 Q20.5,12 20,13.5 C18.5,22 5.5,22 4,13.5 Q3.5,12 5,12 Z M8,8 C5,5 10,5 8,2.5 M15.5,8 C12.5,5 17.5,5 15.5,2.5",
        ["chat"] = "M7,4 H17 Q21,4 21,8 V13 Q21,17 17,17 H11 Q10,17 9,18 L6.5,20 Q5.5,21 5.5,19 V17 Q3,16 3,13 V8 Q3,4 7,4 Z M7.5,10.5 H7.6 M12,10.5 H12.1 M16.5,10.5 H16.6",
        ["walk"] = "M12,4.5 A2,2 0 1 0 16,4.5 A2,2 0 1 0 12,4.5 M5,12 Q8,11 10,8.5 Q11,8 12.5,9 L15.5,12 Q17,13.5 20,13 M12,9 L10,14 Q9.5,15.5 8,17 L4.5,20.5 M10,14.5 L14.5,16 Q16,16.5 16,18 V21",
        ["back"] = "M14.5,5.5 L9,10.5 Q7.5,12 9,13.5 L14.5,18.5",
        ["jump"] = "M5,20 Q12,22 19,20 M12,17 V5 M6.5,9.5 L10.5,5.5 Q12,4 13.5,5.5 L17.5,9.5",
        ["peek"] = "M5.5,3.5 H11 Q12.5,3.5 12.5,5 V19 Q12.5,20.5 11,20.5 H5.5 Q4,20.5 4,19 V5 Q4,3.5 5.5,3.5 Z M16.5,6 C22,6 22,13 16.5,13 M17,16 V20 M17,9.5 H17.1",
        ["peek-left"] = "M14.5,3.5 H19 Q20.5,3.5 20.5,5 V19 Q20.5,20.5 19,20.5 H14.5 Q13,20.5 13,19 V5 Q13,3.5 14.5,3.5 Z M8.5,7 C2,7 2,15 8.5,15 M7,11 H7.1",
        ["peek-right"] = "M5,3.5 H9.5 Q11,3.5 11,5 V19 Q11,20.5 9.5,20.5 H5 Q3.5,20.5 3.5,19 V5 Q3.5,3.5 5,3.5 Z M15.5,7 C22,7 22,15 15.5,15 M17,11 H17.1",
        ["bonk"] = "M5,5 L8,3 Q9,2 10,3 L17,10 Q18,11 17,12 L14,15 Q13,16 12,15 L5,8 Q3.5,6.5 5,5 Z M13,15 L8,20 Q7,21 6,20 M18,4 L19.5,3 M21,8 H22",
        ["headpat"] = "M8,21 Q6,21 6,18 V14 L4.5,11 Q3.5,8.5 5.5,9 Q7,9.5 8.5,12 V5 Q8.5,2.5 10.5,4 V9 M10.5,9 V3.5 Q12.5,1 12.5,4 V9 M12.5,9 V4.5 Q14.5,2.5 14.5,5.5 V10 M14.5,10 V7 Q17,4.5 17,8.5 V15 Q17,20 14,21 Z",
        ["poke"] = "M9,21 Q7,21 6,18 L4.5,15.5 Q4,13.5 6,14 Q7.5,14.5 10,16 V8 Q10,5 12.5,6.5 V12.5 Q18,10 18.5,14.5 V18 Q18.5,21 16,21 Z M6,4 L5,3 M3,8 H2 M18,4 L19,3",
        ["think"] = "M8,21 V16 C2,13 4,4 10,3.5 Q18,2.5 18.5,9 L20,11.5 Q20.5,12.5 18,13 V16 Q18,18 16,18 H14 V21 M9,8 Q12,5.5 14,8 Q15,10 12,11 M12,13.5 V13.6",
        ["dance"] = "M13.5,17.5 V6 Q13.5,4.5 15,4 L19.5,3 Q21,2.5 21,4 V15.5 M13.5,8.5 L21,6.5 M8.5,18 A2.5,2.5 0 1 0 13.5,18 A2.5,2.5 0 1 0 8.5,18 M16,16 A2.5,2.5 0 1 0 21,16 A2.5,2.5 0 1 0 16,16 M3,5 Q1,8 3,10 M6,4 Q4,8 6,12",
        ["close"] = "M7.5,7.5 L16.5,16.5 M7.5,16.5 L16.5,7.5"
    }.ToDictionary(pair => pair.Key, pair => { var geometry = Geometry.Parse(pair.Value); geometry.Freeze(); return geometry; });

    protected override void OnRender(DrawingContext dc)
    {
        var shape = Soft && SoftShapes.TryGetValue(Glyph, out var rounded) ? rounded : Shapes.GetValueOrDefault(Glyph, Shapes["cloud"]);
        var pen = new Pen(Foreground ?? CloudTheme.Ink, Soft ? 1.8 : 1.65) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        if (Soft)
        {
            // A narrow halo follows only the stroke, keeping it readable over wallpaper without adding a button plate.
            var halo = new Pen(CloudTheme.Brush("#A6FFFCFA"), 3.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(null, halo, shape);
        }
        dc.DrawGeometry(null, pen, shape);
        dc.Pop();
    }
}
