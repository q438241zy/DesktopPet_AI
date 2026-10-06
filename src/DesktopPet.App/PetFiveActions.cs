using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class PetWindow
{
    private FiveInteraction? five;
    private readonly FiveFeedback fiveFeedback=new();
    private readonly Border fivePanel=new(){Width=276,Padding=new Thickness(12),CornerRadius=new CornerRadius(18),Background=CloudTheme.Cream,BorderBrush=CloudTheme.Line,BorderThickness=new Thickness(1),Visibility=Visibility.Collapsed};
    private readonly TextBlock fiveSpeech=new(){TextWrapping=TextWrapping.Wrap,FontSize=12,LineHeight=19,Foreground=CloudTheme.Ink};
    private string fiveControlState="", photoName="我";
    private int fiveSavedRevision;
    internal FiveInteraction? ActiveFive=>five;
    internal FiveFeedback FiveEffects=>fiveFeedback;
    internal Border FivePanel=>fivePanel;
    private void InitializeFive()
    {
        surface.Children.Insert(surface.Children.IndexOf(menu),fiveFeedback);
        surface.Children.Insert(surface.Children.IndexOf(menu),fivePanel);
        fiveFeedback.Selected+=FiveCommand;
        fiveFeedback.MouseRightButtonUp+=(_,e)=>{HandleRightClick();e.Handled=true;};
        fivePanel.MouseRightButtonUp+=(_,e)=>{HandleRightClick();e.Handled=true;};
        AutomationProperties.SetName(fiveFeedback,"互动手掌或礼物：点击、拖到角色手心，或按空格");
    }
    private void StartFive(string key)
    {
        if(Character.FiveFor(State.Outfit) is null){Say("这套外观还没有此互动的画稿。");return;}
        five=new FiveInteraction(State);five.Start(key);fiveSavedRevision=0;fiveControlState="";
        SetAction(key);lastInteraction=Now;Render();
    }
    public void ReadStory(string id)
    {
        RunInteraction("read");if(five?.SelectStory(id)==true){five.BeginRead();Render();}
    }
    private void ClearFive()
    {
        five=null;fiveFeedback.Reset();fivePanel.Visibility=Visibility.Collapsed;fiveControlState="";
    }
    internal void FiveCommand(string command)
    {
        if(five is null)return;
        switch(command)
        {
            case "highfive":fiveFeedback.PrepareApproach();five.HighFive();break;
            case "rock":case "paper":case "scissors":five.Choose(command);break;
            case "deliver":fiveFeedback.PrepareApproach();five.Deliver();break;
            case "unwrap":five.Unwrap();break;
            case "begin":five.BeginRead();break;
            case "pause":five.PauseRead();break;
            case "next":five.NextSentence();break;
            case "shutter":five.Shutter();break;
            case "again":five.Start(five.Key);break;
            case "save-photo":SaveFivePhoto();break;
        }
        lastInteraction=Now;Render();
    }
    private void TickFive(double milliseconds)
    {
        if(five is null)return;
        // Bounded substeps retain the full elapsed time on a late desktop frame.
        while(milliseconds>0){double step=Math.Min(100,milliseconds);five.Advance(step);milliseconds-=step;}
        if(five.Revision!=fiveSavedRevision)
        {fiveSavedRevision=five.Revision;Save();settings?.RefreshLife();}
    }
    private bool RenderFivePose()
    {
        if(five is null || Character.FiveFor(State.Outfit) is not {} art)return false;
        string visualPose=State.ReducedMotion&&five.Key=="rps"&&five.Phase=="countdown"?"fist-mid":five.Pose;
        var pose=art.Poses[visualPose];var sheet=art.Atlases[pose.Atlas];var frame=Art.Frame(Character,sheet,pose.Frame);
        var idle=Character.Resolve(State.Outfit,"idle",0);
        double height=State.Size*Art.VisibleHeight(Art.Frame(Character,idle.Sprite,idle.Frame));
        double scale=height/sheet.ReferenceHeightPixels;
        sprite.Source=frame;sprite.Width=frame.PixelWidth*scale;sprite.Height=frame.PixelHeight*scale;
        sprite.RenderTransform=facing;facing.ScaleX=facing.ScaleY=1;sprite.Opacity=State.Opacity;sprite.Visibility=Visibility.Visible;
        groundLine=pose.FootY/frame.PixelHeight;AirborneOffset=0;
        double x=CenterX-pose.FootX*scale,y=FloorY-pose.FootY*scale;
        Canvas.SetLeft(sprite,x);Canvas.SetTop(sprite,y);
        UsingDrawnAction=true;DrawnFrame=pose.Frame;bakedProps=true;effects.Clear();bubble.Visibility=Visibility.Collapsed;
        Point Anchor(string name,double fallbackX=0,double fallbackY=-.5)=>pose.Anchors.TryGetValue(name,out var p)?new(CenterX+p.X*scale,FloorY+p.Y*scale):new(CenterX+fallbackX*height,FloorY+fallbackY*height);
        var target=Anchor(five.Key=="highfive"?"hand":five.Pose=="gift-empty"?"giftSlot":five.Pose=="gift"?"gift":"receive",-.12,-.55);
        HandTarget=target;
        fiveFeedback.Model=five;fiveFeedback.Target=target;fiveFeedback.ActorHeight=height;fiveFeedback.ReducedMotion=State.ReducedMotion;
        fiveFeedback.HandSize=Math.Clamp(height*(Character.Category==CharacterStyles.Chibi?.24:.16),28,57);
        fiveFeedback.Origin=new Point(Math.Max(45,CenterX-height*.7),FloorY-height*(five.Key=="gift"?.28:.64));
        fiveFeedback.Frame=frame;fiveFeedback.FrameBounds=new Rect(x,y,sprite.Width,sprite.Height);
        fiveFeedback.FrontY=FloorY+pose.FrontY*scale;fiveFeedback.BoxWidth=pose.BoxWidth*scale;fiveFeedback.Opacity=State.Opacity;
        fiveFeedback.Refresh();
        fiveSpeech.Text=five.Speech;BuildFiveControls();fivePanel.Visibility=Visibility.Visible;
        fivePanel.Measure(new Size(fivePanel.Width,double.PositiveInfinity));
        double top=Math.Max(Math.Max(8,WorkArea.Top-Top+8),FloorY-height-fivePanel.DesiredSize.Height-14);
        double left=Math.Clamp(CenterX-fivePanel.Width/2,Math.Max(5,WorkArea.Left-Left+5),Math.Max(5,Math.Min(Width-fivePanel.Width-5,WorkArea.Right-Left-fivePanel.Width-5)));
        Canvas.SetLeft(fivePanel,left);Canvas.SetTop(fivePanel,top);
        UpdateWalkClock();return true;
    }
    private void BuildFiveControls()
    {
        if(five is null)return;
        string state=five.Key+"/"+five.Phase+"/"+five.Story.Id;
        if(fiveControlState==state)return;fiveControlState=state;
        if(fiveSpeech.Parent is Panel old)old.Children.Remove(fiveSpeech);
        var stack=new StackPanel();fivePanel.Child=stack;
        var heading=new DockPanel();var close=FiveButton("结束互动","stop",StopInteraction);close.Width=25;close.Height=25;close.Padding=new Thickness(3);DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);
        heading.Children.Add(new TextBlock{Text=PetActions.Five.Single(a=>a.Key==five.Key).Title,FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=CloudTheme.Ink,VerticalAlignment=VerticalAlignment.Center});stack.Children.Add(heading);
        fiveSpeech.Margin=new Thickness(0,5,0,7);stack.Children.Add(fiveSpeech);
        var row=new WrapPanel();stack.Children.Add(row);
        void B(string label,string command,string icon="")=>row.Children.Add(FiveButton(label,icon,()=>FiveCommand(command)));
        switch(five.Key)
        {
            case "highfive": B("递出手掌","highfive","highfive");break;
            case "rps":
                if(five.Phase=="choose")foreach(string hand in FiveInteraction.Hands)B(FiveInteraction.HandName(hand),hand,hand=="scissors"?"rps":"highfive");
                else if(five.Phase=="revealed")B("再猜一次","again");
                break;
            case "gift":
                if(five.Phase=="receive")B("递出礼物","deliver","gift");
                else if(five.Phase=="holding")B("一起拆开","unwrap","gift");
                else if(five.Phase=="opened")B("再送一份","again","gift");
                break;
            case "read":
                var select=new ComboBox{ItemsSource=StoryLibrary.All,DisplayMemberPath="Title",SelectedItem=five.Story,Width=242,Margin=new Thickness(0,0,0,6)};
                select.SelectionChanged+=(_,_)=>{if(select.SelectedItem is CompanionStory story&&five?.Story.Id!=story.Id){five?.SelectStory(story.Id);Render();}};
                AutomationProperties.SetName(select,"选择共读故事");stack.Children.Insert(stack.Children.Count-1,select);
                if(five.Phase is "ready" or "finished")B(five.Phase=="finished"?"再读一遍":"开始共读","begin","read");
                else if(five.Phase is "reading" or "turning" or "paused")
                {B(five.Phase=="paused"?"继续":"暂停","pause");if(five.Phase!="turning")B(five.Sentence==five.Story.Sentences.Count-1?"读完":"下一句","next");}
                break;
            case "photo":
                if(five.Phase is "compose" or "saved")
                {
                    var name=new TextBox{Text=photoName,MaxLength=20,Width=110,Margin=new Thickness(0,2,6,2),Padding=new Thickness(6)};
                    name.TextChanged+=(_,_)=>photoName=name.Text.Trim();AutomationProperties.SetName(name,"合照昵称");row.Children.Add(name);B("拍合照","shutter","photo");
                }
                else if(five.Phase=="capture") {B("保存合照","save-photo","photo");B("重拍","again");}
                break;
        }
    }
    private static Button FiveButton(string label,string icon,Action clicked)
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        if(icon.Length>0)row.Children.Add(CloudTheme.Icon(icon,15));
        if(icon!="stop")row.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(icon.Length>0?5:0,0,0,0)});
        var b=new Button{Content=row,Padding=new Thickness(8,5,8,5),Margin=new Thickness(0,2,5,2),FontSize=11};
        AutomationProperties.SetName(b,label);b.Click+=(_,_)=>clicked();return b;
    }
    internal BitmapSource CreateFivePhoto()
    {
        if(five?.Key!="photo" || Character.FiveFor(State.Outfit) is not {} art)throw new InvalidOperationException("尚未进入合照。");
        var pose=art.Poses["photo"];var sheet=art.Atlases[pose.Atlas];var frame=Art.Frame(Character,sheet,pose.Frame);
        double k=595/sheet.ReferenceHeightPixels;
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(ItemArt.Brush("#FAF4F6"),null,new Rect(0,0,720,880));
            dc.DrawRoundedRectangle(Brushes.White,null,new Rect(30,30,660,820),28,28);
            dc.DrawImage(frame,new Rect(250-pose.FootX*k,725-pose.FootY*k,frame.PixelWidth*k,frame.PixelHeight*k));
            dc.PushTransform(new TranslateTransform(456,350));dc.PushTransform(new ScaleTransform(7,7));
            dc.DrawGeometry(ItemArt.Brush("#E2EEF2"),new Pen(ItemArt.Brush("#B1CBD5"),.4),ItemArt.Path("M4,17 C-1,17 0,9 5,9 C5,1 17,1 18,9 C24,9 24,17 18,17 Z"));dc.DrawEllipse(ItemArt.Brush("#77939F"),null,new Point(8,11),.6,.8);dc.DrawEllipse(ItemArt.Brush("#77939F"),null,new Point(15,11),.6,.8);dc.Pop();dc.Pop();
            void Label(string value,double x,double y,double size){var t=new FormattedText(value,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),size,CloudTheme.Ink,1);dc.DrawText(t,new Point(x-t.Width/2,y));}
            Label("今天也一起",360,65,29);Label(string.IsNullOrWhiteSpace(photoName)?"我":photoName,540,500,22);
            Label(Character.Name+"  ♡  "+(string.IsNullOrWhiteSpace(photoName)?"我":photoName),360,768,21);Label(DateTime.Now.ToString("yyyy.MM.dd"),360,808,15);
        }
        var bmp=new RenderTargetBitmap(720,880,96,96,PixelFormats.Pbgra32);bmp.Render(visual);bmp.Freeze();return bmp;
    }
    private void SaveFivePhoto()
    {
        if(five?.Phase!="capture")return;
        var dialog=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png",FileName="一起-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png",InitialDirectory=AppContext.BaseDirectory};
        if(dialog.ShowDialog(this)!=true)return;
        try{var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(CreateFivePhoto()));using(var file=File.Create(dialog.FileName))encoder.Save(file);five.PhotoSaved();}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){MessageBox.Show(this,"图片未保存："+ex.Message);}
        Render();
    }
    internal void PreviewFive(string key,double elapsed)
    {
        ClearTransient();previewClock=elapsed;five=new FiveInteraction(new PetState(),new Random(9));five.Start(key);SetAction(key);fiveSavedRevision=0;
        for(double at=0;at<elapsed;at+=20)
        {
            if(key=="highfive"&&at>=1000)five.HighFive();
            if(key=="rps"&&at>=400)five.Choose("paper");
            if(key=="gift"){if(at>=400)five.Deliver();if(at>=1400)five.Unwrap();}
            if(key=="read"&&at>=200){five.BeginRead();if(at>=1600&&five.Sentence==0)five.NextSentence();}
            if(key=="photo"&&at>=200)five.Shutter();
            five.Advance(Math.Min(20,elapsed-at));
        }
        fiveControlState="";Render();
    }
}
