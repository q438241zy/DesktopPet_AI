using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;
using SkiaSharp;

namespace DesktopPet.App;

/// <summary>Repeatable animation export from the desktop's own renderer.</summary>
internal static class DemoExporter
{
    internal static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output);
        var coverage = ActionCoverage.All(pet.Catalog);
        File.WriteAllText(Path.Combine(output,"action-coverage.json"),JsonSerializer.Serialize(coverage,Json.Options));
        var markdown = new List<string> { "# 動作覆蓋清單", "", "自動讀取目前角色清單。近似動作和缺少動作不視為完成；靜態姿勢需配合使用情境閱讀。", "", "| 角色 | 風格 | 服裝 | 動作 | 狀態 | 說明 |", "|---|---|---|---|---|---|" };
        markdown.AddRange(coverage.Select(r=>$"| {r.Character} | {r.Category} | {r.Outfit} | {r.Title} | {r.Status} | {r.Detail} |"));
        File.WriteAllLines(Path.Combine(output,"動作清單.md"),markdown);
        pet.State.Size=240; pet.State.Wander=pet.State.ReducedMotion=false; pet.State.Opacity=1; pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now));
        pet.BeginPreview();
        var frames = new List<string>(); var hashes = new Dictionary<string,int>();
        var clips = new Dictionary<string,Dictionary<string,object>>();
        var headTops = new Dictionary<string,double>();
        var canvas=(Canvas)pet.Content;
        int Capture()
        {
            canvas.UpdateLayout();
            // Preserve the entire 560x680 logical surface. VisualBrush's automatic
            // content bounds would stretch compact poses and destroy the baseline.
            var bmp=new RenderTargetBitmap(280,340,48,48,PixelFormats.Pbgra32); bmp.Render(canvas);
            byte[] pixels=new byte[280*340*4]; bmp.CopyPixels(pixels,280*4,0);
            string hash=Convert.ToHexString(SHA256.HashData(pixels));
            if(hashes.TryGetValue(hash,out int existing)) return existing;
            using var data=SKData.CreateCopy(pixels);
            using var image=SKImage.FromPixels(new SKImageInfo(280,340,SKColorType.Bgra8888,SKAlphaType.Premul),data,280*4);
            using var encoded=image.Encode(SKEncodedImageFormat.Webp,85);
            int index=frames.Count; frames.Add("data:image/webp;base64,"+Convert.ToBase64String(encoded.ToArray())); hashes[hash]=index; return index;
        }
        foreach(var c in pet.Catalog.Characters.Where(c=>c.FamilyId=="whale"))
        foreach(string outfit in new[]{"original","swim","wedding"})
        {
            pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id]=outfit; pet.ApplySettings();
            pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280; pet.Top=pet.WorkArea.Bottom-468;
            var actions=new Dictionary<string,object>(); clips[c.Category+"-"+outfit]=actions;
            pet.PreviewMotion("listen",0,0);
            var sprite=canvas.Children.OfType<System.Windows.Controls.Image>().Single();
            headTops[c.Category+"-"+outfit]=(468-sprite.Height*pet.Art.VisibleHeight((BitmapSource)sprite.Source))/2;
            foreach(var a in ActionCoverage.Actions)
            {
                pet.StopInteraction();
                var row=coverage.Single(r=>r.Character==c.Id && r.Outfit==outfit && r.Action==a.Key);
                if(row.Status=="不适用") continue;
                var sequence=new List<int>();
                for(int t=0;t<=a.Duration;t+=100)
                {
                    if(a.Key=="dance") pet.PreviewDance(t);
                    else pet.PreviewMotion(a.Motion,t,a.Duration);
                    sequence.Add(Capture());
                    if(t%500==0) await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                }
                actions[a.Key]=new { frames=sequence, duration=a.Duration, status=row.Status, detail=row.Detail };
            }
            File.AppendAllText(Path.Combine(output,"demo-progress.txt"),c.Id+"/"+outfit+" exported\n");
        }
        var payload=new { version=typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0], generatedAt=DateTimeOffset.Now, actions=ActionCoverage.Actions, coverage, frames, clips, headTops,
            source="WPF desktop renderer sampled at 100ms; browser interaction simulation is separate from Windows input", frameWidth=280,frameHeight=340 };
        string json=JsonSerializer.Serialize(payload,Json.Options);
        string template=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Demo","template.html"));
        File.WriteAllText(Path.Combine(output,"DeepSeek-demo.html"),template.Replace("__DEMO_DATA__",json.Replace("<","\\u003c")));
        File.WriteAllText(Path.Combine(output,"demo-export.json"),JsonSerializer.Serialize(new { appearances=clips.Count, actions=clips.Values.Sum(c=>c.Count), uniqueFrames=frames.Count, coverageRows=coverage.Length, missing=coverage.Count(r=>r.Status=="缺少动作"), approximate=coverage.Count(r=>r.Status=="近似动作") },Json.Options));
    }
}
