using System.Globalization;
using System.Windows;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

internal sealed record FeedbackFrame(string Action, double Elapsed, double Duration, double Size, Point Head, Point Mouth,
    Point Hand, Point Body, Point Feet, Rect WorkBounds, bool Reduced, string? Prop, double PropElapsed,
    Collectible Food, Collectible? Prize, bool BakedProps = false, int? DanceBeat = null, bool OnBeat = false);

/// <summary>Action-specific, code-drawn feedback. Anchors follow the same animated skeleton as the portrait.</summary>
internal sealed class InteractionFeedback : FrameworkElement
{
    private FeedbackFrame? frame;
    public string EffectKey => frame is null ? "" : frame.Action;
    public void Update(FeedbackFrame next) { frame = next; InvalidateVisual(); }
    public void Clear() { frame = null; InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        if (frame is not { } f) return;
        double t = f.Reduced ? .62 : f.Elapsed / 1000;
        double size = f.Size, unit = Math.Clamp(size / 200, .75, 1.3);
        var blue = ItemArt.Brush("#6D9BC3"); var pink = ItemArt.Brush("#EFAEBB");
        Pen Line(string color, double width = 1.5) => new(ItemArt.Brush(color), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        void Text(string value, double x, double y, double fontSize, Brush? brush = null)
        {
            dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI, Microsoft YaHei UI"), fontSize, brush ?? blue, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
        }
        void Symbol(string path, Point at, double scale, string? fill, string stroke)
        {
            dc.PushTransform(new TranslateTransform(at.X, at.Y)); dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawGeometry(fill is null ? null : ItemArt.Brush(fill), Line(stroke, 1.3), ItemArt.Path(path)); dc.Pop(); dc.Pop();
        }
        void Heart(double x, double y, double scale = .55) => Symbol("M12,20 C8,16 3,12 3,8 C3,2 10,2 12,7 C14,2 21,2 21,8 C21,12 16,16 12,20 Z", new Point(x, y), scale * unit, "#F6CBD4", "#DAA0AE");
        void Spark(Point p, double radius = 5, string color = "#D5AE64")
        {
            dc.DrawLine(Line(color), new Point(p.X - radius, p.Y), new Point(p.X + radius, p.Y));
            dc.DrawLine(Line(color), new Point(p.X, p.Y - radius), new Point(p.X, p.Y + radius));
        }
        void Hand(Point p, double rotation = 0, double scale = 1)
        {
            dc.PushTransform(new RotateTransform(rotation, p.X + 10, p.Y + 10));
            Symbol("M6,19 L3,13 Q2,10 5,11 L8,14 L8,4 Q10,1 12,4 L12,11 L12,2 Q14,0 16,3 L16,11 L16,5 Q18,3 20,6 L20,13 L20,9 Q23,7 23,11 L23,18 Q22,25 15,25 L12,25 Q8,25 6,19 Z", p, .78 * unit * scale, "#FFF6EC", "#B99D87");
            dc.Pop();
        }
        void Affection()
        {
            for (int i = 0; i < 3; i++)
            {
                double phase = (t * .65 + i * .3) % 1;
                dc.PushOpacity(f.Reduced ? .8 : Math.Sin(phase * Math.PI));
                Heart(f.Head.X - 33 + i * 25, f.Head.Y - 16 - phase * 31, .4 + i * .06); dc.Pop();
            }
        }
        switch (f.Action)
        {
            case "headpat":
                Hand(new Point(f.Head.X - 14, f.Head.Y - 33 + 6 * Math.Pow(Math.Sin(t * 4), 2)), -70); Affection(); break;
            case "poke":
                double rub = Math.Sin(t * 10) * 3;
                Hand(new Point(f.Mouth.X - 29 + rub, f.Mouth.Y - 9), -80, .75);
                Hand(new Point(f.Mouth.X + 12 - rub, f.Mouth.Y - 11), 80, .75);
                dc.PushOpacity(.28); dc.DrawEllipse(pink, null, new Point(f.Mouth.X - 7, f.Mouth.Y - 2), 4, 2); dc.DrawEllipse(pink, null, new Point(f.Mouth.X + 7, f.Mouth.Y - 2), 4, 2); dc.Pop(); break;
            case "tickle":
                for (int side = -1; side <= 1; side += 2)
                {
                    var at = new Point(f.Body.X + side * size * .11, f.Body.Y - 15 + Math.Sin(t * 15) * 3);
                    Hand(at, side * 85, .8);
                    Symbol("M2,4 Q7,0 12,4 M3,9 Q8,5 13,9", new Point(at.X - side * 13, at.Y - 5), .7, null, "#D8B971");
                }
                Affection(); break;
            case "meal":
            case "eat":
                if (f.BakedProps) break;
                double cycle = (t % 1.2) / 1.2;
                var plate = new Point(f.Body.X - size * .18, f.Body.Y + 18);
                if (f.Action == "meal") ItemArt.Draw(dc, Collectibles.Get("rice"), new Rect(plate.X - 16, plate.Y - 16, 32, 32));
                // Food reaches the mouth repeatedly, shrinks at the bite, then starts another mouthful.
                double reach = Math.Clamp(cycle / .56, 0, 1); reach = reach * reach * (3 - 2 * reach);
                var start = f.Action == "meal" ? plate : new Point(f.Hand.X - 13, f.Hand.Y + 12);
                var food = new Point(start.X + (f.Mouth.X - start.X) * reach, start.Y + (f.Mouth.Y + 3 * unit - start.Y) * reach - Math.Sin(reach * Math.PI) * 9);
                double biteSize = (f.Action == "meal" ? 12 - 5 * reach : 22 - 12 * reach) * unit * (cycle < .7 ? 1 : Math.Max(0, (1 - cycle) / .3));
                if (biteSize > 1) ItemArt.Draw(dc, f.Food, new Rect(food.X - biteSize / 2, food.Y - biteSize / 2, biteSize, biteSize));
                if (cycle > .6) for (int i = 0; i < 3; i++) dc.DrawEllipse(ItemArt.Brush("#E4BA83"), null, new Point(f.Mouth.X - 8 + i * 7, f.Mouth.Y + 4 + (cycle - .6) * (16 + i * 6)), 1.5, 1.5);
                break;
            case "sleep":
                dc.DrawEllipse(ItemArt.Brush("#287DA3BB"), null, new Point(f.Feet.X, f.Feet.Y - 1), size * .2, 4);
                for (int i = 0; i < 3; i++)
                {
                    double phase = (t * .24 + i * .3) % 1;
                    dc.PushOpacity(f.Reduced ? .65 : Math.Sin(phase * Math.PI));
                    Text("z", f.Head.X + 18 + phase * 12, f.Head.Y - 6 - phase * 40, 11 + phase * 7); dc.Pop();
                }
                break;
            case "curl":
                dc.DrawEllipse(ItemArt.Brush("#CBDDED"), Line("#ABC3D8"), new Point(f.Feet.X, f.Feet.Y - 1), size * .23, 5); Heart(f.Body.X + 20, f.Body.Y - 16); break;
            case "chat": case "farewell":
                Symbol("M0,3 Q7,7 0,12 M5,0 Q15,7 5,16", new Point(f.Hand.X + 7, f.Hand.Y - 11), .75 + .1 * Math.Sin(t * 6), null, "#97B7D2"); break;
            case "happy": Affection(); break;
            case "think":
                dc.DrawEllipse(ItemArt.Brush("#F4FBFCFF"), Line("#B2C7DD", 1), new Point(f.Head.X + 30, f.Head.Y - 14), 18, 11);
                for (int i = 0; i < 3; i++) dc.DrawEllipse(blue, null, new Point(f.Head.X + 21 + i * 9, f.Head.Y - 14 - (i == (int)(t * 3) % 3 ? 2 : 0)), 1.6, 1.6); break;
            case "jump": case "pounce":
                dc.DrawEllipse(ItemArt.Brush("#39789BAA"), null, new Point(f.Feet.X, f.Feet.Y - 1), size * .18, 3);
                for (int i = 0; i < 3; i++) dc.DrawLine(Line("#B8CEE0"), new Point(f.Feet.X - 25 + i * 25, f.Feet.Y - 10), new Point(f.Feet.X - 25 + i * 25, f.Feet.Y - 25 - i % 2 * 5)); break;
            case "bonk":
                var hammer = new Point(f.Head.X + 20, f.Head.Y - 20); double swing = -30 + 65 * Math.Pow(Math.Sin(t * 4), 2);
                dc.PushTransform(new RotateTransform(swing, hammer.X + 7, hammer.Y + 24));
                Symbol("M7,5 L11,5 L11,25 L7,25 Z", hammer, 1, "#E3CAA4", "#B59C7B"); Symbol("M0,0 L20,0 Q24,5 20,10 L0,10 Q-4,5 0,0 Z", hammer, 1, "#BBD7EF", "#8DAECB"); dc.Pop();
                if (Math.Pow(Math.Sin(t * 4), 2) > .65) Spark(new Point(f.Head.X + 3, f.Head.Y), 6); break;
            case "ball-ready": case "anticipate":
                for (int i = 0; i < 2; i++) Spark(new Point(f.Hand.X - 13 + i * 31, f.Hand.Y - 8), 3, "#ABC5DB"); break;
            case "ball-hit":
                for (int i = 0; i < 5; i++) { double a = i * Math.PI * 2 / 5; Spark(new Point(f.Hand.X + Math.Cos(a) * 24, f.Hand.Y + Math.Sin(a) * 20), 3); } break;
            case "ball-miss": case "sad":
                Symbol("M6,0 Q-3,12 6,14 Q15,12 6,0 Z", new Point(f.Head.X + 20, f.Head.Y + 2), .65, "#BAD7EF", "#8CB4D2"); break;
            case "kick": case "nudge":
                Symbol("M0,0 L13,0 M2,5 L17,5 M0,10 L12,10", new Point(f.Feet.X + (f.Action == "kick" ? 22 : -38), f.Feet.Y - 16), .8, null, "#A9C6DF"); break;
            case "dizzy": case "faint": case "shaken": case "shaken-strong":
                for (int i = 0; i < 3; i++) Spark(new Point(f.Head.X + Math.Cos(t * 4 + i * 2.1) * 27, f.Head.Y - 12 + Math.Sin(t * 4 + i * 2.1) * 7), 3.5); break;
            case "peek":
                Text("?", f.Head.X + 18, f.Head.Y - 20, 17); break;
            case "pickup":
                Heart(f.Head.X + 20, f.Head.Y - 14, .4); break;
        }
        if (f.DanceBeat is { } beat)
            for (int i = 0; i < 4; i++) dc.DrawEllipse(i == beat % 4 ? ItemArt.Brush(f.OnBeat ? "#66B28B" : "#7CADD6") : ItemArt.Brush("#C4D4E3"), null, new Point(f.Head.X - 21 + i * 14, f.Head.Y - 18), i == beat % 4 ? 3.5 : 2, i == beat % 4 ? 3.5 : 2);
        double propTime = f.Reduced ? .75 : f.PropElapsed / 1000;
        if (f.Prop == "blocks")
        {
            double side = f.Feet.X + size * .32 + 75 < f.WorkBounds.Right ? 1 : -1;
            for (int i = 0; i < Math.Min(5, (int)(propTime / .45) + 1); i++)
            {
                double kick = Math.Clamp((propTime - 3.3) * 1.6, 0, 1);
                double x = f.Feet.X + side * (size * .32 + i % 2 * 22 + kick * (i - 2) * 16) - (side < 0 ? 21 : 0);
                var at = new Rect(Math.Clamp(x, f.WorkBounds.Left + 5, Math.Max(f.WorkBounds.Left + 5, f.WorkBounds.Right - 26)), f.Feet.Y - 22 - (1 - kick) * i * 22, 21, 21);
                dc.PushTransform(new RotateTransform(kick * (i % 2 == 0 ? 45 : -60), at.X + 10, at.Y + 10));
                dc.DrawRoundedRectangle(ItemArt.Brush(new[] { "#EAB79E", "#AFCCB6", "#B8B6D9" }[i % 3]), Line("#FFFFFF", 1), at, 4, 4); dc.Pop();
            }
        }
        if (f.Prop == "treasure" && f.Prize is { } prize)
        {
            double x = f.Body.X + size * .29;
            if (x + 52 > f.WorkBounds.Right) x = f.Body.X - size * .29 - 22;
            var at = new Point(Math.Clamp(x, f.WorkBounds.Left + 34, Math.Max(f.WorkBounds.Left + 34, f.WorkBounds.Right - 52)), f.Body.Y + 10 - Math.Min(propTime, 1) * 12);
            dc.DrawRoundedRectangle(ItemArt.Brush("#F8FFFFFF"), Line("#DCE6F0", 1), new Rect(at.X - 25, at.Y - 27, 72, 66), 13, 13);
            ItemArt.Draw(dc, prize, new Rect(at.X - 7, at.Y - 20, 34, 34)); Text(prize.Name, at.X - 10, at.Y + 18, 11, ItemArt.Brush("#566377"));
            Spark(new Point(at.X - 29, at.Y - 15), 4); Spark(new Point(at.X + 47, at.Y + 8), 4);
        }
        if (f.Prop == "letter")
        {
            var card = new Rect(f.Body.X - 75, f.Head.Y - 80, 150, 55);
            dc.DrawRoundedRectangle(ItemArt.Brush("#FFFBF2"), Line("#E5D5B6", 1), card, 12, 12);
            Heart(card.X + 10, card.Y + 15, .65); Text("谢谢你陪着我", card.X + 38, card.Y + 18, 13, ItemArt.Brush("#9B7F65"));
        }
        if (f.Prop == "confetti")
            for (int i = 0; i < 16; i++) Spark(new Point(f.Head.X - 70 + i * 9, f.Head.Y - 30 + (propTime * 30 + i * 9) % 90), 2.5, i % 2 == 0 ? "#E1B3BA" : "#D6B768");
    }
}
