using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class DanceVerification
{
    internal static async Task Run(PetWindow pet,string output,string? appearance=null)
    {
        Directory.CreateDirectory(output);
        var checks=new List<string>();
        void Require(bool pass,string label){if(!pass)throw new InvalidOperationException(label);checks.Add("PASS "+label);}
        pet.State.Size=300;pet.State.Opacity=1;pet.State.Wander=pet.State.ReducedMotion=false;pet.BeginPreview();
        var canvas=(Canvas)pet.Content;
        var characters=pet.Catalog.Characters.Where(c=>c.Category=="adult"&&(appearance is null||appearance==c.Id)).ToArray();
        Require(characters.Length>0,"at least one requested dance character exists");
        foreach(var c in characters)
        {
            var sheet=new DrawingVisual();
            using(var dc=sheet.RenderOpen())
            {
                dc.DrawRectangle(CloudTheme.Cream,null,new Rect(0,0,1440,1020));
                int row=0;
                foreach(string outfit in new[]{"original","swim","wedding"})
                {
                    string key=c.Id+"/"+outfit;
                    var clip=c.MotionFor(outfit,"dance");Require(clip?.DanceRig is not null,key+": dedicated measured dance base");
                    pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]=outfit;pet.ApplySettings();
                    for(int sample=0;sample<=64;sample++)
                    {
                        pet.PreviewDance(sample*125);pet.UpdateLayout();var visual=pet.ActiveDance!;
                        var rig=visual.Rig;var pose=visual.Pose;
                        Require(ReferenceEquals(visual.Texture,pet.Art.Frame(c,clip!,clip!.Frames![0])),key+": current outfit texture");
                        Require(pose.Bones[8].B.Y<=rig.Rest[8].B.Y+1e-8&&pose.Bones[11].B.Y<=rig.Rest[11].B.Y+1e-8,key+": soles never penetrate floor");
                        Require(pose.Bones[8].A.X<pose.Bones[11].A.X,key+": ankles never cross");
                        Require(pose.Bones.Zip(rig.Rest).All(p=>{double ratio=(p.First.B-p.First.A).Length/(p.Second.B-p.Second.A).Length;return ratio is >=.9 and <=1.01;}),key+": no limb stretching; only shallow frontal foreshortening");
                        var vertices=rig.Skin(pose);Require(vertices.All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y)),key+": finite mesh");
                        Require(Math.Abs(pose.Bones[8].B.Y-rig.Rest[8].B.Y)<1e-8||Math.Abs(pose.Bones[11].B.Y-rig.Rest[11].B.Y)<1e-8,key+": at least one supporting sole is grounded");
                        var next=rig.Pose(sample*125+16);
                        Require(pose.Bones.Zip(next.Bones).All(p=>(p.First.B-p.Second.B).Length<.007),key+": no joint jump between 60 Hz frames");
                        if(sample is 0 or 64)Require(vertices.Zip(rig.Vertices).All(p=>(p.First-p.Second).Length<1e-8),key+": neutral start and end");
                    }
                    double[] times=[0,750,1250,2250,4250,5750];
                    for(int col=0;col<times.Length;col++)
                    {
                        pet.PreviewDance(times[col]);pet.UpdateLayout();
                        var frame=new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32);frame.Render(canvas);
                        dc.DrawImage(new CroppedBitmap(frame,new Int32Rect(95,115,370,365)),new Rect(col*240,row*340+8,240,237));
                        dc.DrawText(new FormattedText($"{outfit} / {times[col]:0} ms",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),13,CloudTheme.Ink,1),new Point(col*240+20,row*340+275));
                    }
                    var detail=new Canvas {Width=1680,Height=620,Background=CloudTheme.Cream};
                    for(int col=0;col<3;col++)
                    {
                        double time=new[]{0d,1250,5000}[col];
                        var view=new RigVisual(pet.ActiveDance!.Texture,c.FamilyId,outfit,danceRig:clip!.DanceRig){Width=560,Height=560};
                        view.Update(time);detail.Children.Add(view);Canvas.SetLeft(view,col*560);
                        var label=new TextBlock {Text=$"{c.Id} / {outfit} / {time:0} ms",FontSize=18,Foreground=CloudTheme.Ink};
                        detail.Children.Add(label);Canvas.SetLeft(label,col*560+30);Canvas.SetTop(label,580);
                    }
                    detail.Measure(new Size(1680,620));detail.Arrange(new Rect(0,0,1680,620));
                    var enlarged=new RenderTargetBitmap(1680,620,96,96,PixelFormats.Pbgra32);enlarged.Render(detail);
                    var detailPng=new PngBitmapEncoder();detailPng.Frames.Add(BitmapFrame.Create(enlarged));using(var stream=File.Create(Path.Combine(output,$"{c.Id}-{outfit}-detail.png")))detailPng.Save(stream);
                    row++;pet.StopInteraction();
                }
            }
            var proof=new RenderTargetBitmap(1440,1020,96,96,PixelFormats.Pbgra32);proof.Render(sheet);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(proof));using(var stream=File.Create(Path.Combine(output,c.Id+"-dance.png")))png.Save(stream);
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }
        pet.EndPreview();pet.SelectCharacter(characters[0].Id);pet.State.Outfits[pet.State.Character]="original";pet.ApplySettings();
        pet.RunInteraction("dance");
        Require(pet.WalkUsesRendering,"live dance uses compositor presentation clock");
        var cadence=new List<double>();TimeSpan? previous=null;
        void Presented(object? sender,EventArgs args)
        {
            var stamp=((RenderingEventArgs)args).RenderingTime;
            if(previous is { } last && stamp>last)cadence.Add((stamp-last).TotalMilliseconds);
            previous=stamp;
        }
        CompositionTarget.Rendering+=Presented;
        try { await Task.Delay(2000); }
        finally { CompositionTarget.Rendering-=Presented; }
        Require(pet.IsDancing && pet.ActiveDance!.Pose.Bones.Zip(pet.ActiveDance.Rig.Rest).Any(p=>(p.First.B-p.Second.B).Length>.01),"live dance advances measured joints");
        pet.StopInteraction();Require(!pet.WalkUsesRendering,"stopping dance releases compositor callback");
        cadence.Sort();
        File.WriteAllText(Path.Combine(output,"dance-cadence.txt"),cadence.Count==0?"No compositor interval observed.":
            $"Intervals: {cadence.Count}; median: {cadence[cadence.Count/2]:0.00} ms; p95: {cadence[(int)((cadence.Count-1)*.95)]:0.00} ms. Local diagnostic, not a universal performance guarantee.");
        File.WriteAllLines(Path.Combine(output,"dance-check.txt"),checks.Append($"{checks.Count} dance checks passed."));
    }
}
