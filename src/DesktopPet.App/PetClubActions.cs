using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class PetWindow
{
    private ClubPoseVisual? clubVisual;
    private readonly ClubFeedback clubFeedback=new() { Width=560, Height=680 };
    private string? clubAction;
    private double clubStarted, butterflyTarget, butterflyStarted;
    private int clubScore;
    internal string? CurrentClub => clubAction;
    internal ClubPoseVisual? ActiveClubVisual => clubVisual;
    internal int ClubScore => clubScore;
    internal ClubFeedback ClubEffects => clubFeedback;

    private void InitializeClub()
    {
        surface.Children.Insert(surface.Children.IndexOf(menu),clubFeedback);
        clubFeedback.Selected += (kind,at) =>
        {
            if(kind=="bubbles") { clubScore++; Say($"啵！{clubScore}",1000); }
            else if(kind=="stars") { clubStarted=Now; actionStarted=Now; actionUntil=Now+ClubMotion.Duration("stars"); }
            else if(kind=="butterfly") LureButterfly(Left+walkOffset.X+at.X);
        };
        clubFeedback.MouseRightButtonUp += (_,e)=> { HandleRightClick();e.Handled=true; };
    }
    private void StartClubAction(string key)
    {
        Play(key,duration:ClubMotion.Duration(key));
        clubAction=key;clubStarted=Now;clubScore=0;clubFeedback.Reset();
        onMotionEnd=()=> { ClearClub();lastInteraction=Now; };
        if(key=="butterfly")
        {
            if(WorkArea.Bottom-FloorY-Top>1 && !State.ReducedMotion)
            { DropToFloor(()=>StartClubAction(key));return; }
            LureButterfly(Left+walkOffset.X+CenterX+(Random.Shared.Next(2)==0?-145:145));
        }
        Render();
    }
    private void LureButterfly(double target)
    {
        var area=WorkArea;double pad=State.Size*.5;
        butterflyTarget=Math.Clamp(target,area.Left+pad,area.Right-pad);
        butterflyStarted=Now;walkCenter=Left+walkOffset.X+CenterX;walkPlayback.Reset();
        clubStarted=Now;actionStarted=Now;actionUntil=Now+ClubMotion.Duration("butterfly");
    }
    private void TickClub(double dt)
    {
        if(clubAction!="butterfly" || State.ReducedMotion || !CanWalk) return;
        double delta=butterflyTarget-walkCenter;
        if(Math.Abs(delta)<.5) return;
        direction=Math.Sign(delta);
        var clip=Character.MotionFor(State.Outfit,"walk")!;
        double speed=DesktopWalk.Speed(State.Size,clip);
        var step=walkPlayback.Advance(walkCenter,direction,Math.Min(dt,Math.Abs(delta)/speed),State.Size,clip,WorkArea.Left+State.Size*.46,WorkArea.Right-State.Size*.46);
        double moved=Math.Min(Math.Abs(delta),Math.Abs(step.Center-walkCenter));
        walkCenter+=direction*moved;PlaceWalk(walkCenter);
        Top=WorkArea.Bottom-FloorY;
    }
    private void ClearClub()
    {
        clubAction=null;clubScore=0;clubFeedback.Reset();
        if(clubVisual is not null) { surface.Children.Remove(clubVisual);clubVisual=null; }
    }
    private bool RenderClubPose()
    {
        string key=clubAction ?? action;
        if(!ClubMotion.HasPoses(key) || Character.MotionFor(State.Outfit,key) is not { } sheet
            || !sheet.File.StartsWith("motions/cloud-club-",StringComparison.Ordinal)
            || !File.Exists(Path.ChangeExtension(Character.SafeFile(Character.Root,sheet.File),".json")))
        {
            if(clubVisual is not null) { surface.Children.Remove(clubVisual);clubVisual=null; }
            return false;
        }
        if(clubVisual?.Appearance!=Character.Id+"/"+State.Outfit)
        {
            if(clubVisual is not null)surface.Children.Remove(clubVisual);
            clubVisual=new ClubPoseVisual(Character,State.Outfit,sheet,Art);
            surface.Children.Insert(surface.Children.IndexOf(sprite)+1,clubVisual);
        }
        if(danceVisual is not null) { surface.Children.Remove(danceVisual);danceVisual=null; }
        var idle=Character.Resolve(State.Outfit,"idle",0);
        var neutral=Art.Frame(Character,idle.Sprite,idle.Frame);
        double height=State.Size*Art.VisibleHeight(neutral),plane=height/.7;
        double elapsed=Now-(clubAction is null?actionStarted:clubStarted);
        var pose=ClubMotion.Sample(key,State.ReducedMotion?(key=="stretch"?2700:key=="bubbles"?1800:4300):elapsed);
        clubVisual.Width=clubVisual.Height=plane;clubVisual.Opacity=State.Opacity;
        Canvas.SetLeft(clubVisual,CenterX-plane/2);Canvas.SetTop(clubVisual,FloorY-plane*.92);clubVisual.Update(pose);
        // Retain the normal transparent sprite surface for drag and right-click input.
        sprite.Source=neutral;sprite.Width=sprite.Height=State.Size;sprite.Opacity=0;
        groundLine=Art.GroundLine(neutral);Canvas.SetLeft(sprite,CenterX-State.Size/2);Canvas.SetTop(sprite,PetTop);
        sprite.RenderTransform=facing;facing.ScaleX=1;
        UsingDrawnAction=true;DrawnFrame=pose.Amount<.5?pose.A:pose.B;bakedProps=key=="bubbles";AirborneOffset=0;
        effects.Clear();RenderClubFeedback(key,elapsed,height);LayoutChat();UpdateWalkClock();return true;
    }
    private void RenderClubFeedback(string key,double elapsed,double height)
    {
        bool chibi=Character.Category==CharacterStyles.Chibi;
        if(State.ReducedMotion) elapsed=key=="stretch"?2700:key=="bubbles"?1800:key=="stars"?4300:2000;
        var mouth=new Point(CenterX,FloorY-height*(chibi?.43:.80));
        var palm=HandTarget ?? new Point(CenterX-height*.18,FloorY-height*.55);
        double remaining=Math.Abs(butterflyTarget-(Left+walkOffset.X+CenterX));
        clubFeedback.Update(key,elapsed,height,new Point(CenterX,FloorY),mouth,palm,chibi,State.ReducedMotion,
            remaining>2?new Point(butterflyTarget-Left-walkOffset.X,mouth.Y-24):palm,remaining<=2 && Now-butterflyStarted>1200);
        clubFeedback.Opacity=State.Opacity;
        if(key=="stars") clubScore=ClubMotion.Sample("stars",elapsed).Count;
    }
    internal void PreviewClub(string key,double elapsed)
    {
        if(clubAction!=key) { ClearTransient();clubFeedback.Reset(); }
        previewClock=elapsed;clubAction=key;clubStarted=actionStarted=0;action=key;actionUntil=ClubMotion.Duration(key);
        butterflyStarted=0;butterflyTarget=Left+walkOffset.X+CenterX;Render();
    }
}
