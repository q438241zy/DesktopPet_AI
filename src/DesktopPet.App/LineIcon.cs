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
        ["highfive"]="M7,21 Q3,17 3,12 Q3,10 5,11 L7,14 V6 Q7,3 9,6 V12 V3 Q11,1 12,4 V12 V5 Q14,3 15,6 V13 L17,8 Q20,7 19,11 L17,19 Q15,23 7,21 Z M4,4 L2,2 M19,3 L21,1",
        ["rps"]="M7,21 Q3,18 4,14 L7,13 L4,5 Q4,2 7,3 L11,11 L13,3 Q14,1 16,3 L15,12 Q20,10 20,15 Q20,20 16,22 Z M8,14 Q12,12 15,15",
        ["gift"]="M4,10 H20 V21 H4 Z M3,7 H21 V11 H3 Z M12,7 V21 M12,7 Q3,7 5,3 Q8,0 12,7 Q21,7 19,3 Q16,0 12,7",
        ["read"]="M12,6 Q7,2 2,4 V20 Q7,18 12,22 Q17,18 22,20 V4 Q17,2 12,6 V22 M5,8 L9,9 M5,12 L9,13 M15,9 L19,8 M15,13 L19,12",
        ["photo"]="M3,6 H7 L9,3 H15 L17,6 H21 Q23,6 23,9 V19 Q23,21 20,21 H4 Q1,21 1,18 V9 Q1,6 3,6 M8,13 A4,4 0 1 0 16,13 A4,4 0 1 0 8,13 M19,9 H20",
        ["cloud"] = "M6,18 C1,18 1,10 6,10 C6,3 17,3 18,10 C24,10 23,18 18,18 Z",
        ["heart"] = "M12,20 C9,17 3,13 3,8 C3,2 10,2 12,7 C14,2 21,2 21,8 C21,13 15,17 12,20 Z",
        ["sun"] = "M8,12 A4,4 0 1 0 16,12 A4,4 0 1 0 8,12 M12,2 L12,4 M12,20 L12,22 M2,12 L4,12 M20,12 L22,12 M5,5 L6.5,6.5 M17.5,17.5 L19,19 M5,19 L6.5,17.5 M17.5,6.5 L19,5",
        ["brush"] = "M9,15 L18,3 Q20,1 22,4 L12,17 Z M9,15 C5,14 5,18 3,20 C8,22 12,21 12,17",
        ["settings"] = "M5,3 L5,8 M5,12 L5,21 M12,3 L12,14 M12,18 L12,21 M19,3 L19,5 M19,9 L19,21 M2,8 L8,8 L8,12 L2,12 Z M9,14 L15,14 L15,18 L9,18 Z M16,5 L22,5 L22,9 L16,9 Z",
        ["cube"] = "M12,2 L21,7 L21,17 L12,22 L3,17 L3,7 Z M3,7 L12,12 L21,7 M12,12 L12,22 M7,4.8 L16,10",
        ["person"] = "M8,7 A4,4 0 1 0 16,7 A4,4 0 1 0 8,7 M4,21 C4,11 20,11 20,21",
        ["member"] = "M4,8 Q3,6 5,7 L9,10 L11,5 Q12,3 13,5 L15,10 L19,7 Q21,6 20,8 L18.5,17 Q18,19 16,19 H8 Q6,19 5.5,17 Z M8,21 H16 M10,14 Q12,16 14,14",
        ["dress"] = "M8,3 L6,8 L9,11 L4,21 L20,21 L15,11 L18,8 L16,3 Q12,8 8,3 Z M9,11 L15,11",
        ["walk"] = "M12,4 A2,2 0 1 0 16,4 A2,2 0 1 0 12,4 M5,11 L10,8 L14,9 L17,13 L21,13 M12,9 L10,15 L4,21 M10,15 L16,17 L16,22",
        ["moon"] = "M19,15 C10,18 6,9 11,3 C0,4 0,19 10,21 C16,23 20,19 21,15 Z",
        ["ball"] = "M3,12 A9,9 0 1 0 21,12 A9,9 0 1 0 3,12 M6,5 Q17,12 8,20 M21,8 Q10,7 4,17",
        ["blocks"] = "M3,13 L11,13 L11,21 L3,21 Z M13,13 L21,13 L21,21 L13,21 Z M8,3 L16,3 L16,11 L8,11 Z",
        ["food"] = "M3,12 L21,12 C20,24 4,24 3,12 Z M7,8 C3,5 10,5 7,2 M15,8 C11,5 18,5 15,2",
        ["letter"] = "M3,5 L21,5 L21,19 L3,19 Z M3,5 L12,13 L21,5",
        ["chat"] = "M5,3 L19,3 Q22,3 22,6 L22,15 Q22,18 19,18 L10,18 L5,22 L5,18 Q2,18 2,15 L2,6 Q2,3 5,3 Z M7,9 L7,10 M12,9 L12,10 M17,9 L17,10",
        ["check"] = "M4,12 L9,17 L20,6",
        ["calendar"] = "M6,5 H18 Q21,5 21,8 V19 Q21,21 18,21 H6 Q3,21 3,18 V8 Q3,5 6,5 M8,3 V7 M16,3 V7 M3,10 H21 M7,14 H9 M14,14 H16 M7,17 H9",
        ["back"] = "M15,4 L7,12 L15,20",
        ["spark"] = "M12,2 L15,9 L22,12 L15,15 L12,22 L9,15 L2,12 L9,9 Z",
        ["nudge"] = "M2,6 L11,6 M7,2 L11,6 L7,10 M12,16 A5,5 0 1 0 22,16 A5,5 0 1 0 12,16 M3,21 L8,21",
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
        ["close"] = "M7.5,7.5 L16.5,16.5 M7.5,16.5 L16.5,7.5"
    }.ToDictionary(pair => pair.Key, pair => { var geometry = Geometry.Parse(pair.Value); geometry.Freeze(); return geometry; });

    // Literal gestures share one silhouette in the radial menu and daily activity buttons.
    private static readonly Dictionary<string, Geometry> ActionShapes = new Dictionary<string, string>
    {
        ["comb"] = "M5 4h9q3 0 3 3v6h-3V8H5q-3 0-3-2t3-2Z M5 8v5m3-5v5m3-5v5m3 0h3v7q-1.5 3-3 0Z M20 3v3m-1.5-1.5h3",
        ["wipe"] = "M5 5q7-3 14 0l1 14q-8 3-16 0Z M7 9h10M8 13q2 2 4 0 2 2 4 0M8 18h8",
        ["stretch"] = "M10 9a2 2 0 1 0 4 0 2 2 0 1 0-4 0M6 3q0 8 6 10 6-2 6-10M12 13v4m-4 5 4-5 4 5M4 2l2 1 2-1m8 0 2 1 2-1",
        ["bubbles"] = "M3 15a5 5 0 1 0 10 0 5 5 0 1 0-10 0M12 6a4 4 0 1 0 8 0 4 4 0 1 0-8 0M17 17a2 2 0 1 0 4 0 2 2 0 1 0-4 0M5 13q1-2 3-2",
        ["stars"] = "m12 3 2.5 5 5.5.8-4 4 .9 5.5-4.9-2.5L7.1 18l.9-5.2-4-4L9.5 8Z M3 21h18",
        ["butterfly"] = "M12 9C1-5 0 13 10 13 0 18 8 24 12 14 16 24 24 18 14 13 24 13 23-5 12 9ZM12 9v7m-3-13 3 6 3-6",
        ["pose"] = "M9.7,4.8 A2.3,2.3 0 1 0 14.3,4.8 A2.3,2.3 0 1 0 9.7,4.8 M5,5 Q5.5,10 9,10 H14 Q17,10 19,7 M12,10 V15 M8,21 L11,15 Q12,14 13,15 L16,21",
        ["think"] = "M15,10 C13,6 5,6 4,12 C2.5,19 7,22 12,20 Q16,18.5 16,14 M7,13 H8 M11.5,13 H12.5 M9,17 H11 M17,9 L17.1,9 M17,4.5 L17.1,4.5 M20.5,4.5 L20.6,4.5",
        ["jump"] = "M9.7,4.5 A2.3,2.3 0 1 0 14.3,4.5 A2.3,2.3 0 1 0 9.7,4.5 M4.5,4.5 L8,9 Q9,10 12,10 Q15,10 16,9 L19.5,4.5 M12,10 V13 M12,13 L9,16.5 Q8.5,17.5 7.5,17.5 H5.5 M12,13 L15,16.5 Q15.5,17.5 16.5,17.5 H18.5 M8,21.5 H16",
        ["curl"] = "M5.4,6.6 A2.6,2.6 0 1 0 10.6,6.6 A2.6,2.6 0 1 0 5.4,6.6 M6,11 C2.5,14.5 4.5,20.5 9,20.5 H18 M9.5,18.5 L13.5,12 Q15,10 16.5,12.5 L19,18.5 M8,11.5 L10,14 Q11,15 12.5,14.5 L15,13.5",
        ["bonk"] = "M6,4.5 H18 Q20,4.5 20,6.5 V8.5 Q20,10.5 18,10.5 H6 Q4,10.5 4,8.5 V6.5 Q4,4.5 6,4.5 Z M8,5 V10 M16,5 V10 M10.5,10.5 V19 Q12,21 13.5,19 V10.5 M4.5,15 L2.5,14 M4.5,19 H2.5",
        ["peek"] = "M7,3 V21 M7,7 C10,3.5 17,5 17,10.5 C17,15 12,17 8,14.5 M10,9.5 L10.1,9.5 M14,9.5 L14.1,9.5 M10.5,12.5 Q12,13.5 13.5,12.5 M3,16 H7 Q10,16 10,18 Q10,20 7,20 H3",
        ["peek-left"] = "M5.5,3 V21 M5.5,6.5 C8.5,3 16,4.5 16,9.5 C16,14 10.5,16 6.5,13 M9,9 L9.1,9 M13,9 L13.1,9 M18.5,19 H10 M12.5,16.5 L10,19 L12.5,21.5",
        ["peek-right"] = "M18.5,3 V21 M18.5,6.5 C15.5,3 8,4.5 8,9.5 C8,14 13.5,16 17.5,13 M15,9 L15.1,9 M11,9 L11.1,9 M5.5,19 H14 M11.5,16.5 L14,19 L11.5,21.5",
        ["headpat"] = "M6.5,12 C3,18 7.5,22 12,21 C17,21 20,16.5 17,12 M8.5,16 Q9.5,17 10.5,16 M13.5,16 Q14.5,17 15.5,16 M20,4 H11 Q8.5,4 7.5,6 L5.5,8 Q5,10 7,9 L10,7.5 H16 M9,7.5 Q8,10 10.5,10 H17 Q19,10 20,8",
        ["poke"] = "M6,10 C6,2.5 18,2.5 18,10 M7.5,18 Q12,22 16.5,18 M8.5,10.5 L10,11 M15.5,10.5 L14,11 M11,16 Q12,17 13,16 M2.5,12 H5 Q8,12 8,14 Q8,16 5,16 H2.5 M21.5,12 H19 Q16,12 16,14 Q16,16 19,16 H21.5",
        ["tickle"] = "M7,8 C7,2 17,2 17,8 M9,8 Q10,9 11,8 M13,8 Q14,9 15,8 M10,11 Q12,14 14,11 M8,16 Q12,20 16,16 M4,14 Q2,16 4,18 M20,14 Q22,16 20,18 M3,10 Q2,11 3,12 M21,10 Q22,11 21,12",
        ["praise"] = "M4,12 C4,4 17,4 17,12 C17,21 4,21 4,12 M7,11 Q8,12 9,11 M12,11 Q13,12 14,11 M8,15 Q10.5,18 13,15 M18,3 V7 M16,5 H20 M20,10 H22",
        ["comfort"] = "M8,8 C8,2 16,2 16,8 M9,8 Q10,9 11,8 M13,8 Q14,9 15,8 M3,12 C3,17 6,20 12,20 C18,20 21,17 21,12 M3,12 L8,15 Q10,17 12,16 Q14,17 16,15 L21,12 M10,12 Q12,14 14,12",
        ["lullaby"] = "M8,11 C4,10 3,14 5,16 C7,19 13,19 15,15 M7,13 Q8,14 9,13 M11,13 Q12,14 13,13 M3,20 H19 M17,3 C13,7 16,11 20,9 M13,3 H15 L13,6 H15",
        ["breakfast"] = "M5,12 H19 Q20.5,12 20,14 C18.5,21 5.5,21 4,14 Q3.5,12 5,12 Z M7,20 H17 M6.5,11 C6,8 9,6.5 12,8 C15,6.5 18,8 17.5,11 M17,3 L13,9 M21,4 L17,9",
        ["snack"] = "M14.5,3.5 C7,1 2,7 3.5,14 C5,21 15,23 19.5,16 Q17,16 17,13 Q13.5,13.5 13,10 Q9.5,9 11,6 Q14,6.5 14.5,3.5 Z M7,10 L7.1,10 M7.5,15 L7.6,15 M12.5,17 L12.6,17 M19.5,6 L19.6,6 M17,2.5 L17.1,2.5",
    }.ToDictionary(pair => pair.Key, pair => { var geometry = Geometry.Parse(pair.Value); geometry.Freeze(); return geometry; });

    protected override void OnRender(DrawingContext dc)
    {
        var shape = ActionShapes.TryGetValue(Glyph, out var gesture) ? gesture
            : Soft && SoftShapes.TryGetValue(Glyph, out var rounded) ? rounded : Shapes.GetValueOrDefault(Glyph, Shapes["cloud"]);
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
