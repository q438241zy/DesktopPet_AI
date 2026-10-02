using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

internal sealed class ClubFeedback : FrameworkElement
{
    private string key="";
    private double elapsed,height;
    private Point feet,mouth,palm,butterfly;
    private bool chibi,reduced,landed;
    private readonly HashSet<int> popped=[];
    private readonly List<(int Id,Point At,double Radius)> targets=[];
    internal event Action<string,Point>? Selected;
    internal int VisibleBubbles => key=="bubbles"?targets.Count:0;
    internal bool ButterflyLanded => key=="butterfly" && landed;
    internal Point? FirstTarget => targets.Count>0?targets[0].At:null;
    internal void Reset() { key="";popped.Clear();targets.Clear();InvalidateVisual(); }
    internal void Update(string key,double elapsed,double height,Point feet,Point mouth,Point palm,bool chibi,bool reduced,Point butterfly,bool landed)
    { this.key=key;this.elapsed=elapsed;this.height=height;this.feet=feet;this.mouth=mouth;this.palm=palm;this.chibi=chibi;this.reduced=reduced;this.butterfly=butterfly;this.landed=landed;InvalidateVisual(); }
    protected override HitTestResult? HitTestCore(PointHitTestParameters input)
    {
        var p=input.HitPoint;
        bool outsideBody=Math.Abs(p.X-feet.X)>height*(chibi?.49:.35) || p.Y<feet.Y-height-12;
        return targets.Any(t=>(t.At-p).Length<t.Radius+5) || key=="butterfly" && outsideBody && p.X is >20 and <540 && p.Y>feet.Y-height-65 && p.Y<feet.Y
            ?new PointHitTestResult(this,p):null;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if(Select(e.GetPosition(this))) e.Handled=true; }
    internal bool Select(Point p)
    {
        var hit=targets.FirstOrDefault(t=>(t.At-p).Length<t.Radius+5);
        if(key=="bubbles" && targets.Any(t=>t.Id==hit.Id && (t.At-p).Length<t.Radius+5))popped.Add(hit.Id);
        else if(key!="butterfly" && key!="stars")return false;
        Selected?.Invoke(key,p);InvalidateVisual();return true;
    }
    protected override void OnRender(DrawingContext dc)
    {
        targets.Clear();if(key=="")return;
        double unit=height/210,t=reduced?.5:elapsed/1000;
        Pen Pen(string c,double w=1.4)=>new(CloudTheme.Brush(c),w){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        void Label(string s,Point at,double font=12) => dc.DrawText(new FormattedText(s,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI, Microsoft YaHei UI"),font,CloudTheme.Brush("#8C7889"),VisualTreeHelper.GetDpi(this).PixelsPerDip),at);
        void Star(Point p,double radius,bool bright)
        {
            dc.PushTransform(new TranslateTransform(p.X,p.Y));dc.PushTransform(new ScaleTransform(radius/10,radius/10));
            dc.DrawGeometry(CloudTheme.Brush(bright?"#FFE3A0":"#FFF7E9"),Pen(bright?"#C19D63":"#D9CBBC"),Geometry.Parse("M0,-10 L3,-3 L10,-3 L5,2 L6,9 L0,5 L-6,9 L-5,2 L-10,-3 L-3,-3 Z"));dc.Pop();dc.Pop();
        }
        if(key=="stars")
        {
            int count=ClubMotion.Sample(key,elapsed).Count;
            double[] dx=[-106,-59,0,59,106],dy=[38,72,85,72,38];
            for(int i=0;i<5;i++)
            {
                var at=new Point(feet.X+dx[i]*unit,Math.Max(20,feet.Y-height-dy[i]*unit-8));
                double r=(count==i+1?11:8)*unit;Star(at,r,i<count);
                Label((i+1).ToString(),new Point(at.X-3,at.Y+14*unit),10);
                targets.Add((i,at,r));
            }
            Label(count==0?"一起数星星":$"{count} 颗",new Point(feet.X+height*.36,mouth.Y-20));
        }
        else if(key=="bubbles")
        {
            // The ring and bottle are in the authored poses. Emit only at that ring
            // during the blowing interval, never while the arm is being lifted.
            var origin=new Point(mouth.X-height*(chibi?.17:.055),mouth.Y);
            for(int cycle=0;cycle<4;cycle++)for(int n=0;n<5;n++)
            {
                int id=cycle*5+n;double birth=cycle*3600+1120+n*270,age=(elapsed-birth)/1000;
                if(age<0 || age>3.3 || popped.Contains(id))continue;
                double r=(7+n%3*2)*unit;
                var p=new Point(origin.X-age*(27+n*3)*unit+Math.Sin(age*2+n)*4*unit,origin.Y-age*(21+n*3)*unit);
                dc.PushOpacity(reduced?.75:Math.Clamp((3.3-age)/.5,0,1));
                dc.DrawEllipse(CloudTheme.Brush("#18C2DFF4"),Pen(n%2==0?"#AABFD3":"#CDB8D4",1.2),p,r,r);
                dc.DrawGeometry(null,Pen("#FFFFFE",1.8),Geometry.Parse(FormattableString.Invariant($"M{p.X-r*.5},{p.Y-r*.3} Q{p.X-r*.25},{p.Y-r*.75} {p.X+r*.1},{p.Y-r*.65}")));dc.Pop();
                targets.Add((id,p,r));
            }
        }
        else if(key is "comb" or "wipe")
        {
            double phase=(t%(key=="comb"?1.6:1.4))/(key=="comb"?1.6:1.4),side=(int)(t/(key=="comb"?1.6:1.4))%2==0?-1:1;
            double envelope=Math.Min(1,Math.Min(elapsed/350,(ClubMotion.Duration(key)-elapsed)/400));
            var at=key=="comb"?new Point(feet.X+side*height*.24,feet.Y-height*(chibi?.77:.96)+phase*height*.25)
                :new Point(mouth.X+side*height*(chibi?.15:.065)+Math.Sin(phase*Math.PI*2)*3,mouth.Y-5+Math.Cos(phase*Math.PI*2)*3);
            dc.PushOpacity(Math.Max(0,envelope));
            if(key=="comb")
            {
                dc.PushTransform(new RotateTransform(side*13,at.X,at.Y));
                dc.DrawRoundedRectangle(CloudTheme.Brush("#F2D1DE"),Pen("#C091A3"),new Rect(at.X-16,at.Y-4,32,8),3,3);
                for(int i=0;i<7;i++)dc.DrawLine(Pen("#C091A3",1.5),new Point(at.X-12+i*4,at.Y+3),new Point(at.X-12+i*4,at.Y+11));dc.Pop();
            }
            else { dc.PushTransform(new RotateTransform(side*12,at.X,at.Y));dc.DrawRoundedRectangle(CloudTheme.Brush("#FFF8F0"),Pen("#D6B9C9"),new Rect(at.X-10,at.Y-9,20,18),5,5);dc.DrawLine(Pen("#E4CEDE",1),new Point(at.X-7,at.Y+5),new Point(at.X+7,at.Y+5));dc.Pop(); }
            dc.Pop();
        }
        else if(key=="butterfly")
        {
            // Give the empty lure area visual bounds for WPF hit testing;
            // HitTestCore leaves the character itself available for dragging.
            double top=Math.Max(0,feet.Y-height-65);
            dc.DrawRectangle(Brushes.Transparent,null,new Rect(20,top,520,Math.Max(0,feet.Y-top)));
            var at=butterfly;
            if(!landed)at+=new Vector(Math.Sin(t*2.4)*16,Math.Sin(t*3.2)*13-20);
            double wing=reduced?.8:.35+.65*Math.Abs(Math.Sin(t*(landed?5:12)));
            dc.PushTransform(new TranslateTransform(at.X,at.Y));dc.PushTransform(new ScaleTransform(wing,1));
            dc.DrawGeometry(CloudTheme.Brush("#D7C7EE"),Pen("#A58DBF"),Geometry.Parse("M0,0 C-23,-24 -24,6 -4,8 C-18,21 0,21 0,5 C18,21 23,9 4,7 C27,-8 15,-24 0,0 Z"));dc.Pop();
            dc.DrawLine(Pen("#957DAD",2),new Point(0,-3),new Point(0,10));dc.Pop();
            Label(landed?"停在手心了":"点空处引它过来",new Point(feet.X+height*.32,mouth.Y-26),11);
        }
    }
}
