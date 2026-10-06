using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class PetWindow
{
    private ClubPoseVisual? authoredVisual;
    private Sprite? renderedAuthoredSheet;
    internal ClubPoseVisual? ActiveAuthoredVisual => authoredVisual;

    private void ClearAuthoredVisual()
    {
        if(authoredVisual is not null) { surface.Children.Remove(authoredVisual);authoredVisual=null; }
        renderedAuthoredSheet=null;
    }

    private Point? AuthoredEffectAnchor(string key)
    {
        var visual=clubVisual??authoredVisual;
        if(visual is null || renderedAuthoredSheet?.EffectAnchors is not { } anchors)return null;
        var point=visual.Anchor(index=>key switch { "head"=>anchors[index]?.Head,"mouth"=>anchors[index]?.Mouth,_=>anchors[index]?.Body },visual.Current);
        if(point is not { } p)return null;
        double scaleX=visual.RenderTransform.Value.M11;
        return new Point(Canvas.GetLeft(visual)+(.5+(p.X-.5)*scaleX)*visual.Width,Canvas.GetTop(visual)+p.Y*visual.Height);
    }

    private void RenderAuthoredMotion(Sprite sheet,string key,double elapsed)
    {
        if(!sheet.File.StartsWith("outfits/sports/",StringComparison.Ordinal)
            || key != "idle" && Character.MotionFor(State.Outfit,key) is null
            || !ClubPoseVisual.Supports(Character,sheet))
        { ClearAuthoredVisual();return; }
        if(authoredVisual?.Appearance!=Character.Id+"/"+State.Outfit || authoredVisual.SheetFile!=sheet.File)
        {
            ClearAuthoredVisual();authoredVisual=new ClubPoseVisual(Character,State.Outfit,sheet,Art);
            surface.Children.Insert(surface.Children.IndexOf(sprite)+1,authoredVisual);
        }
        if(motionVisual is not null) { surface.Children.Remove(motionVisual);motionVisual=null; }
        var neutral=Character.Resolve(State.Outfit,"idle",0);
        double plane=State.Size*Art.VisibleHeight(Art.Frame(Character,neutral.Sprite,neutral.Frame))/.7;
        if(key=="sleep") plane*=PortraitChoreography.Breath(elapsed,State.ReducedMotion);
        authoredVisual.Width=authoredVisual.Height=plane;authoredVisual.Opacity=State.Opacity;
        Canvas.SetLeft(authoredVisual,CenterX-plane/2);Canvas.SetTop(authoredVisual,FloorY-plane*.92-AirborneOffset);
        double scaleX=key is "walk" or "peek"?DesktopWalk.ScaleX(direction,sheet.Facing):1;
        authoredVisual.RenderTransformOrigin=new Point(.5,.5);
        authoredVisual.RenderTransform=new ScaleTransform(scaleX,1);
        var pose=Motion.Blend(sheet,State.ReducedMotion?0:elapsed);authoredVisual.Update(pose);
        UpdateAuthoredProxy(authoredVisual,sheet,pose,plane,scaleX);
        UsingDrawnAction=key!="walk";
    }

    // Keep hit testing, geometry probes and contact effects on the same authored
    // pose as the mesh. The proxy remains invisible but never keeps an old frame.
    private void UpdateAuthoredProxy(ClubPoseVisual visual,Sprite sheet,ClubPose pose,double plane,double scaleX=1)
    {
        renderedAuthoredSheet=sheet;
        DrawnFrame=pose.Amount<.5?pose.A:pose.B;
        var frame=Art.Frame(Character,sheet,DrawnFrame);
        double size=visual.SourcePixelScale(DrawnFrame)*plane*Math.Max(frame.PixelWidth,frame.PixelHeight);
        sprite.Source=frame;sprite.Width=sprite.Height=size;sprite.Opacity=0;
        groundLine=Art.GroundLine(frame);
        Canvas.SetLeft(sprite,CenterX-size*(.5+scaleX*(Art.HorizontalAnchor(frame)-.5)));
        Canvas.SetTop(sprite,PetTop-AirborneOffset);
        facing.ScaleX=scaleX;sprite.RenderTransform=facing;
        HandTarget=null;handSpan=0;
        if(visual.Contact(sheet,pose) is { } contact)
        {
            HandTarget=new Point(CenterX+scaleX*(contact.X-.5)*plane,Canvas.GetTop(visual)+contact.Y*plane);
            handSpan=contact.Span*plane;
        }
    }
}
