using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Only the user's hand/gift accepts input; the pet remains draggable.</summary>
internal sealed class FiveFeedback : FrameworkElement
{
    internal FiveInteraction? Model;
    internal Point Target, Origin;
    internal double ActorHeight, HandSize;
    internal bool ReducedMotion;
    internal BitmapSource? Frame;
    internal Rect FrameBounds;
    internal double FrontY, BoxWidth;
    private bool dragging, moved;
    private Point dragged, down, approach;
    internal event Action<string>? Selected;
    internal Point UserHand { get; private set; }
    internal Rect HitRegion { get; private set; }
    internal bool Dragging=>dragging;
    internal void Reset(){ReleaseMouseCapture();dragging=false;Model=null;HitRegion=Rect.Empty;InvalidateVisual();}
    internal void Refresh(){InvalidateVisual();}
    internal void PrepareApproach(){approach=moved?dragged:Origin;moved=false;}
    public FiveFeedback()
    {
        Width=560;Height=680;Focusable=true;Cursor=Cursors.Hand;
        MouseLeftButtonDown+=(_,e)=>{if(HitRegion.IsEmpty)return;down=dragged=e.GetPosition(this);dragging=true;moved=false;CaptureMouse();Focus();e.Handled=true;};
        MouseMove+=(_,e)=>{if(!dragging)return;dragged=e.GetPosition(this);moved|=(dragged-down).Length>4;InvalidateVisual();e.Handled=true;};
        MouseLeftButtonUp+=(_,e)=>
        {
            if(!dragging)return;dragging=false;ReleaseMouseCapture();
            var p=e.GetPosition(this);
            if(!moved || (p-Target).Length<Math.Max(18,HandSize*.65)) Activate();
            InvalidateVisual();e.Handled=true;
        };
        LostMouseCapture+=(_,_)=>{dragging=false;InvalidateVisual();};
        KeyDown+=(_,e)=>{if(e.Key is Key.Enter or Key.Space){Activate();e.Handled=true;}};
    }
    internal void Activate()
    {
        if(Model is null)return;
        Selected?.Invoke(Model.Key=="highfive"?"highfive":Model.Phase=="receive"?"deliver":"unwrap");
    }
    protected override HitTestResult? HitTestCore(PointHitTestParameters p)=>HitRegion.Contains(p.HitPoint)?new PointHitTestResult(this,p.HitPoint):null;
    private static Point Mix(Point a,Point b,double t)=>a+(b-a)*Math.Clamp(t,0,1);
    protected override void OnRender(DrawingContext dc)
    {
        HitRegion=Rect.Empty;var m=Model;if(m is null)return;
        double size=HandSize;
        if(m.Key=="highfive")
        {
            UserHand=m.Phase=="approach"?Mix(approach,Target,ReducedMotion?1:m.Time/480):m.Phase=="contact"?Mix(Target,Origin,ReducedMotion?(m.Time<280?0:1):Math.Max(0,m.Time-250)/650):dragging?dragged:Origin;
            DrawHand(dc,UserHand,size,"paper");
            if(m.Phase=="offer"&&m.Time>=750)HitRegion=new Rect(UserHand.X-size/2,UserHand.Y-size/2,size,size);
            if(!ReducedMotion&&m.Phase=="contact"&&m.Time<330)
            { var pen=new Pen(ItemArt.Brush("#E8B566"),2);for(int i=0;i<6;i++){double a=i*Math.PI/3;dc.DrawLine(pen,Target+new Vector(Math.Cos(a)*size*.55,Math.Sin(a)*size*.55),Target+new Vector(Math.Cos(a)*size*.75,Math.Sin(a)*size*.75));} }
        }
        else if(m.Key=="rps" && m.Phase!="choose")
        {
            double lift=!ReducedMotion&&m.Phase=="countdown"?-Math.Cos(m.Time/600*Math.PI*2)*size*.2:0;
            UserHand=Origin+new Vector(0,lift);
            DrawHand(dc,UserHand,size,m.Phase=="countdown"||m.Phase=="shoot"&&m.Time<200?"rock":m.Player??"rock");
        }
        else if(m.Key=="gift")
        {
            if(m.Phase is "receive" or "delivering")
            {
                Point p=m.Phase=="delivering"?Mix(approach,Target,m.Time/650):dragging?dragged:Origin;
                DrawGift(dc,p,size*.84);
                if(m.Phase=="receive")HitRegion=new Rect(p.X-size/2,p.Y-size/2,size,size);
            }
            else if(m.Phase=="holding")HitRegion=new Rect(Target.X-size/2,Target.Y-size/2,size,size);
            else if(m.VisiblePrize is {} prize && (m.Phase=="opened" || m.Phase=="opening"&&m.Time>=420))
            {
                double w=BoxWidth*.7,progress=m.Phase=="opened"?1:Math.Clamp((m.Time-420)/580,0,1);
                dc.PushClip(new RectangleGeometry(new Rect(Target.X-BoxWidth/2,Target.Y-w,BoxWidth,w*2)));
                ItemArt.Draw(dc,Collectibles.Get(prize),new Rect(Target.X-w/2,Target.Y-w*progress,w,w));
                dc.Pop();
                // Repaint the original front wall and fingers above the drawn prize.
                if(Frame is not null && FrontY>FrameBounds.Top)
                {dc.PushClip(new RectangleGeometry(new Rect(Target.X-BoxWidth/2,FrontY,BoxWidth,Math.Max(0,FrameBounds.Bottom-FrontY))));dc.DrawImage(Frame,FrameBounds);dc.Pop();}
            }
        }
    }
    internal static void DrawHand(DrawingContext dc,Point center,double size,string hand)
    {
        dc.PushTransform(new TranslateTransform(center.X-size/2,center.Y-size/2));dc.PushTransform(new ScaleTransform(size/100,size/100));
        string path=hand switch {
            "rock"=>"M25,66 L20,43 Q20,33 30,34 Q30,23 41,29 Q47,21 56,29 Q66,23 73,33 L80,50 Q80,71 67,81 L36,81 Z M30,37 L31,51 M43,31 L44,49 M57,31 L58,49 M70,35 L71,49",
            "scissors"=>"M32,79 Q25,69 23,54 Q23,44 34,48 L36,55 L24,16 Q21,6 30,5 Q37,4 40,14 L48,41 L53,10 Q56,1 64,4 Q69,6 67,17 L62,49 Q76,39 81,51 Q86,70 68,82 Z M47,56 Q58,48 69,57",
            _=>"M29,82 Q18,70 14,51 Q13,42 20,41 Q26,40 31,54 L29,23 Q29,15 36,15 Q42,15 42,23 L42,8 Q42,1 49,2 Q55,2 55,9 L55,25 L58,12 Q60,5 66,7 Q72,9 70,16 L68,33 L74,25 Q79,20 84,25 Q88,29 84,37 L78,68 Q76,79 66,83 Z M43,49 Q55,43 67,49"};
        dc.DrawGeometry(ItemArt.Brush("#F6DCCB"),new Pen(ItemArt.Brush("#AC8278"),2.8){LineJoin=PenLineJoin.Round,StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},ItemArt.Path(path));
        dc.DrawRoundedRectangle(ItemArt.Brush("#D5E9ED"),null,new Rect(30,78,38,17),5,5);dc.Pop();dc.Pop();
    }
    internal static void DrawGift(DrawingContext dc,Point center,double size)
    {
        var rect=new Rect(center.X-size/2,center.Y-size/2,size,size);
        dc.DrawRoundedRectangle(ItemArt.Brush("#FFF8F2"),new Pen(ItemArt.Brush("#C59EA9"),1.2),rect,4,4);
        dc.DrawRectangle(ItemArt.Brush("#E6AABC"),null,new Rect(center.X-size*.08,rect.Y,size*.16,size));
        dc.DrawLine(new Pen(ItemArt.Brush("#E6AABC"),size*.13),new Point(rect.X,center.Y-size*.2),new Point(rect.Right,center.Y-size*.2));
        dc.DrawEllipse(null,new Pen(ItemArt.Brush("#E6AABC"),2),new Point(center.X-size*.16,rect.Top),size*.16,size*.12);
        dc.DrawEllipse(null,new Pen(ItemArt.Brush("#E6AABC"),2),new Point(center.X+size*.16,rect.Top),size*.16,size*.12);
    }
}
