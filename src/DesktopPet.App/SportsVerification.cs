using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Checks the complete short-sleeve/shorts wardrobe against the native renderer.</summary>
internal static class SportsVerification
{
    internal static async Task Run(PetWindow pet,string output,bool samplingOnly=false,string? appearance=null)
    {
        Directory.CreateDirectory(output);
        var checks=new List<string>();
        void Require(bool ok,string message) { if(!ok)throw new InvalidOperationException(message);checks.Add("PASS "+message); }
        var sequence=new Sprite("test.png",3,3,FrameMs:[100,200,300],Frames:[2,7,4],Loop:false);
        Require(Motion.Blend(sequence,-10)==new ClubPose(2,7,0,0,false),"negative time starts at the first authored frame");
        Require(Motion.Blend(sequence,50)==new ClubPose(2,7,.5,0,false),"midpoints respect nonsequential atlas indices");
        Require(Motion.Blend(sequence,100)==new ClubPose(7,4,0,0,false),"phase boundaries advance without a repeated start");
        Require(Motion.Blend(sequence,1000)==new ClubPose(4,4,0,0,false),"nonlooping final pose holds");
        Require(Motion.Blend(sequence with {Loop=true},450)==new ClubPose(4,2,.5,0,false),"loop seam interpolates back to its authored first frame");
        Require(Motion.Blend(sequence with {Loop=true},650)==new ClubPose(2,7,.5,0,false),"loop timing does not accumulate drift");
        Require(!ClubMotion.HasPoses("comb",null) && ClubMotion.HasPoses("comb",sequence),"legacy care effects do not claim a dedicated actor sheet");
        if(samplingOnly) { File.WriteAllLines(Path.Combine(output,"sports-sampling-check.txt"),checks);return; }
        var characters=pet.Catalog.Characters.Where(c=>Catalog.BuiltInFamilies.Contains(c.FamilyId) && (appearance is null || appearance==c.Id+"/sports" || appearance==c.Id+"-sports")).ToArray();
        Require(characters.Length==(appearance is null?Catalog.BuiltInFamilies.Length*CharacterStyles.All.Length:1),appearance is null?"both styles of all eight companions installed":"requested single sports pilot exists");
        pet.State.Size=240;pet.State.Opacity=1;pet.State.Wander=pet.State.ReducedMotion=false;pet.BeginPreview();
        var canvas=(Canvas)pet.Content;var image=canvas.Children.OfType<Image>().Single();
        byte[] Capture(string? name=null)
        {
            var previous=canvas.Background;canvas.Background=CloudTheme.Cream;pet.UpdateLayout();
            var shot=new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32);shot.Render(canvas);canvas.Background=previous;
            byte[] pixels=new byte[560*680*4];shot.CopyPixels(pixels,560*4,0);
            if(name is not null)
            {
                void Save(RenderTargetBitmap bitmap,string suffix) { var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(Path.Combine(output,name+suffix+".png"));png.Save(stream); }
                Save(shot,"");
                canvas.Background=new SolidColorBrush(Color.FromRgb(35,41,52));pet.UpdateLayout();
                var dark=new RenderTargetBitmap(560,680,96,96,PixelFormats.Pbgra32);dark.Render(canvas);Save(dark,"-dark");
                canvas.Background=previous;
            }
            return SHA256.HashData(pixels);
        }
        string[] required=["listen","walk","chat","think","headpat","poke","tickle","eat","meal","jump","land","curl","sleep","build","pickup","ball-ready","ball-hit","ball-miss","bonk","dizzy","happy","farewell","comb","wipe","stars","bubbles","stretch"];
        foreach(var c in characters)
        {
            bool reviewCharacter=appearance is not null || c.FamilyId is "whale" or "gpt";
            Require(c.Outfits.TryGetValue("sports",out var outfit),c.Id+": sports outfit installed");
            Require(outfit!.Idle is not null && outfit.Idle.File.StartsWith("outfits/sports/",StringComparison.Ordinal),c.Id+": sports has its own idle art");
            foreach(string key in required)
            {
                Require(outfit.Motions.TryGetValue(key,out var clip),c.Id+"/"+key+": exact sports motion exists");
                Require(clip!.File.StartsWith("outfits/sports/",StringComparison.Ordinal),c.Id+"/"+key+": no original/swim/wedding art fallback");
                Require(ClubPoseVisual.Supports(c,clip),c.Id+"/"+key+": measured continuous pose data exists");
                if(key is "eat" or "meal" or "build" or "bonk" or "comb" or "wipe" or "bubbles")
                    Require(clip.BakedProps,c.Id+"/"+key+": prop belongs to the authored hands");
            }
            pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]="sports";pet.ApplySettings();pet.BeginPreview();
            if(reviewCharacter)Capture(c.Id+"-sports-idle");
            foreach(string key in required)
            {
                var clip=outfit.Motions[key];pet.StopInteraction();pet.BeginPreview();
                int[] frames=clip.Frames??Enumerable.Range(0,clip.Columns*clip.Rows).ToArray();
                int[] durations=clip.FrameMs??Enumerable.Repeat(240,frames.Length).ToArray();
                double end=key=="jump"?Math.Min(879,durations.Sum()):durations.Sum();
                double[] times=ClubMotion.HasPoses(key)?key=="bubbles"?[1100,1400,1750,2200]:key=="stars"?[750,2000,4200,6000]:[450,1000,1750,2500]
                    :[0,Math.Min(durations[0]*.25,end),Math.Min(durations[0]*.75,end),Math.Min(durations[0]+1,end)];
                double? plane=null;
                foreach(double time in times)
                {
                    pet.PreviewMotion(key,time,20000);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
                    var visual=pet.ActiveClubVisual??pet.ActiveAuthoredVisual;
                    Require(visual is not null && visual.Appearance==c.Id+"/sports" && visual.SheetFile==clip.File,c.Id+"/"+key+": native renderer uses the selected sports sheet");
                    Require(ReferenceEquals(image.Source,pet.Art.Frame(c,clip,pet.DrawnFrame)),c.Id+"/"+key+": proxy and mesh share the displayed source pose");
                    if(key!="sleep") { plane??=visual!.Height;Require(Math.Abs(visual!.Height-plane.Value)<.001,c.Id+"/"+key+": fixed actor plane through raised and bent poses"); }
                    Require(Math.Abs(Canvas.GetTop(image)+image.Height*pet.Art.GroundLine((BitmapSource)image.Source)+pet.AirborneOffset-468)<.01,c.Id+"/"+key+": grounded proxy baseline");
                    if(clip.Hands?[visual!.Current.A] is not null || clip.Hands?[visual!.Current.B] is not null)
                        Require(pet.HandTarget is { } p && double.IsFinite(p.X) && double.IsFinite(p.Y),c.Id+"/"+key+": hand contact follows the interpolated pose");
                }
                if(reviewCharacter && key is "walk" or "headpat" or "eat" or "meal" or "build" or "jump" or "curl" or "sleep" or "pickup" or "ball-ready" or "ball-hit" or "bonk")
                {
                    double reviewTime=key switch { "walk"=>320,"ball-ready"=>0,"ball-hit"=>300,"bonk"=>500,"jump"=>450,"build"=>2600,"eat" or "meal"=>1500,"pickup"=>750,"curl" or "sleep"=>1800,_=>800 };
                    pet.PreviewMotion(key,reviewTime,20000);Capture(c.Id+"-sports-"+key);
                }
                if(key is "comb" or "wipe" or "bubbles" or "stars" or "stretch")
                {
                    pet.PreviewMotion(key,times[1],20000);var before=Capture();pet.PreviewMotion(key,times[2],20000);
                    Require(!Capture(reviewCharacter?c.Id+"-sports-"+key:null).SequenceEqual(before),c.Id+"/"+key+": actual rendered poses advance");
                }
                if(key=="bubbles")
                {
                    var source=clip.BubbleSources;Require(source is not null,c.Id+": bubble ring has measured crop anchors");
                    foreach(double birth in new[]{1120d,1390,1660,1930,2200})
                    {
                        var pose=ClubMotion.Sample("bubbles",birth,clip);
                        Require(source![pose.A] is not null && source[pose.B] is not null,c.Id+": every emitted bubble has both measured interpolation endpoints");
                    }
                }
            }
            pet.PreviewMotion("ball-ready",0,20000);var contactClip=outfit.Motions["ball-ready"];
            Require(contactClip.Hands?.Any(h=>h is not null)==true && pet.HandTarget is not null,c.Id+": butterfly can land at the sports palm");
            pet.State.Outfits[c.Id]="original";pet.ApplySettings();
            Require(pet.ActiveAuthoredVisual is null && pet.ActiveClubVisual is null,c.Id+": changing clothes removes the old sports visual");
        }
        pet.EndPreview();
        File.WriteAllLines(Path.Combine(output,"sports-check.txt"),checks.Append($"PASS {checks.Count} checks across {characters.Length} sports appearances ({(appearance is null?"full roster":"pilot only")}). Visual inspection still required for clothing and hand anatomy."));
    }
}
