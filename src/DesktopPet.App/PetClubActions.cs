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
    private ButterflyPursuit? butterflyPursuit;
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
        int side = butterflyTarget < walkCenter ? -1 : 1;
        direction = side;
        butterflyPursuit = new(walkCenter, Math.Min(105, State.Size * .47), area.Left + pad, area.Right - pad, side);
        clubStarted=Now;actionStarted=Now;actionUntil=Now+ClubMotion.Duration("butterfly");
    }
    private void TickClub(double dt)
    {
        if(clubAction!="butterfly" || State.ReducedMotion || !CanWalk || butterflyPursuit is null) return;
        double next = butterflyPursuit.Center(Now - butterflyStarted), delta = next - walkCenter;
        if (Math.Abs(delta) > .00001) direction = Math.Sign(delta);
        var clip=Character.MotionFor(State.Outfit,"walk")!;
        walkPlayback.Travel(delta, State.Size, clip);
        walkCenter = next; PlaceWalk(walkCenter);
        Top=WorkArea.Bottom-FloorY;
    }
    private void ClearClub()
    {
        clubAction=null;clubScore=0;butterflyPursuit=null;clubFeedback.Reset();
        if(clubVisual is not null) { surface.Children.Remove(clubVisual);clubVisual=null; }
    }
    private bool RenderClubPose()
    {
        string key=clubAction ?? action;
        var sheet=Character.MotionFor(State.Outfit,key);
        if(!ClubMotion.HasPoses(key,sheet) || !ClubPoseVisual.Supports(Character,sheet))
        {
            if(clubVisual is not null) { surface.Children.Remove(clubVisual);clubVisual=null; }
            return false;
        }
        if(clubVisual?.Appearance!=Character.Id+"/"+State.Outfit || clubVisual.SheetFile!=sheet!.File)
        {
            if(clubVisual is not null)surface.Children.Remove(clubVisual);
            clubVisual=new ClubPoseVisual(Character,State.Outfit,sheet!,Art);
            surface.Children.Insert(surface.Children.IndexOf(sprite)+1,clubVisual);
        }
        if(motionVisual is not null) { surface.Children.Remove(motionVisual);motionVisual=null; }
        ClearAuthoredVisual();
        var idle=Character.Resolve(State.Outfit,"idle",0);
        var neutral=Art.Frame(Character,idle.Sprite,idle.Frame);
        double height=State.Size*Art.VisibleHeight(neutral),plane=height/.7;
        double elapsed=Now-(clubAction is null?actionStarted:clubStarted);
        double sampleTime=State.ReducedMotion?(key=="stretch"?2700:key=="bubbles"?1800:key is "comb" or "wipe"?1650:4300):elapsed;
        var pose=key is "comb" or "wipe"?Motion.Blend(sheet!,sampleTime):ClubMotion.Sample(key,sampleTime,sheet!);
        clubVisual.Width=clubVisual.Height=plane;clubVisual.Opacity=State.Opacity;
        Canvas.SetLeft(clubVisual,CenterX-plane/2);Canvas.SetTop(clubVisual,FloorY-plane*.92);clubVisual.Update(pose);
        AirborneOffset=0;UpdateAuthoredProxy(clubVisual,sheet!,pose,plane);
        UsingDrawnAction=true;bakedProps=sheet!.BakedProps;
        effects.Clear();RenderClubFeedback(key,elapsed,height);LayoutChat();UpdateWalkClock();return true;
    }
    private void RenderClubFeedback(string key,double elapsed,double height)
    {
        bool chibi=Character.Category==CharacterStyles.Chibi;
        if(State.ReducedMotion) elapsed=key=="stretch"?2700:key=="bubbles"?1800:key=="stars"?4300:2000;
        var mouth=AuthoredEffectAnchor("mouth")??new Point(CenterX,FloorY-height*(chibi?.43:.80));
        var palm=HandTarget ?? new Point(CenterX-height*.18,FloorY-height*.55);
        Func<double,Point>? bubbleSource=null;
        var bubbleClip=Character.MotionFor(State.Outfit,key);
        if(key=="bubbles" && clubVisual is { } visual && bubbleClip?.BubbleSources is { } sources)
        {
            bubbleSource=birth=>
            {
                var anchor=visual.Anchor(sources,ClubMotion.Sample("bubbles",birth,bubbleClip!));
                return anchor is { } p?new Point(Canvas.GetLeft(visual)+p.X*visual.Width,Canvas.GetTop(visual)+p.Y*visual.Height)
                    :new Point(mouth.X-height*(chibi?.17:.055),mouth.Y);
            };
        }
        bool finished = Now - butterflyStarted >= ButterflyPursuit.MovingDuration;
        var ahead = new Point(CenterX + direction * Math.Clamp(height * .43, 64, 105), FloorY - height * .92);
        clubFeedback.Update(key,elapsed,height,new Point(CenterX,FloorY),mouth,palm,chibi,State.ReducedMotion,
            ahead,finished,bakedProps,bubbleSource);
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
